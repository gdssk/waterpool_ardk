// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System.Linq;
using System.Text;

using ARDK.VirtualStudio.AR.Camera;

using Niantic.ARDK.AR.Configuration;
using Niantic.ARDK.Networking;
using Niantic.ARDK.Utilities.Logging;
using Niantic.ARDK.VirtualStudio.Networking;
using Niantic.ARDK.VirtualStudio.Networking.Mock;

using UnityEditor;
using UnityEngine;

namespace Niantic.ARDK.VirtualStudio
{
  public class VirtualStudioConfigurationEditor : EditorWindow
  {
    private const string TOOLSUITE_WINDOW_ENABLED_KEY = "ARDK_ToolSuiteWindow_Enabled";
    private const string PLAY_CONFIGURATION_KEY = "ARDK_PlayConfiguration";
    private const string USE_DETECTED_SESSION_METADATA_KEY = "ARDK_Use_Detected_Session_Metadata";
    private const string INPUT_SESSION_METADATA_KEY = "ARDK_Input_Session_Metadata";

    private const string FPS_KEY = "ARDK_ToolSuiteWindow_FPS";
    private const string MOVESPEED_KEY = "ARDK_ToolSuiteWindow_Movespeed";
    private const string LOOKSPEED_KEY = "ARDK_ToolSuiteWindow_Lookspeed";

    private bool _enabled;

    private MockPlayConfiguration _playConfiguration;

    private bool _useDetectedSessionMetadata;
    private string _inputSessionMetadata;

    private int _fps;
    private float _moveSpeed;
    private int _lookSpeed;

    [MenuItem("ARDK/Virtual Studio")]
    public static void Init()
    {
      var window = GetWindow<VirtualStudioConfigurationEditor>("Virtual Studio");
      window.Show();

      window._enabled = window.GetEnabledPreference();
      if (window._enabled)
        window.LoadPreferences();
    }

    private bool GetEnabledPreference()
    {
      var value = PlayerPrefs.GetInt(TOOLSUITE_WINDOW_ENABLED_KEY, 1);
      return value == 1;
    }

    private void LoadPreferences()
    {
      var playConfigurationName = PlayerPrefs.GetString(PLAY_CONFIGURATION_KEY, null);
      if (!string.IsNullOrEmpty(playConfigurationName))
        _playConfiguration = GetPlayConfiguration(playConfigurationName);

      _useDetectedSessionMetadata = PlayerPrefs.GetInt(USE_DETECTED_SESSION_METADATA_KEY, 1) == 1;
      _inputSessionMetadata = PlayerPrefs.GetString(INPUT_SESSION_METADATA_KEY, "ABC");

      _fps = PlayerPrefs.GetInt(FPS_KEY, 30);
      _moveSpeed = PlayerPrefs.GetFloat(MOVESPEED_KEY, 3);
      _lookSpeed = PlayerPrefs.GetInt(LOOKSPEED_KEY, 180);
    }

    private static MockPlayConfiguration GetPlayConfiguration(string name)
    {
      var filter = string.Format("{0} t:MockPlayConfiguration", name);
      var guids = AssetDatabase.FindAssets(filter);

      if (guids.Length == 0)
      {
        ARLog._WarnFormat("Could not load MockPlayConfiguration named: {0}", objs: name);
        return null;
      }

      var assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
      var asset = AssetDatabase.LoadAssetAtPath(assetPath, typeof(MockPlayConfiguration));

      ARLog._DebugFormat("Loaded MockPlayConfiguration named: {0}", objs: name);
      return asset as MockPlayConfiguration;
    }

    private void OnGUI()
    {
      EditorGUILayout.Space();

      DrawEnabledGUI();

      EditorGUILayout.Space();
      EditorGUILayout.Space();

      EditorGUI.BeginDisabledGroup(!_enabled);

      DrawCameraConfigurationGUI();

      EditorGUILayout.Space();
      EditorGUILayout.Space();

      DrawPlayConfigurationGUI();

      EditorGUILayout.Space();
      EditorGUILayout.Space();

      DrawSessionMetadataGUI();

      EditorGUILayout.Space();
      EditorGUILayout.Space();

      EditorGUI.BeginDisabledGroup(!Application.isPlaying);
      DrawRuntimeGUI();
      EditorGUI.EndDisabledGroup();

      EditorGUI.EndDisabledGroup();
    }

    private void DrawEnabledGUI()
    {
      var newEnabled = GUILayout.Toggle(_enabled, "Enabled");

      if (_enabled != newEnabled)
      {
        _enabled = newEnabled;
        PlayerPrefs.SetInt(TOOLSUITE_WINDOW_ENABLED_KEY, _enabled ? 1 : 0);
        if (_enabled)
          LoadPreferences();
      }
    }

    private void DrawCameraConfigurationGUI()
    {
      GUILayout.Label("Mock Camera Controls", EditorStyles.boldLabel);

      EditorGUI.BeginDisabledGroup(Application.isPlaying);

      var newFps = EditorGUILayout.IntField("FPS", _fps);
      if (newFps != _fps)
      {
        _fps = newFps;
        _MockCameraConfiguration.FPS = _fps;
        PlayerPrefs.SetInt(FPS_KEY, _fps);
      }

      EditorGUI.EndDisabledGroup();

      var newMovespeed = EditorGUILayout.Slider("Move Speed", _moveSpeed, 0f, 10f);
      if (newMovespeed != _moveSpeed)
      {
        _moveSpeed = newMovespeed;
        _MockCameraConfiguration.MoveSpeed = _moveSpeed;
        PlayerPrefs.SetFloat(MOVESPEED_KEY, _moveSpeed);
      }

      var newLookSpeed = EditorGUILayout.IntSlider("Look Speed", _lookSpeed, 0, 180);
      if (newLookSpeed != _lookSpeed)
      {
        _lookSpeed = newLookSpeed;
        _MockCameraConfiguration.LookSpeed = _lookSpeed;
        PlayerPrefs.SetFloat(LOOKSPEED_KEY, _lookSpeed);
      }
    }

    private void DrawPlayConfigurationGUI()
    {
      var newPlayConfiguration =
        (MockPlayConfiguration) EditorGUILayout.ObjectField
        (
          "Play Configuration",
          _playConfiguration,
          typeof(MockPlayConfiguration),
          false
        );

      if (_playConfiguration != newPlayConfiguration)
      {
        _playConfiguration = newPlayConfiguration;
        PlayerPrefs.SetString
        (
          PLAY_CONFIGURATION_KEY,
          _playConfiguration == null ? null : _playConfiguration.name
        );
      }
    }

    private void DrawSessionMetadataGUI()
    {
      GUILayout.Label("Mock Player Commands", EditorStyles.boldLabel);

      var newUseDetectedSessionMetadata =
        GUILayout.Toggle
        (
          _useDetectedSessionMetadata,
          "Use detected MultipeerNetworking session metadata."
        );

      if (_useDetectedSessionMetadata != newUseDetectedSessionMetadata)
      {
        _useDetectedSessionMetadata = newUseDetectedSessionMetadata;
        PlayerPrefs.SetInt(USE_DETECTED_SESSION_METADATA_KEY, _useDetectedSessionMetadata ? 1 : 0);
      }

      EditorGUI.BeginDisabledGroup(_useDetectedSessionMetadata);

      var newInputSessionMetadata =
        EditorGUILayout.TextField("Session Metadata", _inputSessionMetadata);

      if (_useDetectedSessionMetadata && _inputSessionMetadata != newInputSessionMetadata)
      {
        _inputSessionMetadata = newInputSessionMetadata;
        PlayerPrefs.SetString(INPUT_SESSION_METADATA_KEY, _inputSessionMetadata);
      }

      EditorGUI.EndDisabledGroup();
    }

    private void DrawRuntimeGUI()
    {
      if (GUILayout.Button("Enable Mock Players"))
        EnableMockPlayers();
    }

    private byte[] GetDetectedSessionMetadata()
    {
      var localPlayer = _VirtualStudioManager.Instance.LocalPlayer;

      if (localPlayer == null || localPlayer.Networking == null || !localPlayer.Networking.IsConnected)
      {
        var message =
          "In order to connect mock players using a detected session metadata," +
          "the local networking must have already connected.";

        ARLog._Warn(message);

        return null;
      }

      var hostNetworking = localPlayer.Networking as _MockMultipeerNetworking;
      if (hostNetworking == null)
        return null;

      return hostNetworking.JoinedSessionMetadata;
    }

    private void EnableMockPlayers()
    {
      if (_playConfiguration != null)
      {
        // Select session metadata and then join all networkings
        byte[] sessionMetadata = null;

        if (_useDetectedSessionMetadata)
          sessionMetadata = GetDetectedSessionMetadata();
        else if (!string.IsNullOrEmpty(_inputSessionMetadata))
          sessionMetadata = Encoding.UTF8.GetBytes(_inputSessionMetadata);

        if (sessionMetadata == null || sessionMetadata.Length == 0)
        {
          ARLog._Warn("No valid session metadata for mock players to connect with found.");
          return;
        }

        _playConfiguration.ConnectAllPlayersNetworkings(sessionMetadata);

        // Create standard ARConfiguration and the run all AR sessions
        // Todo: GUI to configure ARConfiguration
        var arConfiguration = ARWorldTrackingConfigurationFactory.Create();
        arConfiguration.PlaneDetection = PlaneDetection.Horizontal | PlaneDetection.Vertical;
        arConfiguration.IsSharedExperienceEnabled = true;

        _playConfiguration.RunAllPlayersARSessions(arConfiguration);
      }
    }
  }
}