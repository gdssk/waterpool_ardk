// Copyright 2021 Niantic, Inc. All Rights Reserved.

#if UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
#define UNITY_STANDALONE_DESKTOP
#endif
#if (UNITY_IOS || UNITY_ANDROID || UNITY_STANDALONE_DESKTOP) && !UNITY_EDITOR
#define AR_NATIVE_SUPPORT
#endif

using System;
using System.Runtime.InteropServices;

using ARDK.Configuration.Authentication;

using Niantic.ARDK.Configuration;
using Niantic.ARDK.Networking;
using Niantic.ARDK.Utilities.Logging;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Niantic.ARDK.Internals
{
  /// Controls the startup systems for ARDK.
  public static class StartupSystems
  {
#if UNITY_EDITOR_OSX
    [InitializeOnLoadMethod]
    private static void EditorStartup()
    {
#if !REQUIRE_MANUAL_STARTUP
      ManualStartup();
#endif
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Startup()
    {
#if AR_NATIVE_SUPPORT
#if !REQUIRE_MANUAL_STARTUP
      ManualStartup();
#endif
#endif
    }

    /// <summary>
    /// Starts up the ARDK startup systems if they haven't been started yet.
    /// </summary>
    public static void ManualStartup()
    {
#if (AR_NATIVE_SUPPORT || UNITY_EDITOR_OSX)
      try
      {
        _ROR_CREATE_STARTUP_SYSTEMS();
        if (ServerConfiguration.AuthRequired)
          SetAuthenticationParameters();
      }
      catch (DllNotFoundException e)
      {
        ARLog._DebugFormat("Failed to create ARDK startup systems: {0}", false, e);
      }
#endif
    }

    private static void SetAuthenticationParameters()
    {
      if (string.IsNullOrEmpty(ServerConfiguration.ApiKey))
      {
        var authConfig = Resources.Load<ArdkAuthConfig>("ARDK/ARDKAuthConfig");
        if (authConfig != null)
        {
          var apiKey = authConfig.ApiKey;
          if (!string.IsNullOrEmpty(apiKey))
          {
            ServerConfiguration.ApiKey = apiKey;
            ArdkGlobalConfig.SetApiKey(apiKey);
          }
          else
          {
            ARLog._Error
            (
              "No API Key was found, please add one to the ARDKAuthConfig in Resources/ARDK/"
            );
          }

          Resources.UnloadAsset(authConfig);
        }
        else
        {
          ARLog._Error
          (
            "Could not load an ARDKAuthConfig, please add one under Resources/ARDK/"
          );
        }
      }

      var authUrl = ArdkGlobalConfig.GetAuthenticationUrl();
      if (string.IsNullOrEmpty(authUrl))
      {
        ArdkGlobalConfig.SetAuthenticationUrl(ArdkGlobalConfig._DEFAULT_AUTH_URL);
        authUrl = ArdkGlobalConfig.GetAuthenticationUrl();
      }

      ServerConfiguration.AuthenticationUrl = authUrl;
    }

    // TODO(bpeake): Find a way to shutdown gracefully and add shutdown here.

#if (AR_NATIVE_SUPPORT || UNITY_EDITOR_OSX)
    [DllImport(_ARDKLibrary.libraryName)]
    private static extern void _ROR_CREATE_STARTUP_SYSTEMS();
#endif
  }
}
