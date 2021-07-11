// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections.Generic;

using Niantic.ARDK.AR;
using Niantic.ARDK.AR.ARSessionEventArgs;
using Niantic.ARDK.Extensions.Depth;
using Niantic.ARDK.Extensions;
using Niantic.ARDK.Utilities.Logging;

using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARDKExamples
{
  public class MeshingSemanticsExampleManager: MonoBehaviour
  {
    [Header("UI")]
    [SerializeField]
    private RawImage _featuresImage = null;

    [SerializeField]
    private GameObject _toggles = null;

    [SerializeField]
    private Text _channelNameText = null;

    [Header("Context Awareness Managers")]
    [SerializeField]
    private ARDepthManager _depthManager = null;

    [SerializeField]
    private ARSemanticSegmentationManager _semanticSegmentationManager = null;

    private IARSession _session;
    private bool _isShowingDepths = false;

    private Texture2D _semanticsTexture;

    // Each feature channel number corresponds to a label, first is depth and the rest is from
    // semantics channel names.
    private uint _featureChannel = 0;

    private void Awake()
    {
      ARLog.EnableLogFeature("Niantic");
      // Hide toggles now, because they're useless without ContextAwareness first initialized
      _toggles.SetActive(false);

      ARSessionFactory.SessionInitialized += OnSessionInitialized;

      _depthManager.DepthBufferUpdated += OnDepthUpdated;
      _semanticSegmentationManager.SemanticBufferUpdated += OnSemanticBufferUpdated;
    }

    private void OnDestroy()
    {
      ARSessionFactory.SessionInitialized -= OnSessionInitialized;

      // Release disparity texture
      if (_semanticsTexture != null)
        Destroy(_semanticsTexture);

      if (_session != null)
      {
        _session.FrameUpdated -= OnFrameUpdated;
        _session = null;
      }
    }

    private void OnSessionInitialized(AnyARSessionInitializedArgs args)
    {
      if (_session != null)
        return;

      _session = args.Session;
      _session.FrameUpdated += OnFrameUpdated;

      ConfigureFeaturesView(false);
    }

    private void OnFrameUpdated(FrameUpdatedArgs args)
    {
      if (args.Frame.Depth != null)
      {
        _toggles.SetActive(true);
        _session.FrameUpdated -= OnFrameUpdated;
      }
    }

    private void OnDepthUpdated(ARDepthManager.DepthBuffersUpdatedArgs args)
    {
      if (!_isShowingDepths)
        return;

      if (_featureChannel != 0)
        return;

      _featuresImage.texture = _depthManager.DisparityTexture;
    }

    private void OnSemanticBufferUpdated
    (
      ARSemanticSegmentationManager.SemanticBufferUpdatedArgs args
    )
    {
     if (!_isShowingDepths)
       return;

     if (_featureChannel == 0)
       return;

     _semanticSegmentationManager.LatestSemanticBuffer.CreateOrUpdateTexture
     (
       Rect.zero,
       ref _semanticsTexture,
       TextureFormat.ARGB32,
       (int)_featureChannel - 1
     );

     _featuresImage.texture = _semanticsTexture;
    }

    public void ToggleShowFeatures()
    {
      var newShowingDisparities = !_isShowingDepths;

      ConfigureFeaturesView(newShowingDisparities);
    }

    public void CycleFeatureChannel()
    {
      var channelNames = _semanticSegmentationManager.LatestSemanticBuffer.ChannelNames;

      _featureChannel = (_featureChannel + 1) % ((uint)channelNames.Length + 1);

      if (_featureChannel == 0)
      {
        _channelNameText.text = "Depth";
        _featuresImage.color = Color.white;
      }
      else
      {
        var text = channelNames[_featureChannel - 1];
        if (text != null)
        {
          _channelNameText.text = FormatDisplayText(channelNames[_featureChannel - 1]);
        }
        else
        {
          _channelNameText.text = "???";
        }
      }
    }

    // Toggle between the camera feed and the depth/semantics image.
    private void ConfigureFeaturesView(bool showDepths)
    {
      _isShowingDepths = showDepths;
      _channelNameText.enabled = _isShowingDepths;
      _featuresImage.enabled = _isShowingDepths;
    }

    private string FormatDisplayText(string text)
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
