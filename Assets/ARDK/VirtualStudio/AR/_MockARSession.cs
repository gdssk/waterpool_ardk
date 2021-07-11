// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using Niantic.ARDK.AR;
using Niantic.ARDK.AR.Anchors;
using Niantic.ARDK.AR.ARSessionEventArgs;
using Niantic.ARDK.AR.Awareness;
using Niantic.ARDK.AR.Configuration;
using Niantic.ARDK.AR.Depth;
using Niantic.ARDK.AR.Frame;
using Niantic.ARDK.AR.Networking;
using Niantic.ARDK.AR.SLAM;
using Niantic.ARDK.LocationService;
using Niantic.ARDK.Utilities;
using Niantic.ARDK.Utilities.Collections;
using Niantic.ARDK.Utilities.Logging;
using Niantic.ARDK.VirtualStudio.AR.Mock;

using UnityEngine;
using UnityEngine.Rendering;

namespace Niantic.ARDK.VirtualStudio.AR
{
  internal sealed class _MockARSession:
    _IMockARSession
  {
    private bool _isWorldTracking;
    private bool _isSharedExperience;

    private bool _anchorsDirty;
    private ReadOnlyCollection<IARAnchor> _cachedAnchors;
    private readonly Dictionary<Guid, IARAnchor> _anchors = new Dictionary<Guid, IARAnchor>();

    private readonly Dictionary<Guid, IARAnchor> _newAnchors = new Dictionary<Guid, IARAnchor>();
    private readonly Dictionary<Guid, IARAnchor> _updatedAnchors = new Dictionary<Guid, IARAnchor>();
    private readonly List<IARAnchor> _removedAnchors = new List<IARAnchor>();

    private bool _mapsDirty;
    private _SerializableARMap[] _cachedMaps = EmptyArray<_SerializableARMap>.Instance;
    private readonly Dictionary<Guid, IARMap> _maps = new Dictionary<Guid, IARMap>();

    private readonly Dictionary<Guid, IARMap> _localMaps =
      new Dictionary<Guid, IARMap>();

    // TODO: Should we use a HashSet here? Should we use _ReferenceComparer?
    private readonly HashSet<IARMap> _newMaps = new HashSet<IARMap>();

    private _IVirtualStudioManager _virtualStudioManager;

    private GameObject _cameraObject;
    private _MockFrameBufferProvider _frameProvider;
    private _MockCameraController _cameraController;

#if DEBUG
    private System.Diagnostics.StackTrace _stackTrace = new System.Diagnostics.StackTrace(true);
#endif

    public _ARConfigChangesCollector _ARConfigChangesCollector { get; }

    public ARSessionRunOptions RunOptions { get; private set; }

    internal _MockARSession(Guid stageIdentifier, _IVirtualStudioManager virtualStudioManager)
    {
      _FriendTypeAsserter.AssertCallerIs(typeof(ARSessionFactory));

      StageIdentifier = stageIdentifier;
      _virtualStudioManager = virtualStudioManager ?? _VirtualStudioManager.Instance;
      _ARConfigChangesCollector = new _ARConfigChangesCollector(this);
    }

    private bool _isDestroyed;
    public void Dispose()
    {
      if (_isDestroyed)
        return;

      _isDestroyed = true;

      if (State == ARSessionState.Running)
        Pause();

      var handler = Deinitialized;
      if (handler != null)
      {
        var args = new ARSessionDeinitializedArgs();
        handler(args);
      }

      CurrentFrame?.Dispose();
      CurrentFrame = null;

      if (_cameraObject != null)
      {
        if (Application.isEditor)
          GameObject.DestroyImmediate(_cameraObject);
        else
          GameObject.Destroy(_cameraObject);
      }

      _cameraController?.Dispose();
      _cameraController = null;

      _frameProvider?.Dispose();
      _frameProvider = null;
    }

    private float _worldScale = 1.0f;
    public float WorldScale
    {
      get { return _worldScale; }
      set { _worldScale = value; }
    }

    public IARConfiguration Configuration { get; private set; }
    public Guid StageIdentifier { get; private set; }
    public ARSessionState State { get; private set; }

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

    public void Run(IARConfiguration configuration, ARSessionRunOptions options = ARSessionRunOptions.None)
    {
      ARLog._DebugFormat("Running _MockARSession with options: {0}", false, options);

      _ARConfigChangesCollector._CollectChanges(configuration);

      if (!_ARConfigurationValidator.RunAllChecks(this, configuration))
        return;

      Configuration = configuration;
      RunOptions = options;

      if (configuration is IARWorldTrackingConfiguration worldConfiguration)
      {
        _isWorldTracking = true;
        _isSharedExperience = _isWorldTracking && worldConfiguration.IsSharedExperienceEnabled;

        if ((worldConfiguration.DepthFeatures & DepthFeatures.Depth) != 0)
          ARLog._Warn("Depth estimation is not supported in Virtual Studio's Mock Mode.");
      }

      if (_virtualStudioManager.LocalPlayer.ARSession == this && _cameraObject == null)
      {
        _cameraObject = new GameObject();
        _cameraObject.name = "ARDK_MockDeviceCamera";
        var camera = _cameraObject.AddComponent<Camera>();
        camera.cullingMask = LayerMask.GetMask("ARDK_MockWorld");

        _frameProvider = new _MockFrameBufferProvider(this, camera);
        _cameraController = new _MockCameraController(camera);
      }

      State = ARSessionState.Running;

      var handler = _onDidRun;
      if (handler != null)
        handler(new ARSessionRanArgs());
    }

    public void Pause()
    {
      if (State != ARSessionState.Running)
        return;

      State = ARSessionState.Paused;

      var handler = Paused;
      if (handler != null)
        handler(new ARSessionPausedArgs());
    }

    public bool AddAnchor(_SerializableARAnchor anchor)
    {
      if (State != ARSessionState.Running)
        return false;

      switch (anchor.AnchorType)
      {
        case AnchorType.Plane:
          var planeAnchor = (IARPlaneAnchor)anchor;
          if (!_IsPlaneAnchorDetectable(planeAnchor))
            return false;

          break;
      }

      _anchors.Add(anchor.Identifier, anchor);
      _newAnchors.Add(anchor.Identifier, anchor);
      _anchorsDirty = true;

      return true;
    }

    public IARAnchor AddAnchor(Matrix4x4 transform)
    {
      var anchor = _ARAnchorFactory._Create(transform);
      AddAnchor((_SerializableARAnchor) anchor);

      return anchor;
    }

    public void UpdateAnchor(IARAnchor anchor)
    {
      if (!_anchors.ContainsKey(anchor.Identifier))
      {
        ARLog._ErrorFormat("Tried to update anchor {0} that hadn't been added.", anchor.Identifier);
        return;
      }

      _anchors[anchor.Identifier] = anchor;
      _updatedAnchors[anchor.Identifier] = anchor;
    }

    public void MergeAnchors(IARAnchor parent, IARAnchor[] children)
    {
      // TODO: ecomas hook this up to MockPlaneAnchor
      bool foundAllAnchors = _anchors.ContainsKey(parent.Identifier);
      foreach(var child in children)
      {
        foundAllAnchors = foundAllAnchors & _anchors.ContainsKey(child.Identifier);
      }

      if (!foundAllAnchors)
      {
        ARLog._Warn("Cannot merge an anchor that was not added.");
        return;
      }

      var handler = AnchorsMerged;
      if (handler != null)
      {
        var args = new AnchorsMergedArgs(parent, children);
        handler(args);
      }
    }

    private bool _IsPlaneAnchorDetectable(IARPlaneAnchor planeAnchor)
    {
      if (!_isWorldTracking)
        return false;

      // See definition of either enum more information on why it has to be this way.
      var detectedPlanes = ((IARWorldTrackingConfiguration)Configuration).PlaneDetection;
      var thisPlane = (PlaneDetection) planeAnchor.Alignment;
      return (detectedPlanes & thisPlane) != 0;
    }

    public void RemoveAnchor(IARAnchor anchor)
    {
      if (State != ARSessionState.Running)
        return;

      if (!_anchors.ContainsKey(anchor.Identifier))
      {
        ARLog._Warn("Tried to remove an anchor that was not added.");
        return;
      }

      _anchors.Remove(anchor.Identifier);
      _anchorsDirty = true;

      _removedAnchors.Add(anchor);
    }

    public void UpdateFrame(IARFrame frame)
    {
      _UpdateCachedAnchorsAndMaps();

      var serializableFrame = frame._AsSerializable();
      serializableFrame.Anchors = _cachedAnchors;
      serializableFrame.Maps = _cachedMaps.AsNonNullReadOnly<IARMap>();

      CurrentFrame = serializableFrame;

      var frameUpdatedHandler = FrameUpdated;
      if (frameUpdatedHandler != null)
      {
        var args  = new FrameUpdatedArgs(serializableFrame);
        frameUpdatedHandler(args);
      }

      RaiseAnchorAndMapEvents();
    }

    private void _UpdateCachedAnchorsAndMaps()
    {
      if (_anchorsDirty)
      {
        _cachedAnchors = _anchors.Values.ToArray().AsNonNullReadOnly();
        _anchorsDirty = false;
      }

      if (_mapsDirty)
      {
        _cachedMaps = _maps.Values.ToArray()._AsSerializableArray();
        _mapsDirty = false;
      }
    }

    private void RaiseAnchorAndMapEvents()
    {
      if (_newAnchors.Count > 0)
      {
        var anchors = _newAnchors.Values.ToArray();

        var handler = AnchorsAdded;
        if (handler != null)
        {
          var args = new AnchorsArgs(anchors);
          handler(args);
        }

        _newAnchors.Clear();
      }

      if (_updatedAnchors.Count > 0)
      {
        var anchors = _updatedAnchors.Values.ToArray();

        var handler = AnchorsUpdated;
        if (handler != null)
        {
          var args = new AnchorsArgs(anchors);
          handler(args);
        }

        _updatedAnchors.Clear();
      }

      if (_removedAnchors.Count > 0)
      {
        var anchors = _removedAnchors.ToArray();

        var handler = AnchorsRemoved;
        if (handler != null)
        {
          var args = new AnchorsArgs(anchors);
          handler(args);
        }

        _removedAnchors.Clear();
      }

      if (_newMaps.Count > 0)
      {
        var mapsArray = _newMaps.ToArray();

        var mapsHandler = MapsAdded;
        if (mapsHandler != null)
        {
          var args = new MapsArgs(mapsArray);
          mapsHandler(args);
        }

        _newMaps.Clear();
      }
    }

    public bool CheckMapsUnion(IARSession otherSession)
    {
      var otherFrame = otherSession.CurrentFrame;
      if (otherFrame == null)
        return false;

      var otherMaps = new HashSet<IARMap>(otherFrame.Maps);

      foreach (var map in otherMaps)
        if (_localMaps.ContainsKey(map.Identifier))
          return true;

      return false;
    }

    private IARNetworking _GetPartnerARNetworking()
    {
      return _virtualStudioManager.ArNetworkingMediator.GetSession(StageIdentifier);
    }

    public void AddMap(IARMap map)
    {
      bool isHost = true;

      var arNetworking = _GetPartnerARNetworking();
      if (arNetworking != null)
        isHost = arNetworking.Networking.Host.Equals(arNetworking.Networking.Self);
      else
      {
        ARLog._Debug
        (
          "An ARMap is only supposed to be added to an ARSession that is part of an ARNetworking" +
          " session."
        );
      }

      if (!_isSharedExperience)
      {
        var message =
          "A map cannot be added to a session that is not running with SharedExperienceEnabled " +
          "set to true.";

        ARLog._Warn(message);
        return;
      }

      if (_maps.ContainsKey(map.Identifier))
      {
        var message =
          "Map (identifier {0}) already exists in this session. " +
          "If it needs to be updated use UpdateSerializedMap";

        ARLog._WarnFormat(message, objs: map.Identifier);
        return;
      }

      if (isHost)
      {
        _maps.Add(map.Identifier, map);
        _mapsDirty = true;

        // Can't invoke ImplDidAddMaps here because it needs to be invoked after the
        // frame is updated with the map. See ValidateCachedAnchorsAndMaps for more.
        // Todo: same logic for anchors
        _newMaps.Add(map);
      }
      else
      {
        _localMaps.Add(map.Identifier, map);

        var handler = ImplDidAddLocalMaps;
        if (handler != null)
          handler(new[] { map });
      }
    }

    public void UpdateMap(IARMap map)
    {
      if (_GetPartnerARNetworking() == null)
      {
        ARLog._Warn
        (
          "An ARMap can only be added for an ARSession that is part of an ARNetworking session."
        );

        return;
      }

      if (!_maps.ContainsKey(map.Identifier))
      {
        ARLog._WarnFormat
        (
          "Map (identifier {0}) does not exist in this session.",
          objs: map.Identifier
        );

        return;
      }

      _maps[map.Identifier] = map;
      _mapsDirty = true;

      var maps = _ArrayFromElement.Create(map);

      var handler = MapsUpdated;
      if (handler != null)
      {
        var args = new MapsArgs(maps);
        handler(args);
      }
    }

    public void UpdateMesh(IARMesh mesh)
    {
      if (MeshUpdated != null)
      {
        var args = new MeshUpdatedArgs(mesh);
        MeshUpdated(args);
      }

      // ARSession.OnAnyDidUpdateMesh(this, mesh, _isSilent);
    }

    public AwarenessInitializationStatus GetAwarenessInitializationStatus
    (
      out AwarenessInitializationError error,
      out string errorMessage
    )
    {
      error = AwarenessInitializationError.None;
      errorMessage = string.Empty;

      return AwarenessInitializationStatus.Ready;
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

    public event ArdkEventHandler<FrameUpdatedArgs> FrameUpdated;
    public event ArdkEventHandler<MeshUpdatedArgs> MeshUpdated;
    public event ArdkEventHandler<AnchorsMergedArgs> AnchorsMerged;
    public event ArdkEventHandler<AnchorsArgs> AnchorsAdded;
    public event ArdkEventHandler<AnchorsArgs> AnchorsUpdated;
    public event ArdkEventHandler<AnchorsArgs> AnchorsRemoved;
    public event ArdkEventHandler<MapsArgs> MapsAdded;
    public event ArdkEventHandler<MapsArgs> MapsUpdated;

    // This are internal events, so we don't care about the args standard.
    public event Action<IARMap[]> ImplDidAddLocalMaps;

#region Events Unused by Mock Implementation
      event ArdkEventHandler<ARSessionInterruptedArgs> IARSession.SessionInterrupted { add {} remove {} }
      event ArdkEventHandler<ARSessionInterruptionEndedArgs> IARSession.SessionInterruptionEnded { add {} remove {} }
      event ArdkEventHandler<QueryingShouldSessionAttemptRelocalizationArgs> IARSession.QueryingShouldSessionAttemptRelocalization { add {} remove {} }
      event ArdkEventHandler<ARSessionFailedArgs> IARSession.SessionFailed { add {} remove {} }

      event ArdkEventHandler<CameraTrackingStateChangedArgs> IARSession.CameraTrackingStateChanged
      {
        add {}
        remove {}
      }
#endregion Events Unused by Mock Implementation

    ARInfoSource IARSession.ARInfoSource
    {
      get { return ARInfoSource.Mock; }
    }

    void IARSession.SetupLocationService(ILocationService wrapper)
    {
      // Todo: figure out support
      throw new NotSupportedException("LocationService is not supported with Mock ARSessions.");
    }
  }
}
