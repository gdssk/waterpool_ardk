// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Runtime.InteropServices;

using Niantic.ARDK.AR.Image;
using Niantic.ARDK.Internals;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using UnityEngine;

namespace Niantic.ARDK.AR
{
  internal static unsafe class _VideoStreamHelper
  {
    internal static bool _SupportsCompressing()
    {
      #pragma warning disable 0429
      return (NativeAccess.Mode == NativeAccess.ModeType.Native) ||
        (Application.platform == RuntimePlatform.OSXEditor);
      #pragma warning restore 0429
    }

    internal static bool _SupportsDecompressing()
    {
      return (Application.platform == RuntimePlatform.OSXEditor);
    }

    internal static CompressedImage _CompressForVideo
    (
      _SerializableImagePlanes planes,
      ImageFormat imageFormat,
      int compressionQuality
    )
    {
      if (!_SupportsCompressing())
        throw new Exception("This platform does not support compressing images");

      var plane0 = planes[0];
      void* buffer;
      UInt64 size;

      ROR_Encode_CameraFeed
      (
        (UInt64)plane0.PixelWidth,
        (UInt64)plane0.PixelHeight,
        NativeArrayUnsafeUtility.GetUnsafePtr(plane0.Data),
        NativeArrayUnsafeUtility.GetUnsafePtr(planes[1].Data),
        &buffer,
        &size,
        (UInt64)compressionQuality,
        (UInt32)imageFormat
      );

      var compressedImage = new CompressedImage();

      compressedImage.CompressedData = new byte[size];
      Marshal.Copy(new IntPtr(buffer), compressedImage.CompressedData, 0, (int)size);

      return compressedImage;
    }

    /// <summary>
    /// Decompresses a compressed image into it's YUV NV12 plane representation.
    /// </summary>
    /// <param name="compressedImage">The compressed image to decompress.</param>
    internal static _SerializableImagePlanes _DecompressForVideo(CompressedImage compressedImage)
    {
      if (!_SupportsDecompressing())
        throw new Exception("This platform does not support decompressing images");

      fixed (void* compressedPtr = compressedImage.CompressedData)
      {
        void* yPlane;
        void* uvPlane;
        UInt64 width;
        UInt64 height;
        ROR_Decode_CameraFeed
        (
          compressedPtr,
          (UInt64)compressedImage.CompressedData.LongLength,
          &yPlane,
          &uvPlane,
          &width,
          &height
        );

        var planes = new _SerializableImagePlane[2];

        var yPlaneData = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>
          (yPlane, (int)width * (int)height, Allocator.None);

        // UV has half width, but same data per row.
        var uvPlaneData = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>
          (uvPlane, ((int)width * (int)height) / 2, Allocator.None);
#if UNITY_EDITOR
        AtomicSafetyHandle ySafetyHandle = AtomicSafetyHandle.Create();
        AtomicSafetyHandle uvSafetyHandle = AtomicSafetyHandle.Create();

        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref yPlaneData, ySafetyHandle);
        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref uvPlaneData, uvSafetyHandle);

        planes[0] =
          new _SerializableImagePlane(yPlaneData, ySafetyHandle, (int)width, (int)height, (int)width, 1);
        planes[1] =
          new _SerializableImagePlane(uvPlaneData, uvSafetyHandle, (int)width / 2, (int)height / 2, (int)width, 2);
#else
        planes[0] =
          new _SerializableImagePlane(yPlaneData, (int)width, (int)height, (int)width, 1);
        planes[1] =
          new _SerializableImagePlane(uvPlaneData, (int)width / 2, (int)height / 2, (int)width, 2);
#endif

        return new _SerializableImagePlanes(planes);
      }
    }

    // This method only is supported when _SupportsCompressing is true.
    [DllImport(_ARDKLibrary.libraryName)]
    private static extern void ROR_Encode_CameraFeed
    (
      UInt64 width,
      UInt64 height,
      void* yPlane,
      void* uvPlane,
      void** outBuffer,
      UInt64* outSize,
      UInt64 quality,
      UInt32 imageFormat
    );

    // This method only is supported when _SupportsCompressing is false.
    [DllImport(_ARDKLibrary.libraryName)]
    public static extern void ROR_Decode_CameraFeed
    (
      void* compressedData,
      UInt64 compressedDataSize,
      void** outYPlane,
      void** outUVPlane,
      UInt64* outWidth,
      UInt64* outHeight
    );
  }
}
