// Copyright 2021 Niantic, Inc. All Rights Reserved.

using Niantic.ARDK.Extensions.Depth;

using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARDKExamples
{
  /// @brief An example script to visualize Context Awareness' depth information.
  /// @remark This example only works in portrait mode.
  public class DepthExampleManager:
    MonoBehaviour
  {
    [SerializeField]
    private ARDepthManager _arDepthManager = null;

    // The UI image that the camera overlay is rendered in.
    [SerializeField]
    private RawImage _depthImage = null;

    [Header("UI")]
    [SerializeField]
    private GameObject _toggles = null;

    [SerializeField]
    private Text _toggleViewButtonText = null;

    [SerializeField]
    private Text _toggleDepthButtonText = null;

    private bool _depthEnabled = true;
    private bool _isShowingDepths = false;
    private bool _gotFirstBuffer;

    private void Start()
    {
      if (_toggles != null)
        _toggles.SetActive(false);

      Application.targetFrameRate = 60;

      _arDepthManager.DepthBufferUpdated += OnDepthBufferUpdated;
    }

    private void OnDepthBufferUpdated(ARDepthManager.DepthBuffersUpdatedArgs args)
    {
      if (!_gotFirstBuffer)
      {
        _gotFirstBuffer = true;

        if (_toggles != null)
          _toggles.SetActive(true);
      }

      if (_isShowingDepths)
        _depthImage.texture = _arDepthManager.DisparityTexture;
    }

    public void ToggleShowDepth()
    {
      _isShowingDepths = !_isShowingDepths;

      // Toggle UI elements
      _toggleViewButtonText.text = _isShowingDepths ? "Show Camera" : "Show Depth";
      _depthImage.enabled = _isShowingDepths;
    }

    public void ToggleSessionDepthFeatures()
    {
      _depthEnabled = !_depthEnabled;

      // ARSession configuration through ARDepthManager
      _arDepthManager.enabled = _depthEnabled;

      // Toggle UI elements
      _toggleDepthButtonText.text = _depthEnabled ? "Disable Depth" : "Enable Depth";
    }
  }
}
