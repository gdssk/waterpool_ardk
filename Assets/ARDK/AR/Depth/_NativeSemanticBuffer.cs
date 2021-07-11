// Copyright 2021 Niantic, Inc. All Rights Reserved.

using Niantic.ARDK.Internals;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Niantic.ARDK.AR.Awareness;

using UnityEngine;

namespace Niantic.ARDK.AR.Depth
{
  internal sealed class _NativeSemanticBuffer:
    _NativeAwarenessBufferBase<UInt16>,
    ISemanticBuffer
  {
    private string[] _channelNames;

    static _NativeSemanticBuffer()
    {
      Platform.Init();
    }

    internal _NativeSemanticBuffer
      (IntPtr nativeHandle, float worldScale, Matrix4x4 arCameraIntrinsics)
      : base
      (
        nativeHandle,
        worldScale,
        GetNativeWidth(nativeHandle),
        GetNativeHeight(nativeHandle),
        IsNativeKeyframe(nativeHandle),
        arCameraIntrinsics
      )
    {
    }

    /// <inheritdoc />
    public UInt32 ChannelCount
    {
      get
      {
        if (NativeAccess.Mode == NativeAccess.ModeType.Native)
          return _SemanticBuffer_GetNumberChannels(_nativeHandle);
        #pragma warning disable 0162
        throw new IncorrectlyUsedNativeClassException();
        #pragma warning restore 0162
      }
    }

    /// <inheritdoc />
    public string[] ChannelNames
    {
      get
      {
        if (_channelNames == null)
        {
          if (NativeAccess.Mode == NativeAccess.ModeType.Native)
          {
            IntPtr[] ptrNames = new IntPtr[ChannelCount];
            if (ChannelCount > 0 && _SemanticBuffer_GetNames(_nativeHandle, ptrNames))
            {
              _channelNames = new string[ChannelCount];
              for (int i = 0; i < ChannelCount; i++)
              {
                if (ptrNames[i] != IntPtr.Zero)
                {
                  _channelNames[i] = Marshal.PtrToStringAnsi(ptrNames[i]);
                }
              }
            }
            else
            {
              _channelNames = new string[0];
            }
          }
          else
          {
            #pragma warning disable 0162
            throw new IncorrectlyUsedNativeClassException();
            #pragma warning restore 0162
          }
        }

        return _channelNames;
      }
    }

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
      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
        return _SemanticBuffer_DoesChannelExistAt(_nativeHandle, x, y, channelIndex);
      #pragma warning disable 0162
      throw new IncorrectlyUsedNativeClassException();
      #pragma warning restore 0162
    }

    /// <inheritdoc />
    public bool DoesChannelExistAt(int x, int y, string channelName)
    {
      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
        return _SemanticBuffer_DoesChannelExistAtByName(_nativeHandle, x, y, channelName);
      #pragma warning disable 0162
      throw new IncorrectlyUsedNativeClassException();
      #pragma warning restore 0162
    }

    /// <inheritdoc />
    public bool DoesChannelExistAt(Vector2 uv, int channelIndex)
    {
      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
        return _SemanticBuffer_DoesChannelExistAtNormalised(_nativeHandle, uv.x, uv.y, channelIndex);
      #pragma warning disable 0162
      throw new IncorrectlyUsedNativeClassException();
      #pragma warning restore 0162
    }

    /// <inheritdoc />
    public bool DoesChannelExistAt(Vector2 uv, string channelName)
    {
      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
        return _SemanticBuffer_DoesChannelExistAtNormalisedByName
          (_nativeHandle, uv.x, uv.y, channelName);
      #pragma warning disable 0162
      throw new IncorrectlyUsedNativeClassException();
      #pragma warning restore 0162
    }

    /// <inheritdoc />
    public bool DoesChannelExistAt
    (
      Vector2 point,
      int viewportWidth,
      int viewportHeight,
      int channelIndex
    )
    {
      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
        return _SemanticBuffer_DoesChannelExistAtViewpoint
          (_nativeHandle, point.x, point.y, viewportWidth, viewportHeight, channelIndex);
      #pragma warning disable 0162
      throw new IncorrectlyUsedNativeClassException();
      #pragma warning restore 0162
    }

    /// <inheritdoc />
    public bool DoesChannelExistAt
    (
      Vector2 point,
      int viewportWidth,
      int viewportHeight,
      string channelName
    )
    {
      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
        return _SemanticBuffer_DoesChannelExistAtViewpointByName
          (_nativeHandle, point.x, point.y, viewportWidth, viewportHeight, channelName);
      #pragma warning disable 0162
      throw new IncorrectlyUsedNativeClassException();
      #pragma warning restore 0162
    }

    /// <inheritdoc />
    public bool DoesChannelExist(int channelIndex)
    {
      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
        return _SemanticBuffer_DoesChannelExist(_nativeHandle, channelIndex);
      #pragma warning disable 0162
      throw new IncorrectlyUsedNativeClassException();
      #pragma warning restore 0162
    }

    /// <inheritdoc />
    public bool DoesChannelExist(string channelName)
    {
      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
        return _SemanticBuffer_DoesChannelExistByName(_nativeHandle, channelName);
      #pragma warning disable 0162
      throw new IncorrectlyUsedNativeClassException();
      #pragma warning restore 0162
    }

    /// <inheritdoc />
    public bool CreateOrUpdateTexture
      (Rect croppedRect, ref Texture2D texture, TextureFormat format, int channelIndex)
    {
      uint flag = 1u << ((sizeof(Int16) * 8 - 1) - channelIndex);
      return _AwarenessBufferHelper._CreateOrUpdateTexture
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

    public ISemanticBuffer RotateToScreenOrientation()
    {

      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
      {

        var newHandle = _SemanticBuffer_RotateToScreenOrientation(_nativeHandle);

        return new _NativeSemanticBuffer(newHandle, _worldScale, ARCameraIntrinsics);
      }
      else
      {
        #pragma warning disable 0162
        throw new IncorrectlyUsedNativeClassException();
        #pragma warning restore 0162
      }
    }

    public ISemanticBuffer Interpolate
    (
      IARCamera arCamera,
      int viewportWidth,
      int viewportHeight,
      float backProjectionDistance = 0.95f
    )
    {
      var projectionMatrix =
        arCamera.CalculateProjectionMatrix
        (
          Screen.orientation,
          viewportWidth,
          viewportHeight,
          _SemanticBuffer_GetNearDistance(_nativeHandle),
          _SemanticBuffer_GetFarDistance(_nativeHandle)
        );

      var frameViewMatrix = arCamera.GetViewMatrix(Screen.orientation);
      var nativeProjectionMatrix = _UnityMatrixToNarArray(projectionMatrix);
      var nativeFrameViewMatrix = _UnityMatrixToNarArray(frameViewMatrix);

      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
      {
        var newHandle = _SemanticBuffer_Interpolate
        (
          _nativeHandle,
          nativeProjectionMatrix,
          nativeFrameViewMatrix,
          backProjectionDistance
        );

        return new _NativeSemanticBuffer(newHandle, _worldScale, arCamera.Intrinsics);
      }
      else
      {
#pragma warning disable 0162
        throw new IncorrectlyUsedNativeClassException();
#pragma warning restore 0162
      }
    }

    public ISemanticBuffer FitToViewport
    (
      int viewportWidth,
      int viewportHeight
    )
    {

      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
      {
        var newHandle = _SemanticBuffer_FitToViewport
        (
          _nativeHandle,
          viewportWidth,
          viewportHeight
        );

        return new _NativeSemanticBuffer(newHandle, _worldScale, ARCameraIntrinsics);
      }
      else
      {
        #pragma warning disable 0162
        throw new IncorrectlyUsedNativeClassException();
        #pragma warning restore 0162
      }
    }

    protected override void _InternalGetView(float[] outViewMatrix)
    {
      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
        _SemanticBuffer_GetView(_nativeHandle, outViewMatrix);
      #pragma warning disable 0162
      else
        throw new IncorrectlyUsedNativeClassException();
      #pragma warning restore 0162
    }

    protected override void _OnRelease()
    {
      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
        _SemanticBuffer_Release(_nativeHandle);
      #pragma warning disable 0162
      else
        throw new IncorrectlyUsedNativeClassException();
      #pragma warning restore 0162
    }

    protected override IntPtr _GetDataAddress()
    {
      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
        return _SemanticBuffer_GetDataAddress(_nativeHandle);
      #pragma warning disable 0162
      throw new IncorrectlyUsedNativeClassException();
      #pragma warning restore 0162
    }

    private static uint GetNativeWidth(IntPtr nativeHandle)
    {
      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
        return _SemanticBuffer_GetWidth(nativeHandle);
      #pragma warning disable 0162
      throw new IncorrectlyUsedNativeClassException();
      #pragma warning restore 0162
    }

    private static uint GetNativeHeight(IntPtr nativeHandle)
    {
      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
        return _SemanticBuffer_GetHeight(nativeHandle);
      #pragma warning disable 0162
      throw new IncorrectlyUsedNativeClassException();
      #pragma warning restore 0162
    }


    private static bool IsNativeKeyframe(IntPtr nativeHandle)
    {
      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
        return _SemanticBuffer_IsKeyframe(nativeHandle);
      #pragma warning disable 0162
      throw new IncorrectlyUsedNativeClassException();
      #pragma warning restore 0162
    }

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern IntPtr _SemanticBuffer_Release(IntPtr nativeHandle);

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern UInt32 _SemanticBuffer_GetWidth(IntPtr nativeHandle);

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern UInt32 _SemanticBuffer_GetHeight(IntPtr nativeHandle);

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern bool _SemanticBuffer_IsKeyframe(IntPtr nativeHandle);

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern void _SemanticBuffer_GetView(IntPtr nativeHandle, float[] outViewMatrix);

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern IntPtr _SemanticBuffer_GetDataAddress(IntPtr nativeHandle);

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern UInt32 _SemanticBuffer_GetNumberChannels(IntPtr nativeHandle);

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern float _SemanticBuffer_GetNearDistance(IntPtr nativeHandle);

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern float _SemanticBuffer_GetFarDistance(IntPtr nativeHandle);

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern bool _SemanticBuffer_GetNames
    (
      IntPtr nativeHandle,
      IntPtr[] outNames
    );

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern bool _SemanticBuffer_DoesChannelExistAt
    (
      IntPtr nativeHandle,
      Int32 x,
      Int32 y,
      Int32 channelIndex
    );

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern bool _SemanticBuffer_DoesChannelExistAtByName
    (
      IntPtr nativeHandle,
      Int32 x,
      Int32 y,
      string channelName
    );

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern bool _SemanticBuffer_DoesChannelExistAtNormalised
    (
      IntPtr nativeHandle,
      float u,
      float v,
      Int32 channelIndex
    );

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern bool _SemanticBuffer_DoesChannelExistAtNormalisedByName
    (
      IntPtr nativeHandle,
      float u,
      float v,
      string channelName
    );

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern bool _SemanticBuffer_DoesChannelExistAtViewpoint
    (
      IntPtr nativeHandle,
      float pointX,
      float pointY,
      Int32 viewportWidth,
      Int32 viewportHeight,
      Int32 channelIndex
    );

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern bool _SemanticBuffer_DoesChannelExistAtViewpointByName
    (
      IntPtr nativeHandle,
      float pointX,
      float pointY,
      Int32 viewportWidth,
      Int32 viewportHeight,
      string channelName
    );

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern bool _SemanticBuffer_DoesChannelExist
    (
      IntPtr nativeHandle,
      Int32 channelIndex
    );

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern bool _SemanticBuffer_DoesChannelExistByName
    (
      IntPtr nativeHandle,
      string channelName
    );

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern IntPtr _SemanticBuffer_RotateToScreenOrientation(IntPtr nativeHandle);

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern IntPtr _SemanticBuffer_Interpolate
    (
      IntPtr nativeHandle,
      float[] nativeProjectionMatrix,
      float[] nativeFrameViewMatrix,
      float backProjectionDistance
    );

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern IntPtr _SemanticBuffer_FitToViewport
    (
      IntPtr nativeHandle,
      int viewportWidth,
      int viewportHeight
    );
  }
}
