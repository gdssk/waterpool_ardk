// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;

using Niantic.ARDK.AR;
using Niantic.ARDK.AR.ARSessionEventArgs;
using Niantic.ARDK.AR.Configuration;
using Niantic.ARDK.Networking;
using Niantic.ARDK.Networking.MultipeerNetworkingEventArgs;
using Niantic.ARDK.Utilities;
using Niantic.ARDK.Utilities.Logging;

using UnityEngine;

namespace Niantic.ARDK.Extensions
{
  /// A Unity component that manages an ARSession's lifetime. The session can either be created and
  /// run automatically on start, or can be controlled programatically. Any outstanding sessions are
  /// always cleaned up on destruction. Integrates with the CapabilityChecker to ensure the device
  /// supports AR.
  [DisallowMultipleComponent]
  [RequireComponent(typeof(CapabilityChecker))]
  public class ARSessionManager:
    ARConfigChanger
  {
    /// If unspecified, will default to trying to create a live device session, then remote, then mock.
    /// If specified, will throw an exception if the source is not supported on the current platform.
    [SerializeField]
    private ARInfoSource _arInfoSource = ARInfoSource.Default;

    /// Options used to transition the AR session's current state if you re-run it.
    [SerializeField]
    private ARSessionRunOptions _runOptions = ARSessionRunOptions.None;

    /// Should be true if this ARSessionManager is being used in conjunction with an ARNetworkingManager.
    [SerializeField]
    private bool _useWithARNetworkingSession;

    [SerializeField]
    [Tooltip("A boolean specifying whether or not camera images are analyzed to estimate scene lighting.")]
    private bool _isLightEstimationEnabled = false;

    [SerializeField]
    [Tooltip("A value specifying whether the camera should use autofocus or not when running.")]
    private bool _isAutoFocusEnabled = false;

    [SerializeField]
    [Tooltip("An iOS only value specifying how the session maps the real-world device motion into a coordinate system.")]
    private WorldAlignment _worldAlignment = WorldAlignment.Gravity;

    private CapabilityChecker _capabilityChecker;

    // Variables used to track when their Inspector-public counterparts are changed in OnValidate
    private bool _prevLightEstimationEnabled;
    private bool _prevAutoFocusEnabled;
    private WorldAlignment _prevWorldAlignment;

    private Guid _stageIdentifier = default;

    private IARSession _arSession;

    public IARSession ARSession
    {
      get
      {
        return _arSession;
      }
    }

    public bool IsLightEstimationEnabled
    {
      get
      {
        return _isLightEstimationEnabled;
      }
      set
      {
        if (value != _isLightEstimationEnabled)
        {
          _isLightEstimationEnabled = value;
          RaiseConfigurationChanged();
        }
      }
    }

    public bool IsAutoFocusEnabled
    {
      get
      {
        return _isAutoFocusEnabled;
      }
      set
      {
        if (value != _isAutoFocusEnabled)
        {
          _isAutoFocusEnabled = value;
          RaiseConfigurationChanged();
        }
      }
    }

    public WorldAlignment WorldAlignment
    {
      get
      {
        return _worldAlignment;
      }
      set
      {
        if (value != _worldAlignment)
        {
          _worldAlignment = value;
          RaiseConfigurationChanged();
        }
      }
    }

    protected override void InitializeImpl()
    {
      base.InitializeImpl();

      _prevLightEstimationEnabled = _isLightEstimationEnabled;
      _prevAutoFocusEnabled = _isAutoFocusEnabled;
      _prevWorldAlignment = _worldAlignment;

      _capabilityChecker = GetComponent<CapabilityChecker>();
      ARSessionFactory.SessionInitialized += OnSessionInitialized;

      if (_useWithARNetworkingSession)
        MultipeerNetworkingFactory.NetworkingInitialized += ListenForStage;
    }

    protected override void DeinitializeImpl()
    {
      base.DeinitializeImpl();

      ARSessionFactory.SessionInitialized -= OnSessionInitialized;
      MultipeerNetworkingFactory.NetworkingInitialized -= ListenForStage;

      if (_arSession == null)
        return;

      _arSession.Dispose();
      _arSession = null;
    }

    private void ListenForStage(AnyMultipeerNetworkingInitializedArgs args)
    {
      // If multiple networkings were created, the ARSessionManager will use the stage of the
      // most recently created networking.
      _stageIdentifier = args.Networking.StageIdentifier;
    }

    protected override void EnableFeaturesImpl()
    {
      base.EnableFeaturesImpl();

      if (_capabilityChecker.HasSucceeded)
        ScheduleCreateAndRunOnNextUpdate();
      else
        _capabilityChecker.Success.AddListener(ScheduleCreateAndRunOnNextUpdate);
    }

    private void ScheduleCreateAndRunOnNextUpdate()
    {
      ARSessionManager manager = this;

      _CallbackQueue.QueueCallback
      (
        () =>
        {
          if (manager != null && manager.AreFeaturesEnabled)
          {
            manager.CreateAndRun();
          }
        }
      );
    }

    protected override void DisableFeaturesImpl()
    {
      base.DisableFeaturesImpl();

      _capabilityChecker.Success.RemoveListener(CreateAndRun);

      if (_arSession != null)
        _arSession.Pause();
    }

    /// Creates the session so that Run can be called later.
    /// This will only create the session if the capability checker is successful.
    public void CreateSession()
    {
      if (_arSession != null)
      {
        ARLog._Warn("Did not create an ARSession because one already exists.");
        return;
      }

      if (!_capabilityChecker.HasSucceeded)
      {
        ARLog._Error("Failed to initialize ARSession because capability check has not yet passed.");
        return;
      }

      if (_useWithARNetworkingSession)
        ARSessionFactory.Create(_arInfoSource, _stageIdentifier);
      else
        ARSessionFactory.Create(_arInfoSource);
    }

    /// Runs an already created session with the provided options.
    public void Run()
    {
      if (_arSession == null)
      {
        ARLog._Error("Failed to run ARSession because one was not initialized.");
        return;
      }

      // Config changes are made later in the _ApplyARConfigurationChange method. That way,
      // this class is able to intercept and alter the ARConfiguration every ARSession is run with,
      // even if the session is run outside of this method.
      var worldConfig = ARWorldTrackingConfigurationFactory.Create();
      _arSession.Run(worldConfig, _runOptions);
    }

    /// Initializes and runs the session.
    public void CreateAndRun()
    {
      CreateSession();
      Run();
    }

    public void Pause()
    {
      if (_arSession == null || _arSession.State != ARSessionState.Running)
      {
        ARLog._Warn("Failed to pause ARSession.");
        return;
      }

      _arSession.Pause();
    }

    public void DestroySession()
    {
      if (_arSession == null)
      {
        ARLog._Debug("Did not destroy ARSession because one was not initialized.");
        return;
      }

      _arSession.Dispose();
    }

    internal override void _ApplyARConfigurationChange(IARConfiguration config)
    {
      config.IsLightEstimationEnabled = _isLightEstimationEnabled;
      config.WorldAlignment = _worldAlignment;

      if (config is IARWorldTrackingConfiguration worldConfig)
        worldConfig.IsAutoFocusEnabled = _isAutoFocusEnabled;
    }

    private void OnSessionInitialized(AnyARSessionInitializedArgs args)
    {
      ARLog._Debug("ARSession initialized");

      _arSession = args.Session;
      args.Session.Deinitialized += (_) => _arSession = null;
    }

    private void OnValidate()
    {
      var configChanged = false;

      if (_isLightEstimationEnabled != _prevLightEstimationEnabled)
      {
        _prevLightEstimationEnabled = _isLightEstimationEnabled;
        configChanged = true;
      }

      if (_isAutoFocusEnabled != _prevAutoFocusEnabled)
      {
        _prevAutoFocusEnabled = _isAutoFocusEnabled;
        configChanged = true;
      }

      if (_worldAlignment != _prevWorldAlignment)
      {
        _prevWorldAlignment = _worldAlignment;
        configChanged = true;
      }

      if (configChanged)
        RaiseConfigurationChanged();
    }
  }
}
