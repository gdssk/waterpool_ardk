// Copyright 2021 Niantic, Inc. All Rights Reserved.

using Niantic.ARDK.Utilities.BinarySerialization;
using Niantic.ARDK.Utilities.BinarySerialization.ItemSerializers;

using Unity.Collections;

using UnityEngine;

namespace Niantic.ARDK.AR.Awareness
{
  internal abstract class _SerializableAwarenessBufferSerializer<TBuffer, T>:
    BaseItemSerializer<TBuffer>
    where TBuffer: _SerializableAwarenessBufferBase<T>
    where T: struct
  {
    protected override void DoSerialize
      (BinarySerializer serializer, TBuffer item)
    {
      var uint32Serializer = CompressedUInt32Serializer.Instance;
      Matrix4x4Serializer.Instance.Serialize(serializer, item.ARCameraIntrinsics);
      uint32Serializer.Serialize(serializer, item.Width);
      uint32Serializer.Serialize(serializer, item.Height);
      BooleanSerializer.Instance.Serialize(serializer, item.IsKeyframe);
      Matrix4x4Serializer.Instance.Serialize(serializer, item.ViewMatrix);
      NativeArraySerializer<T>.Instance.Serialize(serializer, item.Data);
    }

    protected override TBuffer DoDeserialize
      (BinaryDeserializer deserializer)
    {
      var uint32Serializer = CompressedUInt32Serializer.Instance;
      var arCameraIntrinsics = Matrix4x4Serializer.Instance.Deserialize(deserializer);
      uint width = uint32Serializer.Deserialize(deserializer);
      uint height = uint32Serializer.Deserialize(deserializer);
      var isKeyFrame = BooleanSerializer.Instance.Deserialize(deserializer);
      var viewMatrix = Matrix4x4Serializer.Instance.Deserialize(deserializer);
      var data = NativeArraySerializer<T>.Instance.Deserialize(deserializer);

      return _InternalDeserialize
        (deserializer, width, height, isKeyFrame, viewMatrix, data, arCameraIntrinsics);
    }

    protected abstract TBuffer _InternalDeserialize
    (
      BinaryDeserializer deserializer,
      uint width,
      uint height,
      bool isKeyFrame,
      Matrix4x4 view,
      NativeArray<T> data,
      Matrix4x4 arCameraIntrinsics
    );
  }
}
