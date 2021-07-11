// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Linq;

using Niantic.ARDK.AR;
using Niantic.ARDK.AR.Anchors;
using Niantic.ARDK.AR.ARSessionEventArgs;
using Niantic.ARDK.AR.Awareness;
using Niantic.ARDK.AR.Configuration;
using Niantic.ARDK.AR.Camera;
using Niantic.ARDK.AR.Frame;
using Niantic.ARDK.AR.Mesh;
using Niantic.ARDK.AR.SLAM;
using Niantic.ARDK.LocationService;
using Niantic.ARDK.Utilities;
using Niantic.ARDK.Utilities.Logging;
using Niantic.ARDK.VirtualStudio.Remote;
using Niantic.ARDK.VirtualStudio.Remote.Data;

using UnityEngine;

namespace Niantic.ARDK.VirtualStudio.AR.Remote
{
  /// <inheritdoc />
  internal sealed class _RemoteDeviceARSession:
    IARSession
  {
    private readonly IARSession _session;

    public IARSession InnerARSession
    {
      get { return _session; }
    }

    /// <inheritdoc />
    public IARConfiguration Configuration { get; private set; }

    /// <inheritdoc />
    public IARFrame CurrentFrame
    {
      get { return _session.CurrentFrame; }
    }

    /// <inheritdoc />
    public ARFrameDisposalPolicy DefaultFrameDisposalPolicy
    {
      get { return _session.DefaultFrameDisposalPolicy; }
      set { _session.DefaultFrameDisposalPolicy = value; }
    }

    /// <inheritdoc />
    public float WorldScale
    {
      get { return _session.WorldScale; }
      set { _session.WorldScale = value; }
    }

    /// <inheritdoc />
    public Guid StageIdentifier
    {
      get { return _session.StageIdentifier; }
    }

    /// <inheritdoc />
    public ARSessionState State
    {
      get { return _session.State; }
    }

    private readonly bool _compressImageData;

    internal _RemoteDeviceARSession(Guid stageIdentifier, bool compressImageData)
    {
      _FriendTypeAsserter.AssertCallerIs(typeof(_RemoteDeviceARSessionConstructor));

      _session = ARSessionFactory.Create(stageIdentifier);
      _compressImageData = compressImageData;

      if (!_compressImageData)
        _RemoteConnection.OverrideTargetFramerate(10);

      _EasyConnection.Register<ARSessionRunMessage>(HandleRunMessage);
      _EasyConnection.Register<ARSessionPauseMessage>(HandlePauseMessage);
      _EasyConnection.Register<ARSessionAddAnchorMessage>(HandleAddAnchorMessage);
      _EasyConnection.Register<ARSessionRemoveAnchorMessage>(HandleRemoveAnchorMessage);
      _EasyConnection.Register<ARSessionSetWorldScaleMessage>(HandleSetWorldScaleMessage);
      _EasyConnection.Register<ARSessionDestroyMessage>(HandleDestroyMessage);

      FrameUpdated += OnInternalDidUpdateFrame;
      AnchorsAdded += OnInternalDidAddAnchors;
      AnchorsUpdated += OnInternalDidUpdateAnchors;
      AnchorsRemoved += OnInternalDidRemoveAnchors;
      AnchorsMerged += OnInternalDidMergeAnchors;
      MapsAdded += OnInternalDidAddMaps;
      MapsUpdated += OnInternalDidUpdateMaps;

      CameraTrackingStateChanged += OnInternalCameraDidChangeTrackingState;
      SessionInterrupted += OnInternalSessionWasInterrupted;
      SessionInterruptionEnded += OnInternalSessionInterruptionEnded;
      SessionFailed += OnInternalSessionDidFailWithError;
    }

    private bool _isDestroyed;

    ~_RemoteDeviceARSession()
    {
      ARLog._Error("_RemoteDeviceARSession should be destroyed by an explicit call to Dispose().");
    }

    /// <inheritdoc />
    public void Dispose()
    {
      GC.SuppressFinalize(this);

      if (_isDestroyed)
        return;

      _EasyConnection.Unregister<ARSessionRunMessage>();
      _EasyConnection.Unregister<ARSessionPauseMessage>();
      _EasyConnection.Unregister<ARSessionAddAnchorMessage>();
      _EasyConnection.Unregister<ARSessionRemoveAnchorMessage>();
      _EasyConnection.Unregister<ARSessionSetWorldScaleMessage>();
      _EasyConnection.Unregister<ARSessionDestroyMessage>();

      FrameUpdated -= OnInternalDidUpdateFrame;
      AnchorsAdded -= OnInternalDidAddAnchors;
      AnchorsUpdated -= OnInternalDidUpdateAnchors;
      AnchorsRemoved -= OnInternalDidRemoveAnchors;
      AnchorsMerged -= OnInternalDidMergeAnchors;
      MapsAdded -= OnInternalDidAddMaps;
      MapsUpdated -= OnInternalDidUpdateMaps;

      CameraTrackingStateChanged -= OnInternalCameraDidChangeTrackingState;
      SessionInterrupted -= OnInternalSessionWasInterrupted;
      SessionInterruptionEnded -= OnInternalSessionInterruptionEnded;
      SessionFailed -= OnInternalSessionDidFailWithError;

      _session.Dispose();
      _isDestroyed = true;
    }

    /// <inheritdoc />
    public void Run(IARConfiguration configuration, ARSessionRunOptions options = ARSessionRunOptions.None)
    {
      Configuration = configuration;
      _session.Run(configuration, options);
    }

    /// <inheritdoc />
    public void Pause()
    {
      _session.Pause();
    }

    /// <inheritdoc />
    public IARAnchor AddAnchor(Matrix4x4 transform)
    {
      return _session.AddAnchor(transform);
    }

    /// <inheritdoc />
    public void RemoveAnchor(IARAnchor anchor)
    {
      _session.RemoveAnchor(anchor);
    }

    public void SetupLocationService(ILocationService locationService)
    {
      _session.SetupLocationService(locationService);
    }

    public AwarenessInitializationStatus GetAwarenessInitializationStatus
    (
      out AwarenessInitializationError error,
      out string errorMessage
    )
    {
      return _session.GetAwarenessInitializationStatus(out error, out errorMessage);
    }

    /// <inheritdoc />
    public event ArdkEventHandler<ARSessionDeinitializedArgs> Deinitialized
    {
      add { _session.Deinitialized += value; }
      remove { _session.Deinitialized -= value; }
    }

    /// <inheritdoc />
    public event ArdkEventHandler<ARSessionRanArgs> Ran
    {
      add { _session.Ran += value; }
      remove { _session.Ran -= value; }
    }

    /// <inheritdoc />
    public event ArdkEventHandler<ARSessionPausedArgs> Paused
    {
      add { _session.Paused += value; }
      remove { _session.Paused -= value; }
    }

    /// <inheritdoc />
    public event ArdkEventHandler<AnchorsMergedArgs> AnchorsMerged
    {
      add { _session.AnchorsMerged += value; }
      remove { _session.AnchorsMerged -= value; }
    }

    /// <inheritdoc />
    public event ArdkEventHandler<FrameUpdatedArgs> FrameUpdated
    {
      add { _session.FrameUpdated += value; }
      remove { _session.FrameUpdated -= value; }
    }

    public event ArdkEventHandler<MeshUpdatedArgs> MeshUpdated
    {
      add { _session.MeshUpdated += value; }
      remove { _session.MeshUpdated -= value; }
    }

    /// <inheritdoc />
    public event ArdkEventHandler<AnchorsArgs> AnchorsAdded
    {
      add { _session.AnchorsAdded += value; }
      remove { _session.AnchorsAdded -= value; }
    }

    /// <inheritdoc />
    public event ArdkEventHandler<AnchorsArgs> AnchorsUpdated
    {
      add { _session.AnchorsUpdated += value; }
      remove { _session.AnchorsUpdated -= value; }
    }

    /// <inheritdoc />
    public event ArdkEventHandler<AnchorsArgs> AnchorsRemoved
    {
      add { _session.AnchorsRemoved += value; }
      remove { _session.AnchorsRemoved -= value; }
    }

    /// <inheritdoc />
    public event ArdkEventHandler<MapsArgs> MapsAdded
    {
      add { _session.MapsAdded += value; }
      remove { _session.MapsAdded -= value; }
    }

    /// <inheritdoc />
    public event ArdkEventHandler<MapsArgs> MapsUpdated
    {
      add { _session.MapsUpdated += value; }
      remove { _session.MapsUpdated -= value; }
    }

    /// <inheritdoc />
    public event ArdkEventHandler<CameraTrackingStateChangedArgs> CameraTrackingStateChanged
    {
      add { _session.CameraTrackingStateChanged += value; }
      remove { _session.CameraTrackingStateChanged -= value; }
    }

    /// <inheritdoc />
    public event ArdkEventHandler<ARSessionInterruptedArgs> SessionInterrupted
    {
      add { _session.SessionInterrupted += value; }
      remove { _session.SessionInterrupted -= value; }
    }

    /// <inheritdoc />
    public event ArdkEventHandler<ARSessionInterruptionEndedArgs> SessionInterruptionEnded
    {
      add { _session.SessionInterruptionEnded += value; }
      remove { _session.SessionInterruptionEnded -= value; }
    }

    /// <inheritdoc />
    public event ArdkEventHandler<QueryingShouldSessionAttemptRelocalizationArgs> QueryingShouldSessionAttemptRelocalization
    {
      add { _session.QueryingShouldSessionAttemptRelocalization += value; }
      remove { _session.QueryingShouldSessionAttemptRelocalization -= value; }
    }

    /// <inheritdoc />
    public event ArdkEventHandler<ARSessionFailedArgs> SessionFailed
    {
      add { _session.SessionFailed += value; }
      remove { _session.SessionFailed -= value; }
    }

    ARInfoSource IARSession.ARInfoSource
    {
      get { return _session.ARInfoSource; }
    }

    #region CallbackFowarding

    private void OnInternalDidUpdateFrame(FrameUpdatedArgs args)
    {
      int compressionLevel = 0;
      if (_compressImageData)
        compressionLevel = 70;

      var frame = args.Frame;
      _EasyConnection.Send
      (
        new ARSessionDidUpdateFrameMessage
        {
          Frame = frame.LazySerialize(compressionLevel)._AsSerializable()
        }
      );
    }

    private void OnInternalDidUpdateMesh(IARMesh mesh)
    {
      _EasyConnection.Send
      (
        new ARSessionDidUpdateMeshMessage
        {
          // TODO: Serialize properly
          Mesh = new _SerializableARMesh(0)
        }
      );
    }

    private static void OnInternalDidAddAnchors(AnchorsArgs args)
    {
      var anchors = args.Anchors;
      var anchorArray = (
        from anchor in anchors
        where anchor.AnchorType == AnchorType.Base
        select anchor._AsSerializableBase()).ToArray();

      var planeAnchorArray = (
        from anchor in anchors
        where anchor.AnchorType == AnchorType.Plane
        select ((IARPlaneAnchor)anchor)._AsSerializablePlane()).ToArray();

      var imageAnchorArray = (
        from anchor in anchors
        where anchor.AnchorType == AnchorType.Image
        select ((IARImageAnchor)anchor)._AsSerializableImage()).ToArray();


      // TODO: We could just serialize everything in a single array.
      // The serializer knows what to do!
      _EasyConnection.Send
      (
        new ARSessionDidAddAnchorsMessage
        {
          Anchors = anchorArray,
          PlaneAnchors = planeAnchorArray,
          ImageAnchors = imageAnchorArray
        }
      );
    }

    private static void OnInternalDidUpdateAnchors(AnchorsArgs args)
    {
      var anchors = args.Anchors;
      var anchorArray = (
        from anchor in anchors
        where anchor.AnchorType == AnchorType.Base
        select anchor._AsSerializableBase()).ToArray();

      var planeAnchorArray = (
        from anchor in anchors
        where anchor.AnchorType == AnchorType.Plane
        select ((IARPlaneAnchor)anchor)._AsSerializablePlane()).ToArray();

      var imageAnchorArray = (
        from anchor in anchors
        where anchor.AnchorType == AnchorType.Image
        select ((IARImageAnchor)anchor)._AsSerializableImage()).ToArray();


      _EasyConnection.Send
      (
        new ARSessionDidUpdateAnchorsMessage
        {
          Anchors = anchorArray,
          PlaneAnchors = planeAnchorArray,
          ImageAnchors = imageAnchorArray
        }
      );
    }

    private static void OnInternalDidRemoveAnchors(AnchorsArgs args)
    {
      var anchors = args.Anchors;
      var anchorArray = (
        from anchor in anchors
        where anchor.AnchorType == AnchorType.Base
        select anchor._AsSerializableBase()).ToArray();

      var planeAnchorArray = (
        from anchor in anchors
        where anchor.AnchorType == AnchorType.Plane
        select ((IARPlaneAnchor)anchor)._AsSerializablePlane()).ToArray();

      var imageAnchorArray = (
        from anchor in anchors
        where anchor.AnchorType == AnchorType.Image
        select ((IARImageAnchor)anchor)._AsSerializableImage()).ToArray();

      _EasyConnection.Send
      (
        new ARSessionDidRemoveAnchorsMessage
        {
          Anchors = anchorArray,
          PlaneAnchors = planeAnchorArray,
          ImageAnchors = imageAnchorArray
        }
      );
    }

    private static void OnInternalDidMergeAnchors(AnchorsMergedArgs args)
    {
      var parentQuery = ((IARPlaneAnchor)args.Parent)._AsSerializablePlane();

      var childrenArray =
        (
          from child in args.Children
          select ((IARPlaneAnchor)child)._AsSerializablePlane()
        ).ToArray();

      _EasyConnection.Send
      (
        new ARSessionDidMergeAnchorsMessage
        {
          ParentAnchor = parentQuery, ChildAnchors = childrenArray
        }
      );
    }

    private static void OnInternalDidAddMaps(MapsArgs args)
    {
      var serializableMaps = args.Maps._AsSerializableArray();
      _EasyConnection.Send(new ARSessionDidAddMapsMessage { Maps = serializableMaps });
    }

    private static void OnInternalDidUpdateMaps(MapsArgs args)
    {
      var serializableMaps = args.Maps._AsSerializableArray();
      _EasyConnection.Send(new ARSessionDidUpdateMapsMessage { Maps = serializableMaps });
    }

    private static void OnInternalCameraDidChangeTrackingState(CameraTrackingStateChangedArgs args)
    {
      var serializableCamera = args.Camera._AsSerializable();
      if (serializableCamera == null)
        return;

      _EasyConnection.Send
      (
        new ARSessionCameraDidChangeTrackingStateMessage { Camera = serializableCamera }
      );
    }

    private static void OnInternalSessionWasInterrupted(ARSessionInterruptedArgs args)
    {
      _EasyConnection.Send(new ARSessionWasInterruptedMessage());
    }

    private static void OnInternalSessionInterruptionEnded(ARSessionInterruptionEndedArgs args)
    {
      _EasyConnection.Send(new ARSessionInterruptionEndedMessage());
    }

    private static void OnInternalSessionDidFailWithError(ARSessionFailedArgs args)
    {
      _EasyConnection.Send(new ARSessionDidFailWithError { Error = args.Error });
    }
    #endregion

#region EditorRequests
    private void HandleRunMessage(ARSessionRunMessage message)
    {
      IARConfiguration configuration;

      if (message.serializedWorldConfiguration != null)
        configuration = message.serializedWorldConfiguration;
      else
        throw new Exception("No valid configuration passed to PlayerARSession");

      _session.Run(configuration, message.runOptions);
    }

    private void HandlePauseMessage(ARSessionPauseMessage message)
    {
      _session.Pause();
    }

    private void HandleAddAnchorMessage(ARSessionAddAnchorMessage message)
    {
      // An anchor was added on the remote side. What do we do here?
      throw new NotSupportedException();
    }

    private void HandleRemoveAnchorMessage(ARSessionRemoveAnchorMessage message)
    {
      throw new NotSupportedException();
    }

    private void HandleSetWorldScaleMessage(ARSessionSetWorldScaleMessage message)
    {
      _session.WorldScale = message.WorldScale;
    }

    private void HandleDestroyMessage(ARSessionDestroyMessage message)
    {
      Dispose();
    }
#endregion
  }
}
