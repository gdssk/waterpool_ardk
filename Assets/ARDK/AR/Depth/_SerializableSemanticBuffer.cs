// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Linq;
using System.Runtime.InteropServices;

using Niantic.ARDK.AR.Awareness;

using Unity.Collections;

using UnityEngine;

namespace Niantic.ARDK.AR.Depth
{
  // Can't use [Serializable]. Need to provide a serializer.
  internal sealed class _SerializableSemanticBuffer:
    _SerializableAwarenessBufferBase<UInt16>,
    ISemanticBuffer
  {
    private static bool _hasWarnedAboutRotateToScreenOrientation;
    private static bool _hasWarnedAboutInterpolation;
    private static bool _hasWarnedAboutFitToDisplay;
    private bool[] _hasChannelCache;

    internal _SerializableSemanticBuffer
    (
      uint width,
      uint height,
      bool isKeyframe,
      Matrix4x4 viewMatrix,
      NativeArray<UInt16> data,
      UInt32 numChannels,
      string[] channelNames,
      Matrix4x4 arCameraIntrinsics
    )
      : base(width, height, isKeyframe, viewMatrix, data, arCameraIntrinsics)
    {
      ChannelCount = numChannels;
      ChannelNames = channelNames;
    }

    /// <inheritdoc />
    public UInt32 ChannelCount { get; private set; }

    /// <inheritdoc />
    public string[] ChannelNames { get; private set; }

    /// <inheritdoc />
    public int GetChannelIndex(string channelName)
    {
      return Array.IndexOf(ChannelNames, channelName);
    }

    /// <inheritdoc />
    public UInt16 GetChannelTextureMask(int channelIndex)
    {
      // test for invalid index
      if (channelIndex < 0 || channelIndex >= ChannelNames.Length)
        return 0;

      var mask = 1 << ((sizeof(short) * 8) - 1 - channelIndex);
      return Convert.ToUInt16(mask);
    }

    /// <inheritdoc />
    public UInt16 GetChannelTextureMask(int[] channelIndices)
    {
      UInt16 mask = 0;

      foreach (var index in channelIndices)
      {
        mask |= GetChannelTextureMask(index);
      }

      return mask;
    }

    /// <inheritdoc />
    public UInt16 GetChannelTextureMask(string channelName)
    {
      var index = GetChannelIndex(channelName);

      return GetChannelTextureMask(index);
    }

    /// <inheritdoc />
    public UInt16 GetChannelTextureMask(string[] channelNames)
    {
      UInt16 mask = 0;

      foreach (var name in channelNames)
      {
        mask |= GetChannelTextureMask(name);
      }

      return mask;
    }

    /// <inheritdoc />
    public bool DoesChannelExistAt(int x, int y, int channelIndex)
    {
      var data = Data;
      var value = data[x + y * (int) Width];
      var bitsPerPixel = Marshal.SizeOf(value) * 8;
      var flag = 1u << (bitsPerPixel - 1) - channelIndex;
      return (value & flag) != 0;
    }

    /// <inheritdoc />
    public bool DoesChannelExistAt(int x, int y, string channelName)
    {
      var index = Array.IndexOf(ChannelNames, channelName);
      return index != -1 && DoesChannelExistAt(x, y, index);
    }

    /// <inheritdoc />
    public bool DoesChannelExistAt(Vector2 uv, int channelIndex)
    {
      var widthMinusOne = (int)Width - 1;
      var heightMinusOne = (int)Height - 1;

      // Sample the buffer
      var x = Mathf.Clamp((int)Mathf.Floor(uv.x * widthMinusOne), 0, widthMinusOne);
      var y = Mathf.Clamp((int)Mathf.Floor(uv.y * heightMinusOne), 0, heightMinusOne);

      return DoesChannelExistAt(x, y, channelIndex);
    }

    /// <inheritdoc />
    public bool DoesChannelExistAt(Vector2 uv, string channelName)
    {
      var index = Array.IndexOf(ChannelNames, channelName);
      return index != -1 && DoesChannelExistAt(uv, index);
    }

    /// <inheritdoc />
    public bool DoesChannelExistAt
      (Vector2 point, int viewportWidth, int viewportHeight, int channelIndex)
    {
      var sourceWidth = (int)Width;
      var sourceHeight = (int)Height;
      var srcRatio = sourceWidth * 1f / sourceHeight;
      var viewRatio = viewportWidth * 1f / viewportHeight;
      int croppedWidth, xOffset;

      if (srcRatio > viewRatio)
      {
        // Source image is wider than view, crop the width
        croppedWidth = Mathf.RoundToInt(sourceHeight * viewRatio);
        xOffset = Mathf.RoundToInt((sourceWidth - croppedWidth) / 2.0f);
      }
      else
      {
        // Source image is slimmer than view, pad the width
        xOffset = Mathf.RoundToInt((sourceWidth - (int)(sourceHeight * viewRatio)) / 2.0f);
        croppedWidth = sourceWidth - 2 * xOffset;
      }

      // Sample the buffer. Note that we're inverting viewportPoint's y coordinate to align with the view's coordinate system.
      var x = xOffset +
        Mathf.Clamp((int)Mathf.Floor(point.x * (croppedWidth - 1)), 0, croppedWidth - 1);

      var y = Mathf.Clamp
        ((int)Mathf.Floor((1.0f - point.y) * (sourceHeight - 1)), 0, sourceHeight - 1);

      return x >= 0 &&
        x < sourceWidth &&
        y >= 0 &&
        y < sourceHeight &&
        DoesChannelExistAt(x, y, channelIndex);
    }

    /// <inheritdoc />
    public bool DoesChannelExistAt
      (Vector2 point, int viewportWidth, int viewportHeight, string channelName)
    {
      var index = Array.IndexOf(ChannelNames, channelName);
      return index != -1 && DoesChannelExistAt(point, viewportWidth, viewportHeight, index);
    }

    /// <inheritdoc />
    public bool DoesChannelExist(int channelIndex)
    {
      _ComputeHasChannelCache();
      return _hasChannelCache != null && _hasChannelCache[channelIndex];
    }

    public bool DoesChannelExist(string channelName)
    {
      var index = Array.IndexOf(ChannelNames, channelName);
      return index != -1 && DoesChannelExist(index);
    }

    public ISemanticBuffer RotateToScreenOrientation()
    {
      if (!_hasWarnedAboutRotateToScreenOrientation)
      {
        Debug.Log
        (
          "SemanticBuffer.RotateToScreenOrientation() not yet supported in editor. " +
          "Resolving by returning same object."
        );

        _hasWarnedAboutRotateToScreenOrientation = true;
      }

      return this;
    }

    public ISemanticBuffer Interpolate
    (
      IARCamera arCamera,
      int viewportWidth,
      int viewportHeight,
      float backProjectionDistance = 0.95f
    )
    {
      if (!_hasWarnedAboutInterpolation)
      {
        Debug.Log
        (
          "SemanticBuffer.Interpolate() not yet supported in editor. " +
          "Resolving by returning same object."
        );

        _hasWarnedAboutInterpolation = true;
      }

      return this;
    }

    public ISemanticBuffer FitToViewport
    (
      int viewportWidth,
      int viewportHeight
    )
    {
      if (!_hasWarnedAboutFitToDisplay)
      {
        Debug.Log
        (
          "SemanticBuffer.FitToViewport() not yet supported in editor. " +
          "Resolving by returning same object."
        );

        _hasWarnedAboutFitToDisplay = true;
      }

      return this;
    }

    /// <inheritdoc />
    public bool CreateOrUpdateTexture
      (Rect croppedRect, ref Texture2D texture, TextureFormat format, int channelIndex)
    {
      uint flag = 1u << ((sizeof(Int16) * 8 - 1) - channelIndex);
      return
        _AwarenessBufferHelper._CreateOrUpdateTexture
        (
          Data,
          (int)Width,
          (int)Height,
          croppedRect,
          ref texture,
          format,
          false,
          val => (val & flag) != 0 ? 1.0f : 0.0f
        );
    }

    /// <summary>
    /// Calculate if this image has a specific channel or not by caching all the values and see
    /// which channels are present
    /// </summary>
    private void _ComputeHasChannelCache()
    {
      if (_hasChannelCache == null && Data != null && Data.Length > 0)
      {
        var bitsPerPixel = Marshal.SizeOf(Data[0]) * 8;

        _hasChannelCache = new bool[ChannelCount];

        foreach (var pixel in Data)
        {
          for (var i = 0; i < ChannelCount; i++)
          {
            if (!_hasChannelCache[i])
            {
              var flag = 1u << (bitsPerPixel - 1) - i;
              if ((pixel & flag) != 0)
              {
                _hasChannelCache[i] = true;
              }
            }
          }
        }
      }
    }
  }
}
