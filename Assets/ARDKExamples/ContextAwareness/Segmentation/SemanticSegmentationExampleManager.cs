// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections.Generic;

using Niantic.ARDK.Extensions;

using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARDKExamples
{
  /// @brief An example script to demonstrate Context Awareness' semantic segmentation.
  /// @remark Use the Change Feature Channel button to swap between which active semantic will be
  /// painted white.
  /// @remark This example only works in portrait mode.
  public class SemanticSegmentationExampleManager:
    MonoBehaviour
  {
    [SerializeField]
    private ARSessionManager _arSessionManager = null;

    [Header("Rendering")]
    // The UI image that the camera overlay is rendered in.
    [SerializeField]
    private RawImage _segmentationOverlayImage = null;

    [Header("UI")]
    [SerializeField]
    private GameObject _togglesParent = null;

    [SerializeField]
    private Text _toggleFeaturesButtonText = null;

    [SerializeField]
    private Text _toggleInterpolationText = null;

    [SerializeField]
    private Text _toggleFitToViewportText = null;

    [SerializeField]
    private Text _channelNameText = null;

    private ARSemanticSegmentationManager _semanticSegmentationManager;
    private Texture2D _semanticTexture;

    // The current active channel that is painted white. -1 means that no semantic is used.
    private int _featureChannel = -1;
    private bool _gotFirstBuffer;

    private void Awake()
    {
      _semanticSegmentationManager = _arSessionManager.GetComponent<ARSemanticSegmentationManager>();
      _semanticSegmentationManager.SemanticBufferUpdated += OnSemanticBufferUpdated;
    }

    private void Start()
    {
      if (_togglesParent != null)
        _togglesParent.SetActive(false);

      Application.targetFrameRate = 60;

      _arSessionManager.EnableFeatures();
    }

    private void OnSemanticBufferUpdated
    (
      ARSemanticSegmentationManager.SemanticBufferUpdatedArgs args
    )
    {
      if (!_gotFirstBuffer)
      {
        _gotFirstBuffer = true;

        if (_togglesParent != null)
          _togglesParent.SetActive(true);
      }

      if (_featureChannel == -1)
        return;

      _semanticSegmentationManager.LatestSemanticBuffer.CreateOrUpdateTexture
      (
        Rect.zero,
        ref _semanticTexture,
        TextureFormat.ARGB32,
        _featureChannel
      );

      _segmentationOverlayImage.texture = _semanticTexture;
    }

    public void ChangeFeatureChannel()
    {
      var semanticBuffer = _semanticSegmentationManager.LatestSemanticBuffer;
      if (semanticBuffer == null)
        return;

      var channelNames = semanticBuffer.ChannelNames;

      // If the channels aren't yet known, we can't change off the initial default channel.
      if (channelNames == null)
        return;

      // Increment the channel count with wraparound.
      _featureChannel += 1;
      if (_featureChannel == channelNames.Length)
        _featureChannel = -1;

      // Update the displayed name of the channel, and enable or disable the overlay.
      if (_featureChannel == -1)
      {
        _channelNameText.text = "None";
        _segmentationOverlayImage.enabled = false;
      }
      else
      {
        _channelNameText.text = FormatChannelName(channelNames[_featureChannel]);
        _segmentationOverlayImage.enabled = true;
      }
    }

    public void ToggleSessionSemanticFeatures()
    {
      var newEnabledState = !_semanticSegmentationManager.enabled;

      _toggleFeaturesButtonText.text = newEnabledState ? "Disable Features" : "Enable Features";

      _semanticSegmentationManager.enabled = newEnabledState;
    }

    public void ToggleInterpolation()
    {
      var newInterpolationValue = !_semanticSegmentationManager.Interpolate;
      _semanticSegmentationManager.Interpolate = newInterpolationValue;
      _toggleInterpolationText.text =
        newInterpolationValue ? "Disable Interpolation" : "Enable Interpolation";
    }

    public void ToggleFitToViewport()
    {
      var newFitToViewportValue = !_semanticSegmentationManager.FitToViewport;
      _semanticSegmentationManager.FitToViewport = newFitToViewportValue;
      _toggleFitToViewportText.text =
        newFitToViewportValue ? "Disable Fit To Viewport" : "Enable Fit To Viewport";
    }

    private void OnDestroy()
    {
      _semanticSegmentationManager.SemanticBufferUpdated -= OnSemanticBufferUpdated;

      // Release semantic overlay texture
      if (_semanticTexture != null)
        Destroy(_semanticTexture);
    }

    private string FormatChannelName(string text)
    {
      var parts = text.Split('_');
      List<string> displayParts = new List<string>();
      foreach (var part in parts)
      {
        displayParts.Add(char.ToUpper(part[0]) + part.Substring(1));
      }

      return String.Join(" ", displayParts.ToArray());
    }
  }
}
