// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections.Generic;

using Niantic.ARDK.AR;
using Niantic.ARDK.AR.ARSessionEventArgs;
using Niantic.ARDK.AR.Awareness;
using Niantic.ARDK.AR.Configuration;
using Niantic.ARDK.Utilities;
using Niantic.ARDK.Utilities.Logging;

using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Niantic.ARDK.Extensions
{
  public sealed class ARSemanticSegmentationManager:
    ARSessionListener
  {
    public class SemanticBufferUpdatedArgs: IArdkEventArgs {}

    private static SemanticBufferUpdatedArgs _emptyArgs = new SemanticBufferUpdatedArgs();

    [Header("Aspect Ratio Settings")]
    [SerializeField]
    [Tooltip("(Optional) If set, this camera's info will be used to interpolate and/or fit the LatestSemanticBuffer.")]
    public Camera Camera;

    [SerializeField]
    [Tooltip("If true, the LatestSemanticBuffer will be fit to the Camera's pixel width and height.")]
    public bool FitToViewport = true;

    [Header("Interpolation Settings")]
    [SerializeField]
    [Tooltip("If true, the LatestSemanticBuffer will be interpolated to match the latest ARFrame.")]
    public bool Interpolate = true;

    [SerializeField]
    [Tooltip("Value passed into ISemanticBuffer.Interpolate calls. See ISemanticBuffer documentation for more.")]
    public float BackProjectionDistance = 0.7f;

    /// The latest semantic buffer unaltered.
    public ISemanticBuffer LatestSemanticBuffer
    {
      get;
      private set;
    }

    /// An event triggered whenever the semantic buffer values are updated.
    public event ArdkEventHandler<SemanticBufferUpdatedArgs> SemanticBufferUpdated;

    protected override void EnableFeaturesImpl()
    {
      base.EnableFeaturesImpl();

      RaiseConfigurationChanged();
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
        worldConfig.IsSemanticSegmentationEnabled = AreFeaturesEnabled;
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

      var semanticBuffer = args.Frame.Semantics;
      if (semanticBuffer == null)
        return;

      // Order matters! Rotate first so Interpolate doesn't do it's own rotation.
      semanticBuffer = semanticBuffer.RotateToScreenOrientation();

      if (Camera != null)
      {

        if (Interpolate)
        {
          semanticBuffer =
            semanticBuffer.Interpolate
            (
              args.Frame.Camera,
              Camera.pixelWidth,
              Camera.pixelHeight,
              BackProjectionDistance
            );
        }

        if (FitToViewport)
        {
          semanticBuffer =
            semanticBuffer.FitToViewport
            (
              Camera.pixelWidth,
              Camera.pixelHeight
            );
        }
      }

      LatestSemanticBuffer = semanticBuffer;

      SemanticBufferUpdated?.Invoke(_emptyArgs);
    }
  }
}
