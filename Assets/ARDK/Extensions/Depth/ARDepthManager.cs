// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;

using Niantic.ARDK.AR;
using Niantic.ARDK.AR.ARSessionEventArgs;
using Niantic.ARDK.AR.Awareness;
using Niantic.ARDK.AR.Configuration;
using Niantic.ARDK.Utilities;
using Niantic.ARDK.Utilities.Logging;

using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARDK.Extensions.Depth
{
  public class ARDepthManager:
    ARSessionListener
  {
    public struct DepthBuffersUpdatedArgs : IArdkEventArgs
    { }

    [SerializeField]
    [Tooltip("Controls which depth estimation features are enabled.")]
    private DepthFeatures _depthFeatures = DepthFeatures.Depth;

    [Header("Aspect Ratio Settings")]
    [SerializeField]
    [Tooltip("(Optional) If set, this camera's info will be used to interpolate and/or fit the LatestDepthBuffer.")]
    public Camera Camera;

    [SerializeField]
    [Tooltip("If true, the LatestDepthBuffer will be fit to the Camera's pixel width and height.")]
    public bool FitToViewport = true;

    [Header("Interpolation Settings")]
    [SerializeField]
    [Tooltip("If true, the LatestDepthBuffer will be interpolated to match the latest ARFrame.")]
    public bool Interpolate = true;

    [SerializeField]
    [Tooltip("Value passed into IDepthBuffer.Interpolation calls. See IDepthBuffer documentation for more.")]
    public float BackProjectionDistance = 0.9f;

    [Header("Render Settings")]
    [SerializeField]
    [Tooltip("If true, the depth buffer will be rendered to the DisparityTexture.")]
    public bool RenderToTexture = true;

    [SerializeField]
    public TextureFormat TextureFormat = TextureFormat.ARGB32;

    /// An event triggered whenever the semantic buffer values are updated.
    public event ArdkEventHandler<DepthBuffersUpdatedArgs> DepthBufferUpdated;

    private DepthFeatures _prevDepthFeatures;
    private Texture2D _disparityTexture;
    private readonly DepthBuffersUpdatedArgs _emptyArgs = new DepthBuffersUpdatedArgs();

    /// The latest depth buffer.
    public IDepthBuffer LatestDepthBuffer { get; private set; }

    public Texture2D DisparityTexture
    {
      get { return _disparityTexture; }
    }

    public DepthFeatures DepthFeatures
    {
      get { return _depthFeatures; }
      set
      {
        if (value != _depthFeatures)
        {
          _depthFeatures = value;
          RaiseConfigurationChanged();
        }
      }
    }

    protected override void DeinitializeImpl()
    {
      base.DeinitializeImpl();

      if (_disparityTexture != null)
        Destroy(_disparityTexture);
    }

    protected override void EnableFeaturesImpl()
    {
      base.EnableFeaturesImpl();
      _prevDepthFeatures = _depthFeatures;

      RaiseConfigurationChanged();

      if (Camera == null && (Interpolate || FitToViewport || RenderToTexture))
      {
        ARLog._Error
        (
          "Camera property must be non-null in order to utilize the ARDepthManager's " +
          "Interpolate, FitToViewport, or RenderToTexture features.");
      }
    }

    protected override void DisableFeaturesImpl()
    {
      base.DisableFeaturesImpl();

      RaiseConfigurationChanged();
    }

    protected override void ListenToSession()
    {
      _arSession.FrameUpdated += OnFrameUpdated;
    }

    protected override void StopListeningToSession()
    {
      _arSession.FrameUpdated -= OnFrameUpdated;
    }

    internal override void _ApplyARConfigurationChange(IARConfiguration config)
    {
      if (config is IARWorldTrackingConfiguration worldConfig)
        worldConfig.DepthFeatures = AreFeaturesEnabled ? DepthFeatures : DepthFeatures.None;
    }

    private void OnFrameUpdated(FrameUpdatedArgs args)
    {
      var awarenessStatus =
        _arSession.GetAwarenessInitializationStatus
        (
          out AwarenessInitializationError error,
          out string message
        );

      if (awarenessStatus == AwarenessInitializationStatus.Failed)
      {
        StopListeningToSession();

        ARLog._ErrorFormat
        (
          "Failed to initialize Context Awareness features (error: {0}, message: {1})",
          error,
          message
        );

        return;
      }

      var depthBuffer = args.Frame.Depth;
      if (depthBuffer == null)
        return;

      // Order matters! Rotate first so Interpolate doesn't do it's own rotation.
      depthBuffer = depthBuffer.RotateToScreenOrientation();

      if (Camera != null)
      {
        var width = Camera.pixelWidth;
        var height = Camera.pixelHeight;

        if (Interpolate)
        {
          depthBuffer =
            depthBuffer.Interpolate
            (
              args.Frame.Camera,
              width,
              height,
              BackProjectionDistance
            );
        }

        if (FitToViewport)
        {
          depthBuffer =
            depthBuffer.FitToViewport
            (
              width,
              height
            );
        }

        if (RenderToTexture)
        {
          // If the depth buffer was already fit to the viewport, then there's no need to further
          // change it's aspect ratio.
          var cropRect = FitToViewport ? Rect.zero : depthBuffer.GetCroppedRect(width, height);

          float maxDisp = 1 / depthBuffer.NearDistance;
          float minDisp = 1 / depthBuffer.FarDistance;

          depthBuffer.CreateOrUpdateTexture
          (
            cropRect,
            ref _disparityTexture,
            TextureFormat,
            depth => (1/depth - minDisp) / (maxDisp - minDisp)
          );
        }
      }

      LatestDepthBuffer = depthBuffer;
      DepthBufferUpdated?.Invoke(_emptyArgs);
    }

    protected void OnValidate()
    {
      if (_prevDepthFeatures != _depthFeatures)
      {
        _prevDepthFeatures = _depthFeatures;
        RaiseConfigurationChanged();
      }
    }
  }
}
