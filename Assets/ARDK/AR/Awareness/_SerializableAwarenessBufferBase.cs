// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.IO;

using Unity.Collections;

using UnityEngine;

namespace Niantic.ARDK.AR.Awareness
{
  // Can't use [Serializable]. Need to provide a serializer.
  internal abstract class _SerializableAwarenessBufferBase<T>
    : _AwarenessBufferBase,
      IDataBuffer<T>
  where T: struct
  {
    private readonly Matrix4x4 _viewMatrix;

    internal _SerializableAwarenessBufferBase
    (
      uint width,
      uint height,
      bool isKeyframe,
      Matrix4x4 viewMatrix,
      NativeArray<T> data,
      Matrix4x4 arCameraIntrinsics
    )
      : base(width, height, isKeyframe, arCameraIntrinsics)
    {
      _viewMatrix = viewMatrix;
      Data = data;
    }

    ~_SerializableAwarenessBufferBase()
    {
      Dispose();
    }

    public override Matrix4x4 ViewMatrix
    {
      get
      {
        return _viewMatrix;
      }
    }

    public NativeArray<T> Data { get; }

    public void Dispose()
    {
      GC.SuppressFinalize(this);

      if (Data.IsCreated)
        Data.Dispose();
    }
  }
}
