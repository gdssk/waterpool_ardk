// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections.Generic;

using Niantic.ARDK.AR;
using Niantic.ARDK.Networking;
using Niantic.ARDK.Networking.MultipeerNetworkingEventArgs;
using Niantic.ARDK.Utilities;
using Niantic.ARDK.Utilities.Extensions;
using Niantic.ARDK.Utilities.Collections;
using Niantic.ARDK.Networking.Clock;
using Niantic.ARDK.Utilities.Logging;
using Niantic.ARDK.VirtualStudio.Remote;
using Niantic.ARDK.VirtualStudio.Remote.Data;

using UnityEngine;
using UnityEngine.Networking.PlayerConnection;

namespace Niantic.ARDK.VirtualStudio.Networking.Remote
{
  /// <summary>
  /// <see cref="IMultipeerNetworking"/> That will run on the player and handle player side logic for a remote
  /// player.
  /// </summary>
  internal sealed class _RemoteDeviceMultipeerNetworking:
    IMultipeerNetworking
  {
    private readonly IMultipeerNetworking _networking;

    /// <inheritdoc />
    public Guid StageIdentifier
    {
      get { return _networking.StageIdentifier; }
    }

    /// <inheritdoc />
    public bool IsConnected
    {
      get { return _networking.IsConnected; }
    }

    /// <inheritdoc />
    public IPeer Self
    {
      get { return _networking.Self; }
    }

    /// <inheritdoc />
    public IPeer Host
    {
      get { return _networking.Host; }
    }

    public ICoordinatedClock CoordinatedClock
    {
      get { return _networking.CoordinatedClock; }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IPeer> OtherPeers
    {
      get { return _networking.OtherPeers; }
    }

    internal IMultipeerNetworking InnerNetworking
    {
      get { return _networking; }
    }

    /// <inheritdoc />
    public void SendDataToPeer
    (
      uint tag,
      byte[] data,
      IPeer peer,
      TransportType transportType,
      bool sendToSelf = false
    )
    {
      _networking.SendDataToPeer(tag, data, peer, transportType, sendToSelf);
    }

    /// <inheritdoc />
    public void SendDataToPeers
    (
      uint tag,
      byte[] data,
      IEnumerable<IPeer> peers,
      TransportType transportType,
      bool sendToSelf = false
    )
    {
      _networking.SendDataToPeers(tag, data, peers, transportType, sendToSelf);
    }

    /// <inheritdoc />
    public void BroadcastData
    (
      uint tag,
      byte[] data,
      TransportType transportType,
      bool sendToSelf = false
    )
    {
      _networking.BroadcastData(tag, data, transportType, sendToSelf);
    }

    /// <inheritdoc />
    public void StorePersistentKeyValue(string key, byte[] value)
    {
      _networking.StorePersistentKeyValue(key, value);
    }

    public void SendDataToArm(uint tag, byte[] data)
    {
      _networking.SendDataToArm(tag, data);
    }

    /// <inheritdoc />
    public void Join(byte[] metadata, byte[] token = null, Int64 timestamp = 0)
    {
      _networking.Join(metadata, token, timestamp);
    }

    /// <inheritdoc />
    public void Leave()
    {
      _networking.Leave();
    }

    /// <inheritdoc />
    public void Dispose()
    {
      GC.SuppressFinalize(this);

      Connected -= OnInternalConnected;
      ConnectionFailed -= OnInternalConnectionFailed;
      Disconnecting -= OnInternalDisconnecting;
      PeerDataReceived -= OnInternalPeerDataReceived;
      PeerAdded -= OnInternalPeerAdded;
      PeerRemoved -= OnInternalPeerRemoved;
      Deinitialized -= OnInternalDeinitializing;

      var stageId = StageIdentifier;
      _RemoteConnection.Unregister
      (
        NetworkingJoinMessage.ID.Combine(stageId),
        HandleJoinMessage
      );

      _RemoteConnection.Unregister
      (
        NetworkingLeaveMessage.ID.Combine(stageId),
        HandleLeaveMessage
      );

      _RemoteConnection.Unregister
      (
        NetworkingSendDataToPeersMessage.ID.Combine(stageId),
        HandleSendDataToPeersMessage
      );

      _RemoteConnection.Unregister
      (
        NetworkingDestroyMessage.ID.Combine(stageId),
        HandleDestroyMessage
      );

      _RemoteConnection.Unregister
      (
        NetworkingStorePersistentKeyValueMessage.ID.Combine(stageId),
        HandleStorePersistentKeyValueMessage
      );

      _networking.Dispose();
    }

    /// <inheritdoc />
    public override string ToString()
    {
      return string.Format("StageID: {0}", StageIdentifier);
    }

    /// <inheritdoc />
    public string ToString(int count)
    {
      return string.Format("StageID: {0}", StageIdentifier.ToString().Substring(0, count));
    }

    internal _RemoteDeviceMultipeerNetworking(ServerConfiguration configuration, Guid stageIdentifier)
    {
      _FriendTypeAsserter.AssertCallerIs(typeof(_RemoteDeviceMultipeerNetworkingConstructor));

      _networking =
        MultipeerNetworkingFactory.Create(configuration, stageIdentifier);

      Connected += OnInternalConnected;
      ConnectionFailed += OnInternalConnectionFailed;
      Disconnecting += OnInternalDisconnecting;
      PeerDataReceived += OnInternalPeerDataReceived;
      PeerAdded += OnInternalPeerAdded;
      PeerRemoved += OnInternalPeerRemoved;
      Deinitialized += OnInternalDeinitializing;
      PersistentKeyValueUpdated += OnInternalPersistentKeyValueUpdated;
      DataReceivedFromArm += OnInternalDidReceiveDataFromArm;
      SessionStatusReceivedFromArm += OnInternalDidReceiveStatusFromArm;
      SessionResultReceivedFromArm += OnInternalDidReceiveResultFromArm;

      _RemoteConnection.Register
      (
        NetworkingJoinMessage.ID.Combine(stageIdentifier),
        HandleJoinMessage
      );

      _RemoteConnection.Register
      (
        NetworkingLeaveMessage.ID.Combine(stageIdentifier),
        HandleLeaveMessage
      );

      _RemoteConnection.Register
      (
        NetworkingDestroyMessage.ID.Combine(stageIdentifier),
        HandleDestroyMessage
      );

      _RemoteConnection.Register
      (
        NetworkingSendDataToPeersMessage.ID.Combine(stageIdentifier),
        HandleSendDataToPeersMessage
      );

      _RemoteConnection.Register
      (
        NetworkingStorePersistentKeyValueMessage.ID.Combine(stageIdentifier),
        HandleStorePersistentKeyValueMessage
      );

      _RemoteConnection.Register
      (
        NetworkingSendDataToArmMessage.ID.Combine(stageIdentifier),
        HandleSendDataToArmMessage
      );
    }

    ~_RemoteDeviceMultipeerNetworking()
    {
      ARLog._Error("_RemoteDeviceMultipeerNetworking should be destroyed by an explicit call to Dispose().");
    }

    private void OnInternalConnected(ConnectedArgs args)
    {
      _RemoteConnection.Send
      (
        NetworkingDidConnectMessage.ID.Combine(StageIdentifier),
        new NetworkingDidConnectMessage
        {
          SelfIdentifier = args.Self.Identifier, HostIdentifier = args.Host.Identifier,
        }.SerializeToArray()
      );
    }

    private void OnInternalConnectionFailed(ConnectionFailedArgs args)
    {
      _RemoteConnection.Send
      (
        NetworkingConnectionDidFailWithErrorMessage.ID.Combine(StageIdentifier),
        new NetworkingConnectionDidFailWithErrorMessage
        {
          ErrorCode = args.ErrorCode,
        }.SerializeToArray()
      );
    }

    private void OnInternalDisconnecting(DisconnectingArgs args)
    {
      _RemoteConnection.Send
      (
        NetworkingWillDisconnectMessage.ID.Combine(StageIdentifier),
        new NetworkingWillDisconnectMessage().SerializeToArray()
      );
    }

    private void OnInternalPeerDataReceived(PeerDataReceivedArgs args)
    {
      _RemoteConnection.Send
      (
        NetworkingDidReceiveDataFromPeerMessage.ID.Combine(StageIdentifier),
        new NetworkingDidReceiveDataFromPeerMessage
        {
          Tag = args.Tag,
          Data = args.CopyData(),
          PeerIdentifier = args.Peer.Identifier,
          TransportType = (byte)args.TransportType
        }.SerializeToArray()
      );
    }

    private void OnInternalPeerAdded(PeerAddedArgs args)
    {
      _RemoteConnection.Send
      (
        NetworkingDidAddPeerMessage.ID.Combine(StageIdentifier),
        new NetworkingDidAddPeerMessage
          {
            PeerIdentifier = args.Peer.Identifier
          }
          .SerializeToArray()
      );
    }

    private void OnInternalPeerRemoved(PeerRemovedArgs args)
    {
      _RemoteConnection.Send
      (
        NetworkingDidRemovePeerMessage.ID.Combine(StageIdentifier),
        new NetworkingDidRemovePeerMessage
          {
            PeerIdentifier = args.Peer.Identifier
          }
          .SerializeToArray()
      );
    }

    private void OnInternalDeinitializing(DeinitializedArgs args)
    {
      _RemoteConnection.Send
      (
        NetworkingWillDeInitializeMessage.ID.Combine(StageIdentifier),
        new NetworkingWillDeInitializeMessage().SerializeToArray()
      );
    }

    private void OnInternalPersistentKeyValueUpdated(PersistentKeyValueUpdatedArgs args)
    {
      _RemoteConnection.Send
      (
        NetworkingDidUpdatePersistentKeyValueMessage.ID.Combine(StageIdentifier),
        new NetworkingDidUpdatePersistentKeyValueMessage()
        {
          Key = System.Text.Encoding.UTF8.GetBytes(args.Key), Value = args.CopyValue()
        }.SerializeToArray()
      );
    }

    private void OnInternalDidReceiveDataFromArm(DataReceivedFromArmArgs args)
    {
      _RemoteConnection.Send
      (
        NetworkingDidReceiveDataFromArmMessage.ID.Combine(StageIdentifier),
        new NetworkingDidReceiveDataFromArmMessage
        {
          Tag = args.Tag,
          Data = args.CreateDataReader().ToArray(),
        }.SerializeToArray()
      );
    }

    private void OnInternalDidReceiveStatusFromArm(SessionStatusReceivedFromArmArgs args)
    {
      _RemoteConnection.Send
      (
        NetworkingDidReceiveStatusFromArmMessage.ID.Combine(StageIdentifier),
        new NetworkingDidReceiveStatusFromArmMessage
        {
          Status = args.Status,
        }.SerializeToArray()
      );
    }

    private void OnInternalDidReceiveResultFromArm(SessionResultReceivedFromArmArgs args)
    {
      _RemoteConnection.Send
      (
        NetworkingDidReceiveResultFromArmMessage.ID.Combine(StageIdentifier),
        new NetworkingDidReceiveResultFromArmMessage
        {
          Outcome = args.Outcome,
          Details = args.CreateDetailsReader().ToArray(),
        }.SerializeToArray()
      );
    }

    /// <inheritdoc />
    public event ArdkEventHandler<ConnectedArgs> Connected
    {
      add { _networking.Connected += value; }
      remove { _networking.Connected -= value; }
    }

    /// <inheritdoc />
    public event ArdkEventHandler<ConnectionFailedArgs> ConnectionFailed
    {
      add { _networking.ConnectionFailed += value; }
      remove { _networking.ConnectionFailed -= value; }
    }

    /// <inheritdoc />
    public event ArdkEventHandler<DisconnectingArgs> Disconnecting
    {
      add { _networking.Disconnecting += value; }
      remove { _networking.Disconnecting -= value; }
    }

    /// <inheritdoc />
    public event ArdkEventHandler<PeerDataReceivedArgs> PeerDataReceived
    {
      add { _networking.PeerDataReceived += value; }
      remove { _networking.PeerDataReceived -= value; }
    }

    /// <inheritdoc />
    public event ArdkEventHandler<PeerAddedArgs> PeerAdded
    {
      add { _networking.PeerAdded += value; }
      remove { _networking.PeerAdded -= value; }
    }

    /// <inheritdoc />
    public event ArdkEventHandler<PeerRemovedArgs> PeerRemoved
    {
      add { _networking.PeerRemoved += value; }
      remove { _networking.PeerRemoved -= value; }
    }

    /// <inheritdoc />
    public event ArdkEventHandler<PersistentKeyValueUpdatedArgs> PersistentKeyValueUpdated
    {
      add { _networking.PersistentKeyValueUpdated += value; }
      remove { _networking.PersistentKeyValueUpdated -= value; }
    }

    /// <inheritdoc />
    public event ArdkEventHandler<DeinitializedArgs> Deinitialized
    {
      add { _networking.Deinitialized += value; }
      remove { _networking.Deinitialized -= value; }
    }

#pragma warning disable CS0067
    public event ArdkEventHandler<DataReceivedFromArmArgs> DataReceivedFromArm
    {
      add
      {
        _networking.DataReceivedFromArm += value;
      }
      remove
      {
        _networking.DataReceivedFromArm -= value;
      }
    }

    public event ArdkEventHandler<SessionStatusReceivedFromArmArgs> SessionStatusReceivedFromArm
    {
      add
      {
        _networking.SessionStatusReceivedFromArm += value;
      }
      remove
      {
        _networking.SessionStatusReceivedFromArm -= value;
      }
    }

    public event ArdkEventHandler<SessionResultReceivedFromArmArgs> SessionResultReceivedFromArm
    {
      add
      {
        _networking.SessionResultReceivedFromArm += value;
      }
      remove
      {
        _networking.SessionResultReceivedFromArm -= value;
      }
    }
#pragma warning restore CS0067

    private void HandleJoinMessage(MessageEventArgs e)
    {
      var message = e.data.DeserializeFromArray<NetworkingJoinMessage>();

      Join(message.Metadata);
    }

    private void HandleLeaveMessage(MessageEventArgs e)
    {
      Leave();
    }

    private void HandleSendDataToPeersMessage(MessageEventArgs e)
    {
      var message = e.data.DeserializeFromArray<NetworkingSendDataToPeersMessage>();
      var peers = new IPeer[message.Peers.Length];

      for (var i = 0; i < message.Peers.Length; i++)
        peers[i] = _Peer.FromIdentifier(message.Peers[i]);

      SendDataToPeers
      (
        message.Tag,
        message.Data,
        peers,
        (TransportType)message.TransportType,
        message.SendToSelf
      );
    }

    private void HandleStorePersistentKeyValueMessage(MessageEventArgs e)
    {
      var message = e.data.DeserializeFromArray<NetworkingStorePersistentKeyValueMessage>();

      var key = System.Text.Encoding.UTF8.GetString(message.Key);
      var value = message.Value;

      StorePersistentKeyValue(key, value);
    }

    private void HandleSendDataToArmMessage(MessageEventArgs e)
    {
      var message = e.data.DeserializeFromArray<NetworkingSendDataToArmMessage>();

      SendDataToArm(message.Tag, message.Data);
    }

    private void HandleDestroyMessage(MessageEventArgs e)
    {
      Dispose();
    }

    ARInfoSource IMultipeerNetworking.ARInfoSource
    {
      get { return ARInfoSource.Remote; }
    }
  }
}
