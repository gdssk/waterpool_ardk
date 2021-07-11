// Copyright 2021 Niantic, Inc. All Rights Reserved.

using UnityEngine;

/// @namespace Niantic.ARDK.Utilities
/// Utilities that provide functionality to various objects or classes
namespace Niantic.ARDK.Utilities
{
  /// <summary>
  /// Attach to objects that should only be seen in Editor builds
  /// </summary>
  public class DisableOnStartInNonEditorBuilds: MonoBehaviour
  {
    private void Start()
    {
#if !UNITY_EDITOR && !UNITY_STANDALONE
      gameObject.SetActive(false);
#endif
    }
  }
}
