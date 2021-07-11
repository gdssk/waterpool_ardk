// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Runtime.InteropServices;

using Niantic.ARDK.Utilities.Logging;

using Unity.Collections;

using UnityEngine;

namespace Niantic.ARDK.AR.Awareness
{
  /// <summary>
  /// Common functions to be used for inference buffers used by the native and serialized code.
  /// NOTE:
  /// - if these functions are templated, iOS increases in 10% cpu usage
  /// - if raw pointers (unsafe) is used instead of arrays, memory usage significantly increases
  /// </summary>
  internal static class _AwarenessBufferHelper
  {
    // Buffer used to prepare values for depth textures
    [ThreadStatic]
    private static Color[] _bufferCache;

    /// <summary>
    /// Returns a boolean if input texture has a copy of the observation image buffer.
    /// The texture if successfully copied needs to be deallocated.
    /// If croppedRect is set to Rect.zero then do no cropping.
    /// Only call on Unity thread.
    /// </summary>
    internal static bool _CreateOrUpdateTexture
    (
      NativeArray<float> src,
      int width,
      int height,
      Rect croppedRect,
      ref Texture2D texture,
      TextureFormat format,
      bool opaque = true,
      Func<float, float> valueConverter = null
    ) {
      var destWidth = width;
      var destHeight = height;

      if (croppedRect != Rect.zero) {
        destWidth = (int)croppedRect.width;
        destHeight = (int)croppedRect.height;
      }

      var success = _SetColorBufferFloat
        (
          ref _bufferCache,
          src,
          width,
          height,
          destWidth,
          destHeight,
          (int)croppedRect.x,
          (int)croppedRect.y,
          opaque,
          valueConverter
        );
      
      if (!success) {
        return false;
      }

      return _CreateOrUpdateTexture(ref texture, _bufferCache, format, destWidth, destHeight);
    }
    
    /// <summary>
    /// Returns a boolean if input texture has a copy of the observation image buffer.
    /// The texture if successfully copied needs to be deallocated.
    /// If croppedRect is set to Rect.zero then do no cropping.
    /// Only call on Unity thread.
    /// </summary>
    internal static bool _CreateOrUpdateTexture
    (
      NativeArray<UInt16> src,
      int width,
      int height,
      Rect croppedRect,
      ref Texture2D texture,
      TextureFormat format,
      bool opaque = true,
      Func<UInt16, float> valueConverter = null
    ) {
      var destWidth = width;
      var destHeight = height;

      if (croppedRect != Rect.zero) {
        destWidth = (int)croppedRect.width;
        destHeight = (int)croppedRect.height;
      }

      var success = _SetColorBufferUInt
        (
          ref _bufferCache,
          src,
          width,
          height,
          destWidth,
          destHeight,
          (int)croppedRect.x,
          (int)croppedRect.y,
          opaque,
          valueConverter
        );
      
      if (!success)
        return false;

      return _CreateOrUpdateTexture(ref texture, _bufferCache, format, destWidth, destHeight);
    }

    /// <summary>
    /// Sets the internal color color cache used for texture creation using a source buffer of type Float32.
    /// </summary>
    private static bool _SetColorBufferFloat
    (
      ref Color[] destination,
      NativeArray<float> source,
      int srcWidth,
      int srcHeight,
      int destWidth,
      int destHeight,
      int sourceXOffset = 0,
      int sourceYOffset = 0,
      bool opaque = true,
      Func<float, float> valueConverter = null
    ) {
      var length = destWidth * destHeight;
      if (destination == null || destination.Length != length)
        destination = new Color[length];

      var isPreprocessingDefined = valueConverter != null;
      var i = 0;

      for (var y = destHeight + sourceYOffset - 1; y >= sourceYOffset; y--) {
        for (var x = sourceXOffset; x < destWidth + sourceXOffset; x++) {
          var val = 0.0f;
          if (x >= 0 && x < srcWidth && y >= 0 && y < srcHeight) {
            val = isPreprocessingDefined
              ? valueConverter(source[y * srcWidth + x])
              : source[y * srcWidth + x];
          }

          destination[i++] = new Color(val, val, val, opaque ? 1 : val);
        }
      }

      return true;
    }

    /// <summary>
    /// Sets the internal color color cache used for texture creation using a source buffer of type Int16.
    /// </summary>
    private static bool _SetColorBufferUInt
    (
      ref Color[] destination,
      NativeArray<UInt16> source,
      int srcWidth,
      int srcHeight,
      int destWidth,
      int destHeight,
      int sourceXOffset = 0,
      int sourceYOffset = 0,
      bool opaque = true,
      Func<UInt16, float> valueConverter = null
    ) {
      var length = destWidth * destHeight;
      if (destination == null || destination.Length != length)
        destination = new Color[length];

      var isPreprocessingDefined = valueConverter != null;
      var i = 0;

      for (var y = destHeight + sourceYOffset - 1; y >= sourceYOffset; y--) {
        for (var x = sourceXOffset; x < destWidth + sourceXOffset; x++) {
          var val = 0.0f;
          if (x >= 0 && x < srcWidth && y >= 0 && y < srcHeight) {
            val = isPreprocessingDefined
              ? valueConverter(source[y * srcWidth + x])
              : source[y * srcWidth + x];
          }

          destination[i++] = new Color(val, val, val, opaque ? 1 : val);
        }
      }

      return true;
    }

    /// <summary>
    /// Allocates the specified texture if needed and copies the pixels from the color buffer.
    /// </summary>
    private static bool _CreateOrUpdateTexture(ref Texture2D texture, Color[] pixels, TextureFormat format, int width, int height) {
      if (format != TextureFormat.RFloat && format != TextureFormat.ARGB32)
      {
        ARLog._Error("Unsupported texture format.");
        return false;
      }

      if (width * height != pixels.Length) {
        ARLog._Error("The specified pixel buffer must match the size of the texture.");
        return false;
      }
      
      if (texture == null || texture.format != format) {
        texture = new Texture2D(width, height, format, false, false) {
          filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
        };
      }
      else if (texture.width != width || texture.height != height) {
        texture.Resize(width, height);
      }

      // Copy to texture and push to GPU
      texture.SetPixels(pixels);
      texture.Apply(false);

      // Success
      return true;
    }
  }
}
