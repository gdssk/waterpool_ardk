// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;

using Niantic.ARDK.Utilities.BinarySerialization;
using Niantic.ARDK.Utilities.BinarySerialization.ItemSerializers;

using Niantic.ARDK.AR.Awareness;

using Unity.Collections;

using UnityEngine;

namespace Niantic.ARDK.AR.Depth
{
  internal sealed class _SerializableDepthBufferSerializer:
    _SerializableAwarenessBufferSerializer<_SerializableDepthBuffer, float>
  {
    internal static readonly _SerializableDepthBufferSerializer _instance =
      new _SerializableDepthBufferSerializer();

    private _SerializableDepthBufferSerializer()
    {
    }

    protected override void DoSerialize(BinarySerializer serializer, _SerializableDepthBuffer item)
    {
      base.DoSerialize(serializer, item);

      var floatSerializer = FloatSerializer.Instance;
      floatSerializer.Serialize(serializer, item.NearDistance);
      floatSerializer.Serialize(serializer, item.FarDistance);
    }

    protected override _SerializableDepthBuffer _InternalDeserialize
    (
      BinaryDeserializer deserializer,
      uint width,
      uint height,
      bool isKeyFrame,
      Matrix4x4 view,
      NativeArray<float> data,
      Matrix4x4 arCameraIntrinsics
    )
    {
      var floatSerializer = FloatSerializer.Instance;
      float nearDistance = floatSerializer.Deserialize(deserializer);
      float farDistance = floatSerializer.Deserialize(deserializer);
      return new _SerializableDepthBuffer
        (width, height, isKeyFrame, view, data, nearDistance, farDistance, arCameraIntrinsics);
    }
  }
}
