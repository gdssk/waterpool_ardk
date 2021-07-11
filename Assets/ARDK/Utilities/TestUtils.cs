// Copyright 2021 Niantic, Inc. All Rights Reserved.

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Niantic.ARDK.Utilities
{
  public static class TestUtils
  {
    // Disables "Value assigned but never used" warning.
#pragma warning disable CS0414
    private static bool _bypassEditorCheck;
#pragma warning restore CS0414

    /// <summary>
    /// Temporary solution used to define behavior for when code designed to only be run
    /// in a non-Unity editor environment is called, such as during in-editor tests.
    /// Todo: remove when MultipeerNetworking, ARSession, and ARNetworking are refactored
    /// </summary>
    public static bool ShouldBypassEditorCheck
    {
      get
      {
#if !UNITY_EDITOR
        return false;
#else
        return _bypassEditorCheck || SceneManager.GetActiveScene().name.Contains("InitTestScene");
#endif
      }
      set { _bypassEditorCheck = value; }
    }

    // Disables "Value assigned but never used" warning.
#pragma warning disable CS0414
    private static bool _bypassNativeCheck;
#pragma warning restore CS0414

    /// <summary>
    /// Temporary solution used to define behavior for when external NAR calls should be made
    /// but can't be made, such as during in-editor tests.
    ///  Todo: remove when MultipeerNetworking, ARSession, and ARNetworking are refactored
    /// </summary>
    public static bool ShouldBypassNativeCheck
    {
      get
      {
#if !UNITY_EDITOR
        return false;
#else
        return _bypassNativeCheck;
#endif
      }
      set { _bypassNativeCheck = value; }
    }
  }
}
