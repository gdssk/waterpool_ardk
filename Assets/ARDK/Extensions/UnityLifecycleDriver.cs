// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Security.Cryptography;

using Niantic.ARDK.Utilities;
using Niantic.ARDK.Utilities.Logging;

using UnityEngine;

namespace Niantic.ARDK.Extensions
{
  // Stub class to allow sealing of Unity lifecycle methods
  public abstract class UnityLifecycleDriverBase:
    MonoBehaviour
  {
    protected virtual void Awake()
    {
    }

    protected virtual void Start()
    {
    }

    protected virtual void OnDestroy()
    {
    }

    protected virtual void OnEnable()
    {
    }

    protected virtual void OnDisable()
    {
    }
  }

  /// Base class for ARDK's extension MonoBehaviour extension classes. All Unity lifecycle
  /// methods are sealed to prevent child classes from implementing functionality in them;
  /// functionality is instead kept inside virtual methods so the class can be controlled both
  /// by the Unity lifecycle and independently (via pure scripting).
  ///
  /// If _manageUsingUnityLifecycle is true:
  ///   * Unity's Awake calls Initialize
  ///   * Unity's OnDestroy calls Remove
  ///   * Unity's OnEnable calls EnableFeatures
  ///   * Unity's OnDisable calls Disable
  public abstract class UnityLifecycleDriver:
    UnityLifecycleDriverBase
  {
    /// @note False by default so that EnableFeatures isn't automatically called if this
    /// component is instantiated in script.
    [SerializeField]
    private bool _manageUsingUnityLifecycle = false;

    protected bool Initialized { get; private set; }

    protected bool CanInitialize
    {
      get { return !Initialized && !_deinitialized; }
    }

    protected bool AreFeaturesEnabled { get; private set; }

    // For use in internal testing only
    internal bool _ManageUsingUnityLifecycle
    {
      get { return _manageUsingUnityLifecycle; }
      set { _manageUsingUnityLifecycle = value; }
    }

    private _ThreadCheckedObject _threadChecker;
    private bool _deinitialized;

#region LifecycleManagementMethods
    /// Prepares the instance for use. This is where it will gather all the resources it needs as
    /// defined by a subclass in InitializeImpl.
    public void Initialize()
    {
      _threadChecker?._CheckThread();

      if (!CanInitialize)
      {
        if (_deinitialized)
          ARLog._Warn("Cannot call Initialize on a UnityLifecycleDriver instance that was deinitialized.");

        return;
      }

      Initialized = true;

      InitializeImpl();
    }

    /// Releases any resources held by the instance as defined by a subclass in DeinitializeImpl.
    /// Once this is called, Initialize can't be called. Instead a new instance must be made.
    public void Deinitialize()
    {
      _threadChecker?._CheckThread();

      if (!Initialized)
        return;

      DisableFeatures();

      Initialized = false;
      _deinitialized = true;

      DeinitializeImpl();
    }

    /// Enabled any features controlled by this instance as defined by a subclass in
    /// EnableFeaturesImpl. This will initialize the instance if it wasn't already.
    public void EnableFeatures()
    {
      _threadChecker?._CheckThread();

      // Allow this function to be called multiple times without repeating side effects.
      if (AreFeaturesEnabled)
        return;

      // Ensure this object is already initialized and fail if it can't.
      Initialize();

      if (!Initialized)
        return;

      AreFeaturesEnabled = true;

      EnableFeaturesImpl();
    }

    /// Disable any features controlled by the instance as defined by a subclass in
    /// DisableFeaturesImpl.
    public void DisableFeatures()
    {
      _threadChecker?._CheckThread();

      if (!AreFeaturesEnabled)
        return;

      // There is no need to check the initialization state as an enabled instance is by definition
      // initialized.

      AreFeaturesEnabled = false;

      DisableFeaturesImpl();
    }
#endregion

#region UnityLifecycleIntegration
    protected sealed override void Awake()
    {
      _threadChecker = new _ThreadCheckedObject();

      if (_ManageUsingUnityLifecycle)
        Initialize();
    }

    protected sealed override void Start()
    {
    }

    protected sealed override void OnDestroy()
    {
      Deinitialize();
    }

    protected sealed override void OnEnable()
    {
      if (_ManageUsingUnityLifecycle)
        EnableFeatures();
    }

    protected sealed override void OnDisable()
    {
      if (_ManageUsingUnityLifecycle)
        DisableFeatures();
    }
#endregion

    /// @note If overriding in a subclass, make sure to call this base method.
    protected virtual void InitializeImpl()
    {
      _threadChecker?._CheckThread();
    }

    protected virtual void DeinitializeImpl()
    {
      _threadChecker?._CheckThread();
    }

    protected virtual void EnableFeaturesImpl()
    {
      _threadChecker?._CheckThread();
    }

    protected virtual void DisableFeaturesImpl()
    {
      _threadChecker?._CheckThread();
    }

    // Called when the user hits the Reset button in the Inspector's context menu or when
    // adding the component the first time. This function is only called in editor mode.
    // Used to give good default values in the Inspector.
    protected virtual void Reset()
    {
      _manageUsingUnityLifecycle = true;
    }
  }
}
