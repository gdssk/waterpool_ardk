// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections.Generic;

using Niantic.ARDK.AR;
using Niantic.ARDK.AR.ARSessionEventArgs;
using Niantic.ARDK.AR.Networking;
using Niantic.ARDK.AR.Networking.ARNetworkingEventArgs;
using Niantic.ARDK.AR.Networking.NetworkAnchors;
using Niantic.ARDK.AR.SLAM;
using Niantic.ARDK.Networking;
using Niantic.ARDK.Networking.MultipeerNetworkingEventArgs;
using Niantic.ARDK.Utilities;
using Niantic.ARDK.Utilities.Collections;
using Niantic.ARDK.Utilities.Marker;
using Niantic.ARDK.VirtualStudio.Networking;

using UnityEngine;

namespace Niantic.ARDK.VirtualStudio.AR.Networking.Mock
{
  internal sealed class _MockARNetworking:
    IMockARNetworking
  {
    private readonly _IVirtualStudioManager _virtualStudioManager;

    private bool _isInitialized;
    private bool _isDisposed;

    // milliseconds
    private long _poseLatency = 17; // roughly 60 times per second
    private float _timeSinceLastPoseSend;

    private bool _isPoseBroadcastingEnabled = true;

    private readonly Dictionary<IPeer, PeerState> _cachedPeerStates =
      new Dictionary<IPeer, PeerState>();

    public _MockARNetworking
    (
      IARSession arSession,
      IMultipeerNetworking networking,
      _IVirtualStudioManager virtualStudioMaster
    )
    {
      ARSession = arSession;
      Networking = networking;
      _virtualStudioManager = virtualStudioMaster;

      arSession.MapsAdded += OnMapsAdded;

      // The arSession and networking arguments are dependably always _MockARSession and
      // _MockMultipeerNetworking instances, respectively, due to the checks in the
      // ARNetworkingFactory constructors. The only exception could be when this constructor is called
      // from a NSubstitute test.
      var mockARSession = _TryGetMockSession(arSession);
      if (mockARSession != null)
        mockARSession.ImplDidAddLocalMaps += CheckUnionWithHostMaps;

      networking.PeerAdded += OnPeerAdded;

      _readOnlyLatestPeerPoses = new _ReadOnlyDictionary<IPeer, Matrix4x4>(_latestPeerPoses);
      _readOnlyLatestPeerStates = new _ReadOnlyDictionary<IPeer, PeerState>(_latestPeerStates);

      Networking.PeerAdded += _HandleNetworkingAddedPeer;
      Networking.PeerRemoved += _HandleNetworkingRemovedPeer;
      Networking.Disconnecting += _HandleNetworkingDisconnected;

      Networking.Deinitialized += (_) => Dispose();
      ARSession.Deinitialized += (_) => Dispose();

      _isInitialized = true;

      ARNetworkingFactory.ARNetworkingInitialized += OnARNetworkingInitialized;
      ARNetworkingFactory.NonLocalARNetworkingInitialized += OnARNetworkingInitialized;
    }

    private void OnARNetworkingInitialized(AnyARNetworkingInitializedArgs args)
    {
      if (args.ARNetworking.ARSession.StageIdentifier == ARSession.StageIdentifier)
      {
        // This event subscription is here because if it's in the constructor, and
        // if the MultipeerNetworking is constructed and connected in the same frame as the
        // _MockARNetworking constructor, there's a nullref when the
        // _MockARNetworkingSessionsMediator tries to get connected sessions for this
        // ARNetworking because it hasn't finished initializing yet. So we do a post-initialization
        // subscription to avoid that.
        Networking.Connected += OnNetworkConnected;

        ARNetworkingFactory.ARNetworkingInitialized -= OnARNetworkingInitialized;
        ARNetworkingFactory.NonLocalARNetworkingInitialized -= OnARNetworkingInitialized;
      }
    }

    ~_MockARNetworking()
    {
      Debug.LogError
      (
        "MockARNetworking should be destroyed by an explicit call to Dispose()."
      );
    }

    /// <inheritdoc />
    public void Dispose()
    {
      if (_isDisposed)
        return;

      GC.SuppressFinalize(this);
      _isDisposed = true;

      if (_isInitialized)
      {
        var deinitializing = Deinitialized;
        if (deinitializing != null)
        {
          var args = new ARNetworkingDeinitializedArgs();
          deinitializing(args);
        }
      }

      if (ARSession != null)
        ARSession.Dispose();

      if (Networking != null)
        Networking.Dispose();
    }

    /// <inheritdoc />
    public IMultipeerNetworking Networking { get; private set; }

    /// <inheritdoc />
    public IARSession ARSession { get; private set; }

    /// <inheritdoc />
    public PeerState LocalPeerState { get; private set; }

    private readonly Dictionary<IPeer, Matrix4x4> _latestPeerPoses =
      new Dictionary<IPeer, Matrix4x4>();

    private _ReadOnlyDictionary<IPeer, Matrix4x4> _readOnlyLatestPeerPoses;

    /// <inheritdoc />
    public IReadOnlyDictionary<IPeer, Matrix4x4> LatestPeerPoses
    {
      get { return _readOnlyLatestPeerPoses; }
    }

    private readonly Dictionary<IPeer, PeerState> _latestPeerStates =
      new Dictionary<IPeer, PeerState>();

    private _ReadOnlyDictionary<IPeer, PeerState> _readOnlyLatestPeerStates;

    /// <inheritdoc />
    public IReadOnlyDictionary<IPeer, PeerState> LatestPeerStates
    {
      get { return _readOnlyLatestPeerStates; }
    }

    /// <inheritdoc />
    public void EnablePoseBroadcasting()
    {
      _isPoseBroadcastingEnabled = true;
    }

    /// <inheritdoc />
    public void DisablePoseBroadcasting()
    {
      _isPoseBroadcastingEnabled = false;
    }

    /// <inheritdoc />
    public void SetTargetPoseLatency(long targetPoseLatency)
    {
      _poseLatency = targetPoseLatency;
    }

    private void OnNetworkConnected(ConnectedArgs args)
    {
      Networking.Connected -= OnNetworkConnected;

      _BroadcastState(PeerState.WaitingForLocalizationData);
    }

    private void _HandleNetworkingAddedPeer(PeerAddedArgs args)
    {
      var peer = args.Peer;
      _latestPeerPoses.Add(peer, Matrix4x4.identity);
      _latestPeerStates[peer] = PeerState.Unknown;

      if (_cachedPeerStates.ContainsKey(peer))
      {
        _ReceiveStateFromPeer(_cachedPeerStates[peer], peer);
        _cachedPeerStates.Remove(peer);
      }
    }

    private void _HandleNetworkingRemovedPeer(PeerRemovedArgs args)
    {
      var peer = args.Peer;
      if (peer.Equals(Networking.Self))
      {
        LocalPeerState = PeerState.Unknown;
        _latestPeerPoses.Clear();
        _latestPeerStates.Clear();
      }
      else
      {
        _latestPeerPoses.Remove(peer);
        _latestPeerStates.Remove(peer);
      }
    }

    private void _HandleNetworkingDisconnected(DisconnectingArgs args)
    {
      LocalPeerState = PeerState.Unknown;
      _latestPeerPoses.Clear();
    }

    public void BroadcastPose(Matrix4x4 pose, float deltaTime)
    {
      if (!_isPoseBroadcastingEnabled)
        return;

      _timeSinceLastPoseSend += deltaTime * 1000f;
      if (_timeSinceLastPoseSend < _poseLatency)
        return;

      var mediator = _virtualStudioManager.ArNetworkingMediator;
      var receivers = mediator.GetConnectedSessions(Networking.StageIdentifier);

      foreach (var receiver in receivers)
      {
        // Skip broadcasting to self
        if (receiver.Networking.StageIdentifier == Networking.StageIdentifier)
          continue;

        var mockReceiver = receiver as _MockARNetworking;
        if (mockReceiver != null)
          mockReceiver._ReceivePoseFromPeer(pose, Networking.Self);
      }

      _timeSinceLastPoseSend = 0;
    }

    private void _ReceivePoseFromPeer(Matrix4x4 pose, IPeer peer)
    {
      if (!_latestPeerPoses.ContainsKey(peer))
      {
        Debug.LogWarningFormat
        (
          "ARNetworking {0} received pose from an invalid peer {1}.",
          Networking.StageIdentifier,
          peer
        );
        return;
      }

      _latestPeerPoses[peer] = pose;

      var peerPoseReceived = PeerPoseReceived;
      if (peerPoseReceived != null)
      {
        var args = new PeerPoseReceivedArgs(peer, pose);
        peerPoseReceived(args);
      }
    }

    private void _ReceiveStateFromPeer(PeerState state, IPeer peer)
    {
      // Don't raise the ImplDidReceiveStateFromPeer callback until the DidAddPeer callback
      // has been raised for the same peer. This is required due to how the
      // _MockNetworkingCommandsRouter.Join method works.
      if (!_latestPeerStates.ContainsKey(peer))
      {
        _cachedPeerStates.Add(peer, state);
        return;
      }

      _latestPeerStates[peer] = state;

      if (peer.Equals(Networking.Self))
        LocalPeerState = state;

      var peerStateReceived = _peerStateReceived;
      if (peerStateReceived != null)
      {
        var args = new PeerStateReceivedArgs(peer, state);
        peerStateReceived(args);
      }

      // Don't need to do anything else with self updates
      if (peer.Equals(Networking.Self))
        return;

      // Only a host's state changing to Stable is relevant
      if (peer.Equals(Networking.Host) && state == PeerState.Stable)
      {
        _BroadcastState(PeerState.Localizing);

        // Todo: Implement a delay here. If maps were already found the local peer state will
        //   go immediately from Localizing to Stable
        CheckUnionWithHostMaps();
      }
    }

    private void CheckUnionWithHostMaps()
    {
      if (ARSession.CurrentFrame == null)
        return;

      var mockArSession = _TryGetMockSession(ARSession);
      if (mockArSession == null)
        return;

      var hostPlayer = _virtualStudioManager.GetPlayerWithPeer(Networking.Host);
      var foundUnion = mockArSession.CheckMapsUnion(hostPlayer.ARSession);

      if (foundUnion)
        _BroadcastState(PeerState.Stable);
    }

    private static _IMockARSession _TryGetMockSession(IARSession session)
    {
      if (session.ARInfoSource != ARInfoSource.Mock)
        return null;

      return (_IMockARSession) session;
    }

    private void CheckUnionWithHostMaps(IARMap[] localMaps)
    {
      CheckUnionWithHostMaps();
    }

    private void OnPeerAdded(PeerAddedArgs args)
    {
      var addedPeer = args.Peer;
      _latestPeerStates.Add(addedPeer, PeerState.Unknown);
    }

    private void OnMapsAdded(MapsArgs args)
    {
      if (!Networking.IsConnected)
        Debug.LogWarning("Can not add maps before the ARNetworking session has connected.");
      else if (Networking.Self.Equals(Networking.Host))
        _BroadcastState(PeerState.Stable);
    }

    private void _BroadcastState(PeerState state)
    {
      PeerState currState;
      if (_latestPeerStates.TryGetValue(Networking.Self, out currState) && currState == state)
        return;

      _latestPeerStates[Networking.Self] = state;

      var mediator = _virtualStudioManager.ArNetworkingMediator;
      var receivers = mediator.GetConnectedSessions(Networking.StageIdentifier);

      foreach (var receiver in receivers)
      {
        var mockReceiver = receiver as _MockARNetworking;
        if (mockReceiver != null)
          mockReceiver._ReceiveStateFromPeer(state, Networking.Self);
      }
    }

    private ArdkEventHandler<PeerStateReceivedArgs> _peerStateReceived;

    /// <inheritdoc />
    public event ArdkEventHandler<PeerStateReceivedArgs> PeerStateReceived
    {
      add
      {
        _peerStateReceived += value;

        foreach (var pair in _latestPeerStates)
        {
          var args = new PeerStateReceivedArgs(pair.Key, pair.Value);
          value(args);
        }
      }
      remove
      {
        _peerStateReceived -= value;
      }
    }

    /// <inheritdoc />
    public event ArdkEventHandler<PeerPoseReceivedArgs> PeerPoseReceived;

    /// <inheritdoc />
    public event ArdkEventHandler<ARNetworkingDeinitializedArgs> Deinitialized;

    // Explicit Interface Implementations
    IReadOnlyDictionary<IPeer, IARPeerAnchor> IARNetworking.PeerAnchors
    {
      get { return _EmptyReadOnlyDictionary<IPeer, IARPeerAnchor>.Instance; }
    }

    IReadOnlyDictionary<SharedAnchorIdentifier, IARSharedAnchor> IARNetworking.SharedAnchors
    {
      get { return _EmptyReadOnlyDictionary<SharedAnchorIdentifier, IARSharedAnchor>.Instance; }
    }

    /// <inheritdoc />
    void IARNetworking.InitializeForMarkerScanning(Vector3[] markerPointLocations)
    {
      throw new NotSupportedException();
    }

    /// <inheritdoc />
    void IARNetworking.ScanForMarker
    (
      MarkerScanOption options,
      Action<MarkerMetadata> gotResult,
      IMarkerScanner scanner,
      IMetadataSerializer deserializer
    )
    {
      throw new NotSupportedException();
    }

    bool IARNetworking.CreateSharedAnchor(Matrix4x4 transform, out SharedAnchorIdentifier identifier)
    {
      throw new NotSupportedException();
    }

    event ArdkEventHandler<PeerAnchorUpdatedArgs> IARNetworking.PeerAnchorUpdated
    {
      add { /* Do nothing. */ }
      remove { /* Do nothing. */ }
    }

    event ArdkEventHandler<SharedAnchorsArgs> IARNetworking.SharedAnchorsTrackingUpdated
    {
      add { /* Do nothing. */ }
      remove { /* Do nothing. */ }
    }

    event ArdkEventHandler<SharedAnchorsArgs> IARNetworking.SharedAnchorsUploaded
    {
      add { /* Do nothing. */ }
      remove { /* Do nothing. */ }
    }
  }
}
