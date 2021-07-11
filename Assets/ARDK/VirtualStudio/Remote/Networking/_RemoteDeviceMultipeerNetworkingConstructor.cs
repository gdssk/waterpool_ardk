// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using Niantic.ARDK.Networking;
using Niantic.ARDK.Networking.MultipeerNetworkingEventArgs;
using Niantic.ARDK.Utilities.Collections;
using Niantic.ARDK.VirtualStudio.Remote;
using Niantic.ARDK.VirtualStudio.Remote.Data;

namespace Niantic.ARDK.VirtualStudio.Networking.Remote
{
  /// <summary>
  /// A static wrapper class for listening for messages from the editor to create a parallel
  /// _RemoteDeviceMultipeerNetworking running on device
  /// </summary>
  internal static class _RemoteDeviceMultipeerNetworkingConstructor
  {
    public static Action<AnyMultipeerNetworkingInitializedArgs> NetworkingInitialized;

    public static _ReadOnlyDictionary<Guid, _RemoteDeviceMultipeerNetworking> CurrentNetworkings
    {
      get;
      private set;
    }

    private static readonly
      ConcurrentDictionary<Guid, _RemoteDeviceMultipeerNetworking> _networkings =
        new ConcurrentDictionary<Guid, _RemoteDeviceMultipeerNetworking>();

    private static IDisposable _executor;

    static _RemoteDeviceMultipeerNetworkingConstructor()
    {
      CurrentNetworkings = new _ReadOnlyDictionary<Guid, _RemoteDeviceMultipeerNetworking>(_networkings);
    }

    public static void RegisterForInitMessage()
    {
      _executor = _EasyConnection.Register<NetworkingInitMessage>(Construct);
    }

    internal static void _Deinitialize()
    {
      var networkings = new List<_RemoteDeviceMultipeerNetworking>(_networkings.Values);
      foreach (var networking in networkings)
        networking.Dispose();

      _networkings.Clear();

      var executor = _executor;
      if (executor != null)
      {
        _executor = null;
        executor.Dispose();
      }
    }

    private static void Construct(NetworkingInitMessage message)
    {
      Construct(message.Configuration, message.StageIdentifier);
    }

    public static _RemoteDeviceMultipeerNetworking Construct
    (
      ServerConfiguration serverConfiguration,
      Guid stageIdentifier
    )
    {
      var networking =
        new _RemoteDeviceMultipeerNetworking
        (
          serverConfiguration,
          stageIdentifier
        );

      if (!_networkings.TryAdd(networking.StageIdentifier, networking))
        throw new InvalidOperationException("Tried to create a networking with a StageIdentifier already in use.");

      networking.Deinitialized +=
        (ignored) =>  _networkings.TryRemove(networking.StageIdentifier, out _);

      var handler = NetworkingInitialized;
      if (handler != null)
        handler(new AnyMultipeerNetworkingInitializedArgs(networking));

      return networking;
    }
  }
}
