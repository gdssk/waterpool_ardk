// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections.ObjectModel;
using System.Linq;

using Niantic.ARDK.AR;
using Niantic.ARDK.AR.Anchors;
using Niantic.ARDK.AR.ARSessionEventArgs;
using Niantic.ARDK.AR.Awareness;
using Niantic.ARDK.AR.Configuration;
using Niantic.ARDK.AR.Depth;
using Niantic.ARDK.AR.Depth.Generators;
using Niantic.ARDK.AR.Frame;
using Niantic.ARDK.AR.PointCloud;
using Niantic.ARDK.AR.SLAM;
using Niantic.ARDK.LocationService;
using Niantic.ARDK.Utilities;
using Niantic.ARDK.Utilities.Collections;
using Niantic.ARDK.Utilities.Logging;
using Niantic.ARDK.VirtualStudio.Remote;
using Niantic.ARDK.VirtualStudio.Remote.Data;

using UnityEngine;
using UnityEngine.Rendering;

namespace Niantic.ARDK.VirtualStudio.AR
{
  internal sealed class _RemoteEditorARSession:
    _IARSession
  {
    private DepthPointCloudGenerator _depthPointCloudGen;

    internal _RemoteEditorARSession(Guid stageIdentifier)
    {
      _FriendTypeAsserter.AssertCallerIs(typeof(ARSessionFactory));

      StageIdentifier = stageIdentifier;
      _ARConfigChangesCollector = new _ARConfigChangesCollector(this);

      _EasyConnection.Send
      (
        new ARSessionInitMessage
        {
          StageIdentifier = stageIdentifier,
#if UNITY_EDITOR_OSX // Only can do image compression on OSX
          UseImageCompression = true,
#else
          UseImageCompression = false,
#endif
        }
      );

      _EasyConnection.Register<ARSessionDidUpdateFrameMessage>(HandleDidUpdateFrame);

      _EasyConnection.Register<ARSessionDidUpdateMeshMessage>(HandleDidUpdateMesh);
      _EasyConnection.Register<ARSessionDidAddAnchorsMessage>(HandleDidAddAnchors);
      _EasyConnection.Register<ARSessionDidUpdateAnchorsMessage>(HandleDidUpdateAnchors);
      _EasyConnection.Register<ARSessionDidMergeAnchorsMessage>(HandleDidMergeAnchors);
      _EasyConnection.Register<ARSessionDidRemoveAnchorsMessage>(HandleDidRemoveAnchors);

      _EasyConnection.Register<ARSessionDidAddMapsMessage>(HandleDidAddMaps);
      _EasyConnection.Register<ARSessionDidUpdateMapsMessage>(HandleDidUpdateMaps);

      _EasyConnection.Register<ARSessionCameraDidChangeTrackingStateMessage>
      (
        HandleCameraDidChangeTrackingState
      );

      _EasyConnection.Register<ARSessionWasInterruptedMessage>(HandleSessionWasInterrupted);
      _EasyConnection.Register<ARSessionInterruptionEndedMessage>(HandleSessionInterruptionEnded);

      _EasyConnection.Register<ARSessionDidFailWithError>(HandleDidFailWithError);
    }

    ~_RemoteEditorARSession()
    {
      ARLog._Error("_RemoteEditorARSession should be destroyed by an explicit call to Dispose().");
    }

    private bool _isDestroyed;
    public void Dispose()
    {
      if (_isDestroyed)
        return;

      _isDestroyed = true;
      GC.SuppressFinalize(this);

      var handler = Deinitialized;
      if (handler != null)
      {
        var args = new ARSessionDeinitializedArgs();
        handler(args);
      }

      _EasyConnection.Unregister<ARSessionDidUpdateFrameMessage>();
      _EasyConnection.Unregister<ARSessionDidAddAnchorsMessage>();
      _EasyConnection.Unregister<ARSessionDidUpdateAnchorsMessage>();
      _EasyConnection.Unregister<ARSessionDidMergeAnchorsMessage>();
      _EasyConnection.Unregister<ARSessionDidRemoveAnchorsMessage>();
      _EasyConnection.Unregister<ARSessionDidAddMapsMessage>();
      _EasyConnection.Unregister<ARSessionDidUpdateMapsMessage>();
      _EasyConnection.Unregister<ARSessionCameraDidChangeTrackingStateMessage>();
      _EasyConnection.Unregister<ARSessionWasInterruptedMessage>();
      _EasyConnection.Unregister<ARSessionInterruptionEndedMessage>();
      _EasyConnection.Unregister<ARSessionDidFailWithError>();

      _EasyConnection.Send(new ARSessionDestroyMessage());

      // Dispose of any generators that we've created.
      DisposeGenerators();
    }

    private void DisposeGenerators()
    {
      var depthPointCloudGen = _depthPointCloudGen;
      if (depthPointCloudGen != null)
      {
        _depthPointCloudGen = null;
        depthPointCloudGen.Dispose();
      }
    }

    public Guid StageIdentifier { get; private set; }

    private IARFrame _currentFrame;
    /// <inheritdoc />
    public IARFrame CurrentFrame
    {
      get { return _currentFrame; }
      internal set
      {
        _SessionFrameSharedLogic._MakeSessionFrameBecomeNonCurrent(this);
        _currentFrame = value;
      }
    }

    /// <inheritdoc />
    public ARFrameDisposalPolicy DefaultFrameDisposalPolicy { get; set; }

    public IARConfiguration Configuration { get; private set; }

    private float _worldScale = 1.0f;
    public float WorldScale
    {
      get { return _worldScale; }
      set
      {
        _EasyConnection.Send(new ARSessionSetWorldScaleMessage { WorldScale = value });
        _worldScale = value;
      }
    }

    public ARSessionState State { get; private set; }

    public _ARConfigChangesCollector _ARConfigChangesCollector { get; }

    public ARSessionRunOptions RunOptions { get; private set; }

    public void Run(IARConfiguration configuration, ARSessionRunOptions options = ARSessionRunOptions.None)
    {
      _ARConfigChangesCollector._CollectChanges(configuration);

      if (!_ARConfigurationValidator.RunAllChecks(this, configuration))
        return;

      Configuration = configuration;
      RunOptions = options;

      State = ARSessionState.Running;

      // Need to destroy the generators so they can be recreated once we get new depth data
      DisposeGenerators();

      if (configuration is IARWorldTrackingConfiguration worldConfiguration)
      {
        // Cache the depth features and only ask remote for raw depth.
        var depthFeatures = worldConfiguration.DepthFeatures;
        worldConfiguration.DepthFeatures &= DepthFeatures.Depth;

        var message = new ARSessionRunMessage
        {
          serializedWorldConfiguration = worldConfiguration,
          runOptions = options
        };

        _EasyConnection.Send(message);

        // Restore depth features so we can handle the other ones locally
        worldConfiguration.DepthFeatures = depthFeatures;
      }

      var handler = _onDidRun;
      if (handler != null)
        handler(new ARSessionRanArgs());
    }

    public void Pause()
    {
      if (State != ARSessionState.Running)
        return;

      State = ARSessionState.Paused;
      _EasyConnection.Send(new ARSessionPauseMessage());

      var handler = Paused;
      if (handler != null)
        handler(new ARSessionPausedArgs());
    }

    public IARAnchor AddAnchor(Matrix4x4 transform)
    {
      // TODO: Possibly the following will work when the other AddAnchor works.
      /*Guid identifier = Guid.NewGuid();
      var anchor = new _SerializableARBaseAnchor(transform, identifier, worldScale);
      AddAnchor(anchor);
      return anchor;*/

      throw new NotSupportedException();
    }

    public void RemoveAnchor(IARAnchor anchor)
    {
      throw new NotSupportedException();
      //_EasyConnection.Send(new ARSessionRemoveAnchorMessage { Anchor = anchor._AsSerializable() });
    }

    public AwarenessInitializationStatus GetAwarenessInitializationStatus
    (
      out AwarenessInitializationError error,
      out string errorMessage
    )
    {
      ARLog._Warn
      (
        "Checking the status of Awareness features in a Remote ARSession is not yet implemented. " +
        "Will always return a Ready status."
      );

      error = AwarenessInitializationError.None;
      errorMessage = string.Empty;

      return AwarenessInitializationStatus.Ready;
    }

    private void HandleDidUpdateFrame(ARSessionDidUpdateFrameMessage message)
    {
      var frame = message.Frame;
      UpdateGenerators(frame);

      _InvokeFrameUpdated(frame);
    }

    private void _InvokeFrameUpdated(IARFrame frame)
    {
      CurrentFrame = frame;

      var handler = FrameUpdated;
      if (handler != null)
      {
        var args = new FrameUpdatedArgs(frame);
        handler(args);
      }
    }

    // TODO: Pull depth point cloud generation into an extension so this code isn't duplicated
    // from _NativeARSession
    private void UpdateGenerators(IARFrame frame)
    {
      if (!(Configuration is IARWorldTrackingConfiguration worldConfig))
        return;

      var pointCloudsEnabled = (worldConfig.DepthFeatures & DepthFeatures.PointCloud) != 0;
      if (!pointCloudsEnabled)
        return;

      var depthBuffer = frame.Depth;
      if (depthBuffer == null || !depthBuffer.IsKeyframe)
        return;

      // Create a generator if needed
      if (_depthPointCloudGen == null)
      {
        _depthPointCloudGen =
          new DepthPointCloudGenerator
          (
            worldConfig.DepthPointCloudSettings,
            depthBuffer
          );
      }

      // Generate the point cloud
      var pointCloud = _depthPointCloudGen.GeneratePointCloud(depthBuffer);

      var frameBase = (_ARFrameBase)frame;
      frameBase.DepthFeaturePoints =
        new _SerializableARPointCloud
        (
          new ReadOnlyCollection<Vector3>(pointCloud),
          EmptyReadOnlyCollection<ulong>.Instance,
          _worldScale
        );
    }

    private void HandleDidAddAnchors(ARSessionDidAddAnchorsMessage message)
    {
      var anchors =
        new IARAnchor
        [
          message.Anchors.Length +
          message.PlaneAnchors.Length +
          message.ImageAnchors.Length
        ];

      var i = 0;
      foreach (var anchor in message.Anchors)
      {
        anchors[i] = anchor;
        i += 1;
      }

      foreach (var anchor in message.PlaneAnchors)
      {
        anchors[i] = anchor;
        i += 1;
      }

      foreach (var anchor in message.ImageAnchors)
      {
        anchors[i] = anchor;
        i += 1;
      }

      var handler = AnchorsAdded;
      if (handler != null)
      {
        var args = new AnchorsArgs(anchors);
        handler(args);
      }
    }

    private void HandleDidUpdateAnchors(ARSessionDidUpdateAnchorsMessage message)
    {
      var anchors =
        new IARAnchor
        [
          message.Anchors.Length +
          message.PlaneAnchors.Length +
          message.ImageAnchors.Length
        ];

      var i = 0;
      foreach (var anchor in message.Anchors)
      {
        anchors[i] = anchor;
        i += 1;
      }

      foreach (var anchor in message.PlaneAnchors)
      {
        anchors[i] = anchor;
        i += 1;
      }

      foreach (var anchor in message.ImageAnchors)
      {
        anchors[i] = anchor;
        i += 1;
      }

      var handler = AnchorsUpdated;
      if (handler != null)
      {
        var args = new AnchorsArgs(anchors);
        handler(args);
      }
    }

    private void HandleDidRemoveAnchors(ARSessionDidRemoveAnchorsMessage message)
    {
      var anchors =
        new IARAnchor
        [
          message.Anchors.Length +
          message.PlaneAnchors.Length +
          message.ImageAnchors.Length
        ];

      var i = 0;
      foreach (var anchor in message.Anchors)
      {
        anchors[i] = anchor;
        i += 1;
      }

      foreach (var anchor in message.PlaneAnchors)
      {
        anchors[i] = anchor;
        i += 1;
      }

      foreach (var anchor in message.ImageAnchors)
      {
        anchors[i] = anchor;
        i += 1;
      }

      var handler = AnchorsRemoved;
      if (handler != null)
      {
        var args = new AnchorsArgs(anchors);
        handler(args);
      }
    }

    private void HandleDidMergeAnchors(ARSessionDidMergeAnchorsMessage message)
    {
      IARAnchor parent = message.ParentAnchor;

      var handler = AnchorsMerged;
      if (handler != null)
      {
        var args = new AnchorsMergedArgs(parent, message.ChildAnchors);
        handler(args);
      }
    }

    private void HandleDidAddMaps(ARSessionDidAddMapsMessage message)
    {
      var handler = MapsAdded;
      if (handler != null)
      {
        var args = new MapsArgs(message.Maps);
        handler(args);
      }
    }

    private void HandleDidUpdateMaps(ARSessionDidUpdateMapsMessage message)
    {
      var handler = MapsUpdated;
      if (handler != null)
      {
        var args = new MapsArgs(message.Maps);
        handler(args);
      }
    }

    private void HandleDidUpdateMesh(ARSessionDidUpdateMeshMessage message)
    {
      if (MeshUpdated != null)
      {
        var args = new MeshUpdatedArgs(message.Mesh);
        MeshUpdated(args);
      }

      // ARSession.OnAnyDidUpdateMesh(this, message.Mesh, _isSilent);
    }

    private void HandleCameraDidChangeTrackingState
    (
      ARSessionCameraDidChangeTrackingStateMessage message
    )
    {
      var camera = message.Camera;

      var handler = CameraTrackingStateChanged;
      if (handler != null)
      {
        var args = new CameraTrackingStateChangedArgs(camera, camera.TrackingState);
        handler(args);
      }
    }

    private void HandleSessionWasInterrupted(ARSessionWasInterruptedMessage message)
    {
      var handler = SessionInterrupted;
      if (handler != null)
        handler(new ARSessionInterruptedArgs());
    }

    private void HandleSessionInterruptionEnded(ARSessionInterruptionEndedMessage message)
    {
      var handler = SessionInterruptionEnded;
      if (handler != null)
        handler(new ARSessionInterruptionEndedArgs());
    }

    private void HandleDidFailWithError(ARSessionDidFailWithError message)
    {
      var handler = SessionFailed;
      if (handler != null)
      {
        var args = new ARSessionFailedArgs(message.Error);
        handler(args);
      }
    }

    private ArdkEventHandler<ARSessionRanArgs> _onDidRun;
    public event ArdkEventHandler<ARSessionRanArgs> Ran
    {
      add
      {
        _onDidRun += value;

        if (State == ARSessionState.Running)
          value(new ARSessionRanArgs());
      }
      remove
      {
        _onDidRun -= value;
      }
    }

    public event ArdkEventHandler<ARSessionPausedArgs> Paused;
    public event ArdkEventHandler<ARSessionDeinitializedArgs> Deinitialized;

    public event ArdkEventHandler<ARSessionInterruptedArgs> SessionInterrupted;
    public event ArdkEventHandler<ARSessionInterruptionEndedArgs> SessionInterruptionEnded;
    public event ArdkEventHandler<ARSessionFailedArgs> SessionFailed;
    public event ArdkEventHandler<CameraTrackingStateChangedArgs> CameraTrackingStateChanged;

    public event ArdkEventHandler<FrameUpdatedArgs> FrameUpdated;
    public event ArdkEventHandler<MeshUpdatedArgs> MeshUpdated;
    public event ArdkEventHandler<AnchorsMergedArgs> AnchorsMerged;
    public event ArdkEventHandler<AnchorsArgs> AnchorsAdded;
    public event ArdkEventHandler<AnchorsArgs> AnchorsUpdated;
    public event ArdkEventHandler<AnchorsArgs> AnchorsRemoved;
    public event ArdkEventHandler<MapsArgs> MapsAdded;
    public event ArdkEventHandler<MapsArgs> MapsUpdated;

    ARInfoSource IARSession.ARInfoSource
    {
      get { return ARInfoSource.Remote; }
    }

    void IARSession.SetupLocationService(ILocationService locationService)
    {
      // Todo: figure out support
      throw new NotSupportedException("LocationService is not supported with Remote ARSessions.");
    }

    event ArdkEventHandler<QueryingShouldSessionAttemptRelocalizationArgs> IARSession.QueryingShouldSessionAttemptRelocalization
    {
      add { /* Do nothing. */ }
      remove { /* Do nothing. */}
    }
  }
}
