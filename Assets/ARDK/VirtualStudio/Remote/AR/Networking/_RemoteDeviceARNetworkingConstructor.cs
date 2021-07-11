// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;

using Niantic.ARDK.AR.ARSessionEventArgs;
using Niantic.ARDK.Networking.MultipeerNetworkingEventArgs;
using Niantic.ARDK.Utilities;
using Niantic.ARDK.Utilities.Logging;
using Niantic.ARDK.VirtualStudio.AR.Remote;
using Niantic.ARDK.VirtualStudio.Networking.Remote;
using Niantic.ARDK.VirtualStudio.Remote;
using Niantic.ARDK.VirtualStudio.Remote.Data;

namespace Niantic.ARDK.VirtualStudio.AR.Networking.Remote
{
  /// <summary>
  /// A static wrapper class for listening for messages from the editor to create a parallel
  /// PlayerARNetworking running on device
  /// </summary>
  internal static class _RemoteDeviceARNetworkingConstructor
  {
    public static _RemoteDeviceARNetworking CurrentARNetworking
    {
      get
      {
        return _currentARNetworking;
      }
    }

    private static _RemoteDeviceARNetworking _currentARNetworking;

    private static IDisposable _executor;
    private static ARNetworkingInitMessage _initMessage;

    private static bool _shouldTryToConstruct;

    public static void RegisterForInitMessage()
    {
      _RemoteDeviceARSessionConstructor.SessionInitialized += ARSessionInitialized;
      _RemoteDeviceMultipeerNetworkingConstructor.NetworkingInitialized += MultipeerNetworkingInitialized;

      // Listen for an init...
      _executor = _EasyConnection.Register<ARNetworkingInitMessage>
      (
        initMessage =>
        {
          _initMessage = initMessage;
          _shouldTryToConstruct = true;
          TryToConstruct();
        }
      );
    }

    internal static void Deinitialize()
    {
      _initMessage = null;

      if (_currentARNetworking != null)
      {
        _currentARNetworking.Dispose();
        _currentARNetworking = null;
      }

      if (_executor != null)
      {
        _executor.Dispose();
        _executor = null;
      }
    }

    private static void TryToConstruct()
    {
      if (CurrentARNetworking != null)
      {
        ARLog._Error("A _RemoteDeviceARNetworking instance already exists.");
        return;
      }

      var arSession = _RemoteDeviceARSessionConstructor.CurrentSession;
      if (arSession == null)
        return;

      if (arSession.StageIdentifier != _initMessage.StageIdentifier)
      {
        var msg = "ARNetworking's stage identifier must match previously constructed ARSession's.";
        ARLog._Error(msg);
        return;
      }

      // Need to set it false here so that a construction of a networking doesn't trigger
      // another call of this method before this call has finished.
      _shouldTryToConstruct = false;

      _RemoteDeviceARNetworking arNetworking;
      if (_initMessage.ConstructFromExistingNetworking)
      {
        _RemoteDeviceMultipeerNetworking networking;
        _RemoteDeviceMultipeerNetworkingConstructor.CurrentNetworkings.TryGetValue
        (
          _initMessage.StageIdentifier,
          out networking
        );

        if (networking == null)
        {
          _shouldTryToConstruct = true;
          return;
        }

        arNetworking = new _RemoteDeviceARNetworking(arSession, networking);
      }
      else
      {
        var networking =
          _RemoteDeviceMultipeerNetworkingConstructor.Construct
          (
            _initMessage.ServerConfiguration,
            _initMessage.StageIdentifier
          );

        arNetworking = new _RemoteDeviceARNetworking(arSession, networking);
      }

      _currentARNetworking = arNetworking;
      arNetworking.Deinitialized += (_) => _currentARNetworking = null;
    }

    private static void MultipeerNetworkingInitialized(AnyMultipeerNetworkingInitializedArgs networking)
    {
      if (_shouldTryToConstruct)
        TryToConstruct();
    }

    private static void ARSessionInitialized(AnyARSessionInitializedArgs arSession)
    {
      if (_shouldTryToConstruct)
        TryToConstruct();
    }
  }
}
