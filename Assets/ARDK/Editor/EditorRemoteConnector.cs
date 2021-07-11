// Copyright 2021 Niantic, Inc. All Rights Reserved.

using Niantic.ARDK.Utilities.Logging;

using UnityEditor;
using UnityEngine;

namespace Niantic.ARDK.VirtualStudio.Remote.Editor
{
/// @brief An editor tool that makes it easy to connect to a remote session
  public class EditorRemoteConnector:
    EditorWindow
  {
    private string _pin;
    private _RemoteConnection.ConnectionMethod _connectionMethod;
    private bool _useRemote;

    private const string ARDKPinProperty = "ARDK_Pin";
    private const string ARDKConnectionMethodProperty = "ARDK_Connection_Method";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Startup()
    {
      if (!_RemoteConnection.IsEnabled)
        return;

      if (Application.isPlaying)
      {
        var pin = PlayerPrefs.GetString(ARDKPinProperty, "").ToUpper();
        var connectionMethod =
          (_RemoteConnection.ConnectionMethod)PlayerPrefs.GetInt
          (
            "ARDK_Connection_Method",
            (int)_RemoteConnection.ConnectionMethod.Internet
          );

        if (!string.IsNullOrEmpty(pin) || connectionMethod == _RemoteConnection.ConnectionMethod.USB)
        {
          _RemoteConnection.InitIfNone(connectionMethod);
          _RemoteConnection.Connect(pin);
        }
        else
        {
          ARLog._Release("Unable to create remote connection: No pin entered for Internet connection");
        }
      }
    }

    [MenuItem("ARDK/Remote Connector")]
    static void Init()
    {
      var window = (EditorRemoteConnector)GetWindow(typeof(EditorRemoteConnector));
      window.Show();
      window._pin = PlayerPrefs.GetString(ARDKPinProperty, "");
      window._connectionMethod =
        (_RemoteConnection.ConnectionMethod)PlayerPrefs.GetInt
        (
          ARDKConnectionMethodProperty,
          (int)_RemoteConnection.ConnectionMethod.Internet
        );

      window._useRemote = _RemoteConnection.IsEnabled;
    }

    void OnGUI()
    {
      EditorGUILayout.LabelField("Remote Connection Info");

      GUI.enabled = !Application.isPlaying;

      var newConnectionMethod =
        (_RemoteConnection.ConnectionMethod)EditorGUILayout.EnumPopup(_connectionMethod);

      var newPin = EditorGUILayout.TextField("PIN:", _pin);
      var useRemote = EditorGUILayout.Toggle("Use Remote", _useRemote);

      GUI.enabled = true;

      if (newPin != _pin)
      {
        _pin = newPin;
        PlayerPrefs.SetString(ARDKPinProperty, newPin);
      }

      if (newConnectionMethod != _connectionMethod)
      {
        _connectionMethod = newConnectionMethod;
        PlayerPrefs.SetInt(ARDKConnectionMethodProperty, (int)_connectionMethod);
      }

      if (useRemote != _useRemote)
      {
        _useRemote = useRemote;
        _RemoteConnection.IsEnabled = _useRemote;
      }

      GUIStyle s = new GUIStyle(EditorStyles.largeLabel);
      s.fontSize = 20;
      s.fixedHeight = 30;

      if (Application.isPlaying)
      {
        EditorGUILayout.LabelField(_RemoteConnection.CurrentConnectionMethod.ToString());

        if (!_RemoteConnection.IsReady)
        {
          if (_RemoteConnection.IsEnabled)
          {
            s.normal.textColor = Color.magenta;
            EditorGUILayout.LabelField("Waiting for Remote Connection to be ready...", s);
          }
          else
          {
            s.normal.textColor = Color.gray;
            EditorGUILayout.LabelField("Not using remote...", s);
          }
        }
        else if (!_RemoteConnection.IsConnected)
        {
          s.normal.textColor = Color.blue;
          EditorGUILayout.LabelField("Waiting for remote device to connect...", s);
        }
        else
        {
          s.normal.textColor = Color.green;
          EditorGUILayout.LabelField("Connected!", s);
        }
      }
      else
      {
        if (_RemoteConnection.IsEnabled)
          EditorGUILayout.LabelField("Waiting for play mode...", s);
        else
          EditorGUILayout.LabelField("Not using remote...", s);
      }
    }

    private void OnInspectorUpdate()
    {
      Repaint();
    }
  }
}
