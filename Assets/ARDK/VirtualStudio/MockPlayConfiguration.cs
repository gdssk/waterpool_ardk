// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections.Generic;
using Niantic.ARDK.AR.Configuration;
using Niantic.ARDK.Networking;
using Niantic.ARDK.Utilities.Logging;

using UnityEngine;

namespace Niantic.ARDK.VirtualStudio
{
  [CreateAssetMenu(fileName = "MockPlayConfiguration", menuName = "ARDK/MockPlayConfiguration", order = 0)]
  public class MockPlayConfiguration:
    ScriptableObject
  {
    [SerializeField]
    private List<MockArdkPlayerProfile> _profiles;

    /// <summary>
    /// (Optional) Prefab that will spawn to represent active mock players. These spawned
    ///   GameObjects can be moved in the Unity Editor to change the player's broadcasted pose.
    /// </summary>
    [SerializeField]
    private GameObject _mockPlayerPrefab;

    private MockArdkPlayerProfile[] _activeProfiles;

    private _IVirtualStudioManager _virtualStudioManager;

    [NonSerialized]
    private bool _initialized;

    public MockArdkPlayerProfile[] ActiveProfiles
    {
      get
      {
        if (!_initialized)
          _Initialize(_VirtualStudioManager.Instance);

        return _activeProfiles;
      }
    }

    /// <summary>
    /// Initialize method for when when a non-Inspector defined MockPlayConfiguration
    /// is needed.
    /// </summary>
    /// <param name="profiles"></param>
    /// <param name="playerPrefab"></param>
    public void SetInspectorValues
    (
      List<MockArdkPlayerProfile> profiles,
      GameObject playerPrefab
    )
    {
      _profiles = profiles;
      _mockPlayerPrefab = playerPrefab;
    }

    /// <summary>
    /// Constructs the required ARSession, MultipeerNetworking, and ARNetworking sessions for all
    /// the mock players as defined in the list of EditorArdkPlayerProfiles.
    /// </summary>
    internal void _Initialize(_IVirtualStudioManager virtualStudioManager)
    {
      if (_initialized)
        return;

      ARLog._DebugFormat("Initializing all mock players in {0}", objs: name);
      _initialized = true;
      _virtualStudioManager = virtualStudioManager;

      var activeProfiles = new List<MockArdkPlayerProfile>();

      foreach (var profile in _profiles)
      {
        if (!profile.IsActive)
          continue;

        profile.SpawnPlayerObjectDelegate = SpawnPlayerObject;
        activeProfiles.Add(profile);
      }

      _activeProfiles = activeProfiles.ToArray();
      _virtualStudioManager.InitializeAllProfiles(this);
    }

    /// <summary>
    /// Invokes the Join method on all the active players' IMultipeerNetworking components.
    /// </summary>
    /// <param name="sessionMetadata">Metadata of session to join.</param>
    public void ConnectAllPlayersNetworkings(byte[] sessionMetadata)
    {
      if (!_initialized)
        _Initialize(_VirtualStudioManager.Instance);

      foreach (var profile in ActiveProfiles)
      {
        var player = profile.GetPlayer();
        if (player.Networking != null)
          player.Networking.Join(sessionMetadata);
      }
    }

    /// <summary>
    /// Invokes the Run method on all the active player's IARSession components.
    /// </summary>
    /// <param name="arConfiguration">ARConfiguration to run with.</param>
    public void RunAllPlayersARSessions(IARConfiguration arConfiguration)
    {
      if (!_initialized)
        _Initialize(_VirtualStudioManager.Instance);

      foreach (var profile in ActiveProfiles)
      {
        var player = profile.GetPlayer();
        if (player.ARSession != null)
          player.ARSession.Run(arConfiguration);
      }
    }

    /// <summary>
    /// Returns the MockPlayer that owns the MultipeerNetworking session that the input peer
    /// is the local ("self") peer of.
    /// </summary>
    /// <param name="peer"></param>
    public MockPlayer GetPlayerWithPeer(IPeer peer)
    {
      if (!_initialized)
        _Initialize(_VirtualStudioManager.Instance);

      return _virtualStudioManager.GetPlayerWithPeer(peer);
    }

    /// <summary>
    /// Invoked when a new MockPlayer is constructed. This base method simply spawns a
    /// pre-defined prefab, but it can be overriden by a child implementation of desired.
    /// This will only be invoked for players defined in this MockPlayConfiguration,
    /// ie only for remote mock players. A GameObject can be set for the local player through
    /// the MockPlayer.SetPlayerObject method.
    /// </summary>
    /// <param name="profile"></param>
    /// <returns></returns>
    protected virtual GameObject SpawnPlayerObject(MockArdkPlayerProfile profile)
    {
      if (_mockPlayerPrefab == null)
        return null;

      var playerObject = Instantiate(_mockPlayerPrefab);
      playerObject.name = profile.PlayerName + "_mock";

      return playerObject;
    }
  }
}