// Copyright 2021 Niantic, Inc. All Rights Reserved.

using Niantic.ARDK.AR;
using Niantic.ARDK.AR.Configuration;
using Niantic.ARDK.AR.Networking;
using Niantic.ARDK.Utilities.Logging;

using UnityEngine;

namespace Niantic.ARDK.Extensions
{
  [RequireComponent(typeof(ARSessionManager))]
  [RequireComponent(typeof(NetworkSessionManager))]
  [DisallowMultipleComponent]
  public class ARNetworkingManager: ARConfigChanger
  {
    [SerializeField]
    private bool _isSharedExperienceEnabled = true;

    private bool _prevSharedExperienceEnabled;

    private IARNetworking _arNetworking;
    private ARSessionManager _arSessionManager;
    private NetworkSessionManager _networkSessionManager;

    public bool IsSharedExperienceEnabled
    {
      get
      {
        return _isSharedExperienceEnabled;
      }
      set
      {
        if (value != _isSharedExperienceEnabled)
        {
          _isSharedExperienceEnabled = value;
          RaiseConfigurationChanged();
        }
      }
    }

    public IARNetworking ARNetworking
    {
      get { return _arNetworking; }
    }

    public ARSessionManager ARSessionManager
    {
      get { return _arSessionManager; }
    }

    public NetworkSessionManager NetworkSessionManager
    {
      get { return _networkSessionManager; }
    }

    protected override void InitializeImpl()
    {
      base.InitializeImpl();

      _arSessionManager = GetComponent<ARSessionManager>();
      _arSessionManager.Initialize();

      _networkSessionManager = GetComponent<NetworkSessionManager>();
      _networkSessionManager.Initialize();
    }

    protected override void EnableFeaturesImpl()
    {
      base.EnableFeaturesImpl();

      _prevSharedExperienceEnabled = _isSharedExperienceEnabled;
      RaiseConfigurationChanged();

      CreateAndStart();
    }

    protected override void DisableFeaturesImpl()
    {
      base.DisableFeaturesImpl();

      // A networking (and hence ARNetworking) session, once left, is useless because it cannot
      // be used to join/re-join a session. That's why DestroySession() is called here and
      // separate Leave() method does not exist in this class.
      DestroySession();
    }

    public void CreateSession()
    {
      if (_arNetworking != null)
      {
        ARLog._Error("Failed to create an ARNetworking session because one already exists.");
        return;
      }

      if (_arSessionManager.ARSession == null)
        _arSessionManager.CreateSession();

      if (_networkSessionManager.Networking == null)
        _networkSessionManager.CreateSession();

      _arNetworking = ARNetworkingFactory.Create(_arSessionManager.ARSession, _networkSessionManager.Networking);
    }

    public void ConnectAndRun()
    {
      if (_arNetworking == null)
      {
        ARLog._Error("Failed to connect and run ARNetworking session because one was not initialized.");
        return;
      }

      _arSessionManager.Run();
      _networkSessionManager.Connect();
    }

    public void CreateAndStart()
    {
      CreateSession();
      ConnectAndRun();
    }

    public void DestroySession()
    {
      if (_arNetworking == null)
      {
        ARLog._Warn("Failed to destroy ARNetworking session because one was not initialized.");
        return;
      }

      _arSessionManager.DestroySession();
      _networkSessionManager.DestroySession();
      _arNetworking.Dispose();
      _arNetworking = null;
    }

    internal override void _ApplyARConfigurationChange(IARConfiguration config)
    {
      if (config is IARWorldTrackingConfiguration worldConfig)
      {
        worldConfig.IsSharedExperienceEnabled = AreFeaturesEnabled;
      }
    }

    private void OnValidate()
    {
      if (_isSharedExperienceEnabled != _prevSharedExperienceEnabled)
      {
        _prevSharedExperienceEnabled = _isSharedExperienceEnabled;
        RaiseConfigurationChanged();
      }
    }
  }
}
