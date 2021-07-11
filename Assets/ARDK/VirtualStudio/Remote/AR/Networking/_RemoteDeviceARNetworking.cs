// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections.Generic;

using Niantic.ARDK.AR;
using Niantic.ARDK.AR.Networking;
using Niantic.ARDK.AR.Networking.ARNetworkingEventArgs;
using Niantic.ARDK.AR.Networking.NetworkAnchors;
using Niantic.ARDK.Networking;
using Niantic.ARDK.Utilities;
using Niantic.ARDK.Utilities.Collections;
using Niantic.ARDK.Utilities.Logging;
using Niantic.ARDK.Utilities.Marker;
using Niantic.ARDK.VirtualStudio.AR.Remote;
using Niantic.ARDK.VirtualStudio.Networking.Remote;
using Niantic.ARDK.VirtualStudio.Remote;
using Niantic.ARDK.VirtualStudio.Remote.Data;

using UnityEngine;

namespace Niantic.ARDK.VirtualStudio.AR.Networking.Remote
{
  /// <summary>
  /// An IARNetworking that handles player side logic on the remote connection and send data back to
  /// the editor
  /// </summary>
  internal sealed class _RemoteDeviceARNetworking:
    IARNetworking
  {
    /// <inheritdoc />
    public IMultipeerNetworking Networking { get; private set; }

    /// <inheritdoc />
    public IARSession ARSession { get; private set; }

    private readonly IARNetworking _arNetworking;

    /// <inheritdoc />
    public IReadOnlyDictionary<IPeer, Matrix4x4> LatestPeerPoses
    {
      get { return _arNetworking.LatestPeerPoses; }
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<IPeer, IARPeerAnchor> PeerAnchors
    {
      get { return _peerAnchors; }
    }

    private readonly _ReadOnlyDictionary<IPeer, IARPeerAnchor> _peerAnchors =
      new _ReadOnlyDictionary<IPeer, IARPeerAnchor>(new Dictionary<IPeer, IARPeerAnchor>());

    /// <inheritdoc />
    public IReadOnlyDictionary<SharedAnchorIdentifier, IARSharedAnchor> SharedAnchors
    {
      get { return _sharedAnchors; }
    }

    private readonly _ReadOnlyDictionary<SharedAnchorIdentifier, IARSharedAnchor> _sharedAnchors =
      new _ReadOnlyDictionary<SharedAnchorIdentifier, IARSharedAnchor>(new Dictionary<SharedAnchorIdentifier, IARSharedAnchor>());


    /// <inheritdoc />
    public PeerState LocalPeerState
    {
      get { return _arNetworking.LocalPeerState; }
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<IPeer, PeerState> LatestPeerStates
    {
      get { return _arNetworking.LatestPeerStates; }
    }

    internal _RemoteDeviceARNetworking(_RemoteDeviceARSession session, _RemoteDeviceMultipeerNetworking networking)
    {
      _FriendTypeAsserter.AssertCallerIs(typeof(_RemoteDeviceARNetworkingConstructor));

      _arNetworking =
        ARNetworkingFactory.Create(session.InnerARSession, networking.InnerNetworking);

      ARSession = session;
      Networking = networking;
      RegisterEventsAndMessages();
    }

    ~_RemoteDeviceARNetworking()
    {
      ARLog._Error("_RemoteDeviceARNetworking should be destroyed by an explicit call to Dispose().");
    }

    private void RegisterEventsAndMessages()
    {
      PeerStateReceived += HandlePeerStateReceived;
      PeerPoseReceived += HandlePeerPoseReceived;
      Deinitialized += HandleDeinitializing;

      _EasyConnection.Register<ARNetworkingDestroyMessage>(message => Dispose());
    }

    /// <inheritdoc />
    public void Dispose()
    {
      GC.SuppressFinalize(this);

      PeerStateReceived -= HandlePeerStateReceived;
      PeerPoseReceived -= HandlePeerPoseReceived;
      Deinitialized -= HandleDeinitializing;

      _EasyConnection.Unregister<ARNetworkingDestroyMessage>();

      _arNetworking?.Dispose();
    }

    /// <inheritdoc />
    public void EnablePoseBroadcasting()
    {
    }

    /// <inheritdoc />
    public void DisablePoseBroadcasting()
    {
    }

    /// <inheritdoc />
    public void SetTargetPoseLatency(Int64 targetPoseLatency)
    {
    }

    /// <inheritdoc />
    public void InitializeForMarkerScanning(Vector3[] markerPointLocations)
    {
      throw new NotImplementedException();
    }

    /// <inheritdoc />
    public void ScanForMarker
    (
      MarkerScanOption options,
      Action<MarkerMetadata> gotResult = null,
      IMarkerScanner scanner = null,
      IMetadataSerializer deserializer = null
    )
    {
      throw new NotImplementedException();
    }

    /// <inheritdoc />
    public bool CreateSharedAnchor(Matrix4x4 transform,  out SharedAnchorIdentifier identifier)
    {
      return _arNetworking.CreateSharedAnchor(transform, out identifier);
    }

    /// <inheritdoc />
    public event ArdkEventHandler<PeerStateReceivedArgs> PeerStateReceived
    {
      add
      {
        _arNetworking.PeerStateReceived += value;

        // We will sometimes get a local state update before our PlayerARNetworking is set up
        // In that case, send the state retroactively upon subscription
        if (_arNetworking.LocalPeerState != PeerState.Unknown)
        {
          var args =
            new PeerStateReceivedArgs(_arNetworking.Networking.Self, _arNetworking.LocalPeerState);

          value(args);
        }
      }
      remove
      {
        _arNetworking.PeerStateReceived -= value;
      }
    }

    /// <inheritdoc />
    public event ArdkEventHandler<PeerPoseReceivedArgs> PeerPoseReceived
    {
      add { _arNetworking.PeerPoseReceived += value; }
      remove { _arNetworking.PeerPoseReceived -= value; }
    }

    /// <inheritdoc />
    public event ArdkEventHandler<PeerAnchorUpdatedArgs> PeerAnchorUpdated
    {
      add { _arNetworking.PeerAnchorUpdated += value; }
      remove { _arNetworking.PeerAnchorUpdated -= value; }
    }

    /// <inheritdoc />
    public event ArdkEventHandler<SharedAnchorsArgs> SharedAnchorsTrackingUpdated
    {
      add { _arNetworking.SharedAnchorsTrackingUpdated += value; }
      remove { _arNetworking.SharedAnchorsTrackingUpdated -= value; }
    }

    /// <inheritdoc />
    public event ArdkEventHandler<SharedAnchorsArgs> SharedAnchorsUploaded
    {
      add { _arNetworking.SharedAnchorsUploaded += value; }
      remove { _arNetworking.SharedAnchorsUploaded -= value; }
    }


    /// <inheritdoc />
    public event ArdkEventHandler<ARNetworkingDeinitializedArgs> Deinitialized
    {
      add { _arNetworking.Deinitialized += value; }
      remove { _arNetworking.Deinitialized -= value; }
    }

    private void HandlePeerStateReceived(PeerStateReceivedArgs args)
    {
      var message =
        new ARNetworkingDidReceiveStateFromPeerMessage
        {
          PeerState = args.State,
          PeerIdentifier = args.Peer.Identifier
        };

      _EasyConnection.Send(message);
    }

    private void HandlePeerPoseReceived(PeerPoseReceivedArgs args)
    {
      var message =
        new ARNetworkingDidReceivePoseFromPeerMessage
        {
          PeerIdentifier = args.Peer.Identifier, Pose = args.Pose
        };

      _EasyConnection.Send(message);
    }

    private void HandleDeinitializing(ARNetworkingDeinitializedArgs args)
    {
      _EasyConnection.Send(new ARNetworkingWillDeInitializeMessage());
    }
  }
}
