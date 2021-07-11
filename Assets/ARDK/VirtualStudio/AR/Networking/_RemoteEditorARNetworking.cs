// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections.Generic;

using Niantic.ARDK.AR;
using Niantic.ARDK.AR.Networking;
using Niantic.ARDK.AR.Networking.ARNetworkingEventArgs;
using Niantic.ARDK.AR.Networking.NetworkAnchors;
using Niantic.ARDK.Networking;
using Niantic.ARDK.Networking.MultipeerNetworkingEventArgs;
using Niantic.ARDK.Utilities;
using Niantic.ARDK.Utilities.Collections;
using Niantic.ARDK.Utilities.Logging;
using Niantic.ARDK.Utilities.Marker;
using Niantic.ARDK.VirtualStudio.Networking;
using Niantic.ARDK.VirtualStudio.Remote;
using Niantic.ARDK.VirtualStudio.Remote.Data;

using UnityEngine;

namespace Niantic.ARDK.VirtualStudio.AR.Networking.Remote
{
  /// <summary>
  /// AR networking that will communicate with a ARDK's remote application.
  /// </summary>
  internal sealed class _RemoteEditorARNetworking:
    IARNetworking
  {
    private bool _isInitialized;
    private bool _isDisposed;

    internal _RemoteEditorARNetworking(IARSession arSession, IMultipeerNetworking networking)
    {
      ARSession = arSession;
      Networking = networking;

      _RegisterMessageHandlers();
      _EasyConnection.Send
      (
        new ARNetworkingInitMessage
        {
          StageIdentifier = arSession.StageIdentifier,
          ConstructFromExistingNetworking = true
        }
      );

      _readOnlyLatestPeerPoses = new _ReadOnlyDictionary<IPeer, Matrix4x4>(_latestPeerPoses);
      _readOnlyLatestPeerStates = new _ReadOnlyDictionary<IPeer, PeerState>(_latestPeerStates);

      Networking.PeerAdded += _HandleNetworkingAddedPeer;
      Networking.PeerRemoved += _HandleNetworkingRemovedPeer;
      Networking.Disconnecting += _HandleNetworkingDisconnected;

      Networking.Deinitialized += (_) => Dispose();
      ARSession.Deinitialized += (_) => Dispose();

      _isInitialized = true;
    }

    ~_RemoteEditorARNetworking()
    {
      ARLog._Error
      (
        "_RemoteEditorARNetworking should be destroyed by an explicit call to Dispose()."
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

      if (_RemoteConnection.IsConnected)
        _EasyConnection.Send(new ARNetworkingDestroyMessage());
      else
        _HandleNetworkingAboutToBeDestroyed(null);

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

    private void _RegisterMessageHandlers()
    {
      _EasyConnection.Register<ARNetworkingDidReceiveStateFromPeerMessage>
      (
        _HandleDidReceiveStateFromPeerMessage
      );

      _EasyConnection.Register<ARNetworkingDidReceivePoseFromPeerMessage>
      (
        _HandleDidReceivePoseFromPeerMessage
      );

      _EasyConnection.Register<ARNetworkingWillDeInitializeMessage>
      (
        _HandleNetworkingAboutToBeDestroyed
      );
    }

    private void _HandleDidReceiveStateFromPeerMessage
    (
      ARNetworkingDidReceiveStateFromPeerMessage message
    )
    {
      var peer = _Peer.FromIdentifier(message.PeerIdentifier);
      var state = message.PeerState;

      _latestPeerStates[peer] = state;

      if (peer.Equals(Networking.Self))
        LocalPeerState = state;

      var peerStateReceived = _peerStateReceived;
      if (peerStateReceived != null)
      {
        var args = new PeerStateReceivedArgs(peer, state);
        peerStateReceived(args);
      }
    }

    private void _HandleDidReceivePoseFromPeerMessage
    (
      ARNetworkingDidReceivePoseFromPeerMessage message
    )
    {
      var peer = _Peer.FromIdentifier(message.PeerIdentifier);
      var pose = message.Pose;

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

    private void _HandleNetworkingAboutToBeDestroyed(ARNetworkingWillDeInitializeMessage message)
    {
      _EasyConnection.Unregister<ARNetworkingWillDeInitializeMessage>();
      Dispose();
    }

    private void _HandleNetworkingAddedPeer(PeerAddedArgs args)
    {
      _latestPeerPoses.Add(args.Peer, Matrix4x4.identity);
      _latestPeerStates.Add(args.Peer, PeerState.Unknown);
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

    /// <inheritdoc />
    public event ArdkEventHandler<ARNetworkingDeinitializedArgs> Deinitialized;

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

    // Explicit Interface Implementations - Effectively NotSupported methods.
    IReadOnlyDictionary<IPeer, IARPeerAnchor> IARNetworking.PeerAnchors
    {
      get { return _EmptyReadOnlyDictionary<IPeer, IARPeerAnchor>.Instance; }
    }

    IReadOnlyDictionary<SharedAnchorIdentifier, IARSharedAnchor> IARNetworking.SharedAnchors
    {
      get { return _EmptyReadOnlyDictionary<SharedAnchorIdentifier, IARSharedAnchor>.Instance; }
    }

    void IARNetworking.EnablePoseBroadcasting()
    {
      throw new NotSupportedException();
    }

    void IARNetworking.DisablePoseBroadcasting()
    {
      throw new NotSupportedException();
    }

    void IARNetworking.SetTargetPoseLatency(long targetPoseLatency)
    {
      throw new NotSupportedException();
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

    bool IARNetworking.CreateSharedAnchor
    (
      Matrix4x4 transform,
      out SharedAnchorIdentifier identifier
    )
    {
      throw new NotSupportedException();
    }

    event ArdkEventHandler<SharedAnchorsArgs> IARNetworking.SharedAnchorsUploaded
    {
      add { /* Do nothing. */ }
      remove { /* Do nothing. */ }

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
  }
}
