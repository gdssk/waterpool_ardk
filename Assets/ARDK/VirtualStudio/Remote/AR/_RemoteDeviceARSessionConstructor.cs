// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;

using Niantic.ARDK.AR.ARSessionEventArgs;
using Niantic.ARDK.Utilities.Extensions;
using Niantic.ARDK.Utilities;
using Niantic.ARDK.Utilities.Logging;
using Niantic.ARDK.VirtualStudio.Remote;
using Niantic.ARDK.VirtualStudio.Remote.Data;

using UnityEngine;

namespace Niantic.ARDK.VirtualStudio.AR.Remote
{
  /// <summary>
  /// A static wrapper class for listening for messages from the editor to create a parallel
  /// PlayerARSession running on device.
  /// </summary>
  internal static class _RemoteDeviceARSessionConstructor
  {
    public static Action<AnyARSessionInitializedArgs> SessionInitialized;

    public static _RemoteDeviceARSession CurrentSession
    {
      get
      {
        return _currentSession;
      }
    }

    private static _RemoteDeviceARSession _currentSession;
    private static IDisposable _executor;

    public static void RegisterForInitMessage()
    {
      _executor = _EasyConnection.Register<ARSessionInitMessage>(Construct);
    }

    internal static void Deinitialize()
    {
      if (_currentSession != null)
      {
        _currentSession.Dispose();
        _currentSession = null;
      }

      if (_executor != null)
      {
        _executor.Dispose();
        _executor = null;
      }
    }

    private static void Construct(ARSessionInitMessage message)
    {
      Construct(message.StageIdentifier, message.UseImageCompression);
    }

    public static _RemoteDeviceARSession Construct(Guid stageIdentifier, bool useImageCompression)
    {
      if (CurrentSession != null)
      {
        ARLog._Error("A _RemoteDeviceARSession instance already exists.");
        return null;
      }

      var session =
        new _RemoteDeviceARSession
        (
          stageIdentifier,
          useImageCompression
        );

      _currentSession = session;
      session.Deinitialized += (_) => _currentSession = null;

      var handler = SessionInitialized;
      if (handler != null)
        handler(new AnyARSessionInitializedArgs(session, isLocal: true));

      return session;
    }
  }
}
