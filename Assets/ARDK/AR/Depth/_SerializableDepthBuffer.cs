// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using Niantic.ARDK.AR.Awareness;
using Niantic.ARDK.Utilities.Logging;

using Unity.Collections;

using UnityEngine;

namespace Niantic.ARDK.AR.Depth
{
  // Can't use [Serializable]. Need to provide a serializer.
  internal sealed class _SerializableDepthBuffer
    : _SerializableAwarenessBufferBase<float>,
      IDepthBuffer
  {
    private static bool _hasWarnedAboutRotateToScreenOrientation;
    private static bool _hasWarnedAboutInterpolation;
    private static bool _hasWarnedAboutFitToDisplay;

    internal _SerializableDepthBuffer
    (
      uint width,
      uint height,
      bool isKeyframe,
      Matrix4x4 viewMatrix,
      NativeArray<float> data,
      float nearDistance,
      float farDistance,
      Matrix4x4 arCameraIntrinsics
    )
      : base(width, height, isKeyframe, viewMatrix, data, arCameraIntrinsics)
    {
      NearDistance = nearDistance;
      FarDistance = farDistance;
    }

    public float NearDistance { get; private set; }

    public float FarDistance { get; private set; }

    // TODO (Virtual Studio): Return new buffers to match native implementation
    public IDepthBuffer RotateToScreenOrientation()
    {
      if (!_hasWarnedAboutRotateToScreenOrientation)
      {
        ARLog._Debug
        (
          "DepthBuffer.RotateToScreenOrientation() not yet supported in editor. " +
          "Resolving by returning same object."
        );

        _hasWarnedAboutRotateToScreenOrientation = true;
      }

      return this;
    }

    // TODO (Virtual Studio): Return new buffers to match native implementation
    public IDepthBuffer Interpolate
    (
      IARCamera arCamera,
      int viewportWidth,
      int viewportHeight,
      float backProjectionDistance = 0.95f
    )
    {
      if (!_hasWarnedAboutInterpolation)
      {
        ARLog._Debug
        (
          "DepthBuffer.Interpolate() not yet supported in editor. " +
          "Resolving by returning same object."
        );

        _hasWarnedAboutInterpolation = true;
      }

      return this;
    }

    // TODO (Virtual Studio): Return new buffers to match native implementation
    public IDepthBuffer FitToViewport
    (
      int viewportWidth,
      int viewportHeight
    )
    {
      if (!_hasWarnedAboutFitToDisplay)
      {
        ARLog._Debug
        (
          "DepthBuffer.FitToViewport() not yet supported in editor. " +
          "Resolving by returning same object."
        );

        _hasWarnedAboutFitToDisplay = true;
      }

      return this;
    }

    public bool CreateOrUpdateTexture
    (
      Rect croppedRect,
      ref Texture2D texture,
      TextureFormat format,
      Func<float, float> depthConversion = null
    )
    {
      return
        _AwarenessBufferHelper._CreateOrUpdateTexture
        (
          Data,
          (int)Width,
          (int)Height,
          croppedRect,
          ref texture,
          format,
          true,
          depthConversion
        );
    }
  }
}
