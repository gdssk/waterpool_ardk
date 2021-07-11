// Copyright 2021 Niantic, Inc. All Rights Reserved.

using Niantic.ARDK.Internals;

using System;
using System.Runtime.InteropServices;

using Niantic.ARDK.AR.Awareness;

using UnityEngine;

namespace Niantic.ARDK.AR.Depth
{
  internal sealed class _NativeDepthBuffer:
    _NativeAwarenessBufferBase<float>,
    IDepthBuffer
  {
    static _NativeDepthBuffer()
    {
      Platform.Init();
    }

    internal _NativeDepthBuffer(IntPtr nativeHandle, float worldScale, Matrix4x4 arCameraIntrinsics)
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

    public float NearDistance
    {
      get
      {
        if (NativeAccess.Mode == NativeAccess.ModeType.Native)
          return _DepthBuffer_GetNearDistance(_nativeHandle);
        #pragma warning disable 0162
        throw new IncorrectlyUsedNativeClassException();
        #pragma warning restore 0162
      }
    }

    public float FarDistance
    {
      get
      {
        if (NativeAccess.Mode == NativeAccess.ModeType.Native)
          return _DepthBuffer_GetFarDistance(_nativeHandle);
        #pragma warning disable 0162
        throw new IncorrectlyUsedNativeClassException();
        #pragma warning restore 0162
      }
    }

    public IDepthBuffer RotateToScreenOrientation()
    {

      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
      {

        var newHandle = _DepthBuffer_RotateToScreenOrientation(_nativeHandle);

        return new _NativeDepthBuffer(newHandle, _worldScale, ARCameraIntrinsics);
      }
      else
      {
        #pragma warning disable 0162
        throw new IncorrectlyUsedNativeClassException();
        #pragma warning restore 0162
      }
    }

    public IDepthBuffer Interpolate
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
          NearDistance,
          FarDistance
        );

      var frameViewMatrix = arCamera.GetViewMatrix(Screen.orientation);
      var nativeProjectionMatrix = _UnityMatrixToNarArray(projectionMatrix);
      var nativeFrameViewMatrix = _UnityMatrixToNarArray(frameViewMatrix);

      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
      {
        var newHandle = _DepthBuffer_Interpolate
        (
          _nativeHandle,
          nativeProjectionMatrix,
          nativeFrameViewMatrix,
          backProjectionDistance
        );

       return new _NativeDepthBuffer(newHandle, _worldScale, arCamera.Intrinsics);
      }
      else
      {
        #pragma warning disable 0162
        throw new IncorrectlyUsedNativeClassException();
        #pragma warning restore 0162
      }
    }

    public IDepthBuffer FitToViewport
    (
      int viewportWidth,
      int viewportHeight
    )
    {

      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
      {

        var newHandle = _DepthBuffer_FitToViewport
        (
          _nativeHandle,
          viewportWidth,
          viewportHeight
        );

        return new _NativeDepthBuffer(newHandle, _worldScale, ARCameraIntrinsics);
      }
      else
      {
        #pragma warning disable 0162
        throw new IncorrectlyUsedNativeClassException();
        #pragma warning restore 0162
      }
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
          (int)Width, // TODO change this back to uint
          (int)Height,
          croppedRect,
          ref texture,
          format,
          true,
          depthConversion
        );
    }

    protected override void _InternalGetView(float[] outViewMatrix)
    {
      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
        _DepthBuffer_GetView(_nativeHandle, outViewMatrix);
      #pragma warning disable 0162
      else
        throw new IncorrectlyUsedNativeClassException();
      #pragma warning restore 0162
    }

    protected override void _OnRelease()
    {
        if (NativeAccess.Mode == NativeAccess.ModeType.Native)
          _DepthBuffer_Release(_nativeHandle);
        #pragma warning disable 0162
        else
          throw new IncorrectlyUsedNativeClassException();
        #pragma warning restore 0162
    }

    protected override IntPtr _GetDataAddress()
    {
      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
        return _DepthBuffer_GetDataAddress(_nativeHandle);
      #pragma warning disable 0162
      throw new IncorrectlyUsedNativeClassException();
      #pragma warning restore 0162
    }

    private static uint GetNativeWidth(IntPtr nativeHandle)
    {
      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
        return _DepthBuffer_GetWidth(nativeHandle);
      #pragma warning disable 0162
      throw new IncorrectlyUsedNativeClassException();
      #pragma warning restore 0162
    }

    private static uint GetNativeHeight(IntPtr nativeHandle)
    {
      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
        return _DepthBuffer_GetHeight(nativeHandle);
      #pragma warning disable 0162
      throw new IncorrectlyUsedNativeClassException();
      #pragma warning restore 0162
    }


    private static bool IsNativeKeyframe(IntPtr nativeHandle)
    {
      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
        return _DepthBuffer_IsKeyframe(nativeHandle);
      #pragma warning disable 0162
      throw new IncorrectlyUsedNativeClassException();
      #pragma warning restore 0162
    }

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern IntPtr _DepthBuffer_Release(IntPtr nativeHandle);

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern UInt32 _DepthBuffer_GetWidth(IntPtr nativeHandle);

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern UInt32 _DepthBuffer_GetHeight(IntPtr nativeHandle);

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern bool _DepthBuffer_IsKeyframe(IntPtr nativeHandle);

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern void _DepthBuffer_GetView(IntPtr nativeHandle, float[] outViewMatrix);

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern IntPtr _DepthBuffer_GetDataAddress(IntPtr nativeHandle);

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern float _DepthBuffer_GetNearDistance(IntPtr nativeHandle);

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern float _DepthBuffer_GetFarDistance(IntPtr nativeHandle);

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern IntPtr _DepthBuffer_RotateToScreenOrientation(IntPtr nativeHandle);

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern IntPtr _DepthBuffer_Interpolate
    (
      IntPtr nativeHandle,
      float[] nativeProjectionMatrix,
      float[] nativeFrameViewMatrix,
      float backProjectionDistance
    );

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern IntPtr _DepthBuffer_FitToViewport
    (
      IntPtr nativeHandle,
      int viewportWidth,
      int viewportHeight
    );
  }
}
