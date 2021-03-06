// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;

using Niantic.ARDK.AR;
using Niantic.ARDK.VirtualStudio;
using Niantic.ARDK.VirtualStudio.Networking;
using Niantic.ARDK.VirtualStudio.Networking.Mock;
using Niantic.ARDK.VirtualStudio.Networking.Remote;
using Niantic.ARDK.Networking.MultipeerNetworkingEventArgs;
using Niantic.ARDK.Utilities;
using Niantic.ARDK.Utilities.Collections;
using Niantic.ARDK.Utilities.Logging;
using Niantic.ARDK.VirtualStudio.Remote;

namespace Niantic.ARDK.Networking
{
  /// A class to create new MultipeerNetworking instances as well as to be notified of their
  /// creation.
  public static class MultipeerNetworkingFactory
  {
    /// Initializes the static members of this class that depend on previously initialized members.
    static MultipeerNetworkingFactory()
    {
      _readOnlyNetworkings = _networkings.AsArdkReadOnly();
    }

    /// Create a MultipeerNetworking appropriate for the current device.
    ///
    /// On a mobile device, the attempted order will be LiveDevice, Remote, and finally Mock.
    /// In the Unity Editor, the attempted order will be Remote, then Mock.
    ///
    /// @param stageIdentifier
    ///   The identifier used by the C++ library to connect all related components.
    ///
    /// @returns The created MultipeerNetworking, or throws if it was not possible to create a session.
    public static IMultipeerNetworking Create(Guid stageIdentifier = default(Guid))
    {
      return _Create(ServerConfiguration.ARBE, null, stageIdentifier);
    }

    public static IMultipeerNetworking Create
    (
      ServerConfiguration serverConfiguration,
      Guid stageIdentifier = default(Guid)
    )
    {
      return _Create(serverConfiguration, null, stageIdentifier);
    }

    /// Create a MultipeerNetworking with the specified ARInfoSource.
    ///
    /// @param source
    ///   The source used to create the MultipeerNetworking.
    /// @param stageIdentifier
    ///   The identifier used by the C++ library to connect all related components.
    ///
    /// @returns The created MultipeerNetworking, or throws if it was not possible to create a session.
    public static IMultipeerNetworking Create(ARInfoSource source, Guid stageIdentifier = default(Guid))
    {
      var networking = _Create(source, stageIdentifier, ServerConfiguration.ARBE);
      if (networking == null)
      {
        throw new NotSupportedException
          ("The provided source is not supported by this build.");
      }

      return networking;
    }

    /// Create a MultipeerNetworking with the specified ARInfoSource.
    ///
    /// @param source
    ///   The source used to create the MultipeerNetworking.
    /// @param serverConfiguration
    ///   The ServerConfiguration that this MultipeerNetworking will use to communicate with ARBEs
    /// @param stageIdentifier
    ///   The identifier used by the C++ library to connect all related components.
    ///
    /// @returns The created MultipeerNetworking, or throws if it was not possible to create a session.
    public static IMultipeerNetworking Create
    (
      ARInfoSource source,
      ServerConfiguration serverConfiguration,
      Guid stageIdentifier = default(Guid)
    )
    {
      var networking = _Create(source, stageIdentifier, serverConfiguration);
      if (networking == null)
      {
        throw new NotSupportedException
          ("The the provided source is not supported by this build.");
      }

      return networking;
    }

    /// A collection of all current networking stacks
    public static IReadOnlyCollection<IMultipeerNetworking> Networkings
    {
      get
      {
        return _readOnlyNetworkings;
      }
    }

    private static ArdkEventHandler<AnyMultipeerNetworkingInitializedArgs> _networkingInitialized;

    /// Event called when a new MultipeerNetworking instance is initialized.
    public static event ArdkEventHandler<AnyMultipeerNetworkingInitializedArgs> NetworkingInitialized
    {
      add
      {
        _StaticMemberValidator._FieldIsNullWhenScopeEnds(() => _networkingInitialized);

        _networkingInitialized += value;

        // If there already exists some networkings, call the event so you don't miss anything
        foreach (var networking in _networkings)
        {
          var args = new AnyMultipeerNetworkingInitializedArgs(networking);
          value(args);
        }
      }
      remove
      {
        _networkingInitialized -= value;
      }
    }

    /// Tries to create a MultipeerNetworking of any of the given sources.
    ///
    /// @param configuration
    ///   Configuration object telling how to connect to the server.
    /// @param sources
    ///   A collection of sources used to create the networking for. As not all platforms support
    ///   all sources, the code will try to create the networking for the first source, then for the
    ///   second and so on. If sources is null or empty, then the order used is LiveDevice,
    ///   Remote and finally Mock.
    /// @param stageIdentifier
    ///   The identifier used by the C++ library to connect all related components.
    ///
    /// @returns The created networking, or null if it was not possible to create the object.
    internal static IMultipeerNetworking _Create
    (
      ServerConfiguration configuration,
      IEnumerable<ARInfoSource> sources = null,
      Guid stageIdentifier = default(Guid)
    )
    {
      bool triedAtLeast1 = false;

      if (sources != null)
      {
        foreach (var source in sources)
        {
          var possibleResult = _Create(source, stageIdentifier, configuration);
          if (possibleResult != null)
            return possibleResult;

          triedAtLeast1 = true;
        }
      }

      if (!triedAtLeast1)
        return _Create(configuration, ARSessionFactory._defaultBestMatches, stageIdentifier);

      throw new NotSupportedException("None of the provided sources are supported by this build.");
    }

    internal static IMultipeerNetworking _CreateVirtualStudioManagedNetworking
    (
      ARInfoSource source,
      ServerConfiguration configuration,
      Guid stageIdentifier,
      _IVirtualStudioManager virtualStudioMaster,
      bool isLocal
    )
    {
      IMultipeerNetworking implementation;
      switch (source)
      {
        case ARInfoSource.Mock:
          implementation = new _MockMultipeerNetworking(stageIdentifier, virtualStudioMaster);
          break;

        case ARInfoSource.Remote:
          implementation = new _RemoteEditorMultipeerNetworking(configuration, stageIdentifier);
          break;

        default:
          // Both LiveDevice and Default are invalid cases for this method
          throw new ArgumentOutOfRangeException(nameof(source), source, null);
      }

      _InvokeNetworkingInitialized(implementation, isLocal);
      return implementation;
    }

    private static IMultipeerNetworking _Create
    (
      ARInfoSource source,
      Guid stageIdentifier,
      ServerConfiguration configuration
    )
    {
      if (stageIdentifier == default(Guid))
        stageIdentifier = Guid.NewGuid();

      IMultipeerNetworking result;
      switch (source)
      {
        case ARInfoSource.Default:
          return Create(configuration, stageIdentifier);

        case ARInfoSource.LiveDevice:
          result = new _NativeMultipeerNetworking(configuration, stageIdentifier);
          break;

        case ARInfoSource.Remote:
          if (!_RemoteConnection.IsEnabled)
            return null;

          result = new _RemoteEditorMultipeerNetworking(configuration, stageIdentifier);
          break;

        case ARInfoSource.Mock:
          result = new _MockMultipeerNetworking(stageIdentifier, _VirtualStudioManager.Instance);
          break;

        default:
          throw new InvalidEnumArgumentException(nameof(source), (int)source, source.GetType());
      }

      _InvokeNetworkingInitialized(result, isLocal: true);
      return result;
    }

    internal static _NativeMultipeerNetworking _CreateLiveDeviceNetworking
    (
      ServerConfiguration serverConfiguration,
      Guid stageIdentifier,
      bool isLocal
    )
    {
      var result = new _NativeMultipeerNetworking(serverConfiguration, stageIdentifier);
      _InvokeNetworkingInitialized(result, isLocal);
      return result;
    }

    private static
      ArdkEventHandler<AnyMultipeerNetworkingInitializedArgs> _nonLocalNetworkingInitialized;

    internal static event
      ArdkEventHandler<AnyMultipeerNetworkingInitializedArgs> _NonLocalNetworkingInitialized
      {
        add
        {
          _StaticMemberValidator._FieldIsNullWhenScopeEnds(() => _nonLocalNetworkingInitialized);

          _nonLocalNetworkingInitialized += value;

          // If there already exists some networkings, call the event so you don't miss anything
          foreach (var networking in _nonLocalNetworkings)
          {
            var args = new AnyMultipeerNetworkingInitializedArgs(networking);
            value(args);
          }
        }
        remove
        {
          _nonLocalNetworkingInitialized -= value;
        }
      }

    #region Implementation
    private static readonly ARDKReadOnlyCollection<IMultipeerNetworking> _readOnlyNetworkings;

    private static readonly HashSet<IMultipeerNetworking> _networkings =
      new HashSet<IMultipeerNetworking>(_ReferenceComparer<IMultipeerNetworking>.Instance);

    private static readonly HashSet<IMultipeerNetworking> _nonLocalNetworkings =
      new HashSet<IMultipeerNetworking>(_ReferenceComparer<IMultipeerNetworking>.Instance);

    private static void _InvokeNetworkingInitialized(IMultipeerNetworking networking, bool isLocal)
    {
      if (SessionExists(networking, isLocal))
      {
        ARLog._WarnFormat
        (
          "An IMultipeerNetworking instance with the StageIdentifier {0} was already initialized.",
          false,
          networking.StageIdentifier
        );

        return;
      }

      ArdkEventHandler<AnyMultipeerNetworkingInitializedArgs> handler;

      if (isLocal)
      {
        ARLog._Debug("Initializing a local session");
        _StaticMemberValidator._CollectionIsEmptyWhenScopeEnds(() => _networkings);

        _networkings.Add(networking);
        networking.Deinitialized += (_) => _networkings.Remove(networking);
        handler = _networkingInitialized;
      }
      else
      {
        ARLog._Debug("Initializing a non-local session");
        _StaticMemberValidator._CollectionIsEmptyWhenScopeEnds(() => _nonLocalNetworkings);

        _nonLocalNetworkings.Add(networking);
        networking.Deinitialized += (_) => _nonLocalNetworkings.Remove(networking);
        handler = _nonLocalNetworkingInitialized;
      }

      if (handler != null)
      {
        var args = new AnyMultipeerNetworkingInitializedArgs(networking);
        handler(args);
      }
    }

    private static bool SessionExists(IMultipeerNetworking networking, bool isLocal)
    {
      if (isLocal)
        return _networkings.Contains(networking);

      return _nonLocalNetworkings.Contains(networking);
    }
    #endregion
  }
}
