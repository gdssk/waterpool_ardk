// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Text;

using Niantic.ARDK.AR;
using Niantic.ARDK.AR.ARSessionEventArgs;
using Niantic.ARDK.Networking;
using Niantic.ARDK.Utilities.Logging;

using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARDK.Extensions
{
  /// Initializes, connects, runs, and disposes a MultipeerNetworking object either according to
  /// the Unity lifecycle of this component, or when scripted to do so.
  /// @note
  ///   A MultipeerNetworking object cannot leave a session and be re-used to join a session
  ///   again, so if ManageUsingUnityLifecycle is true, enabling the component will initialize
  ///   a new MultipeerNetworking object if the existing one has left a session.
  [DisallowMultipleComponent]
  public sealed class NetworkSessionManager
    : UnityLifecycleDriver
  {
    /// If unspecified, will default to trying to create a live device session, then remote, then mock.
    /// If specified, will throw an exception if the source is not supported on the current platform.
    /// @note
    ///   Live device networking is supported in the Unity Editor, but it must be explicitly
    ///   specified here.
    [SerializeField]
    private ARInfoSource _arInfoSource = ARInfoSource.Default;

    /// Should be true if this ARSessionManager is being used in conjunction with an ARNetworkingManager.
    [SerializeField]
    private bool _useWithARNetworkingSession;

    /// The session identifier used when `Connect` is called.
    /// @note If the `InputField` is not-null, its text value will override this field's current value.
    [SerializeField]
    [Tooltip("The session identifier used when `Connect` is called.")]
    private string _sessionIdentifier = null;

    [SerializeField]
    private Encoding _encoding = Encoding.UTF8;

    /// If not empty, the text value of this InputField will be used as the session
    /// identifier when `Connect` is called. Leave empty to get the default behaviour.
    [SerializeField]
    [Tooltip("(Optional) InputField source for the session identifier.")]
    private InputField _inputField = null;

    private IMultipeerNetworking _networking;
    private Guid _stageIdentifier = default;

    public IMultipeerNetworking Networking
    {
      get { return _networking; }
    }

    protected override void InitializeImpl()
    {
      base.InitializeImpl();

      if (_useWithARNetworkingSession)
        ARSessionFactory.SessionInitialized += ListenForStage;

      if (_inputField != null)
      {
        _sessionIdentifier = _inputField.text;
        _inputField.onEndEdit.AddListener(SetSessionIdentifier);
      }
    }

    protected override void DeinitializeImpl()
    {
      base.DeinitializeImpl();

      ARSessionFactory.SessionInitialized -= ListenForStage;

      if (_inputField != null)
        _inputField.onEndEdit.RemoveListener(SetSessionIdentifier);

      if (_networking == null)
        return;

      _networking.Dispose();
      _networking = null;
    }

    private void ListenForStage(AnyARSessionInitializedArgs args)
    {
      _stageIdentifier = args.Session.StageIdentifier;
    }

    protected override void EnableFeaturesImpl()
    {
      base.EnableFeaturesImpl();
      CreateAndConnect();
    }

    protected override void DisableFeaturesImpl()
    {
      base.DisableFeaturesImpl();

      // A networking, once left, is useless because it cannot be used to join/re-join a session.
      // That's why DestroySession() is called here and separate Leave() method does not exist
      // in this class.
      if (_networking != null)
        DestroySession();
    }

    public void SetSessionIdentifier(string sessionIdentifier)
    {
      _sessionIdentifier = sessionIdentifier;

      if (_inputField != null)
        _inputField.text = sessionIdentifier;
    }

    /// Initializes a new MultipeerNetworking object with the set ARInfoSource(s), if one does
    /// not yet exist.
    public void CreateSession()
    {
      if (_networking != null)
      {
        ARLog._Error
        (
          "Failed to create a MultipeerNetworking session because one already exists." +
          "To create multiple sessions, use the MultipeerNetworkingFactory API instead."
        );

        return;
      }

      if (_useWithARNetworkingSession)
        _networking = MultipeerNetworkingFactory.Create(_arInfoSource, _stageIdentifier);
      else
        _networking = MultipeerNetworkingFactory.Create(_arInfoSource);

      ARLog._DebugFormat("Created {0} MultipeerNetworking.", objs: _networking.ARInfoSource);
      _networking.Deinitialized += deinitializedArgs => _networking = null;
    }

    /// Connects the existing MultipeerNetworking object to a session with the set SessionIdentifier.
    public void Connect()
    {
      if (_networking == null)
      {
        ARLog._Error("Failed to connect MultipeerNetworking session because one was not initialized.");
        return;
      }

      if (string.IsNullOrEmpty(_sessionIdentifier) && _inputField != null)
        _sessionIdentifier = _inputField.text;

      var sessionMetadata = _encoding.GetBytes(_sessionIdentifier);

      _networking.Join(sessionMetadata);
    }

    public void CreateAndConnect()
    {
      CreateSession();
      Connect();
    }

    public void DestroySession()
    {
      if (_networking == null)
      {
        ARLog._Debug("Did not destroy MultipeerNetworking session because one was not initialized.");
        return;
      }

      if (_networking.IsConnected)
        _networking.Leave();

      _networking.Dispose();
      _networking = null;
    }
  }
}
