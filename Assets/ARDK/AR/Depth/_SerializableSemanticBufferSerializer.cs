// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;

using Niantic.ARDK.Utilities.BinarySerialization;
using Niantic.ARDK.Utilities.BinarySerialization.ItemSerializers;

using Niantic.ARDK.AR.Awareness;

using Unity.Collections;

using UnityEngine;

namespace Niantic.ARDK.AR.Depth
{
  internal sealed class _SerializableSemanticBufferSerializer:
    _SerializableAwarenessBufferSerializer<_SerializableSemanticBuffer, UInt16>
  {
    internal static readonly _SerializableSemanticBufferSerializer _instance =
      new _SerializableSemanticBufferSerializer();

    private _SerializableSemanticBufferSerializer()
    {
    }

    protected override void DoSerialize
      (BinarySerializer serializer, _SerializableSemanticBuffer item)
    {
      base.DoSerialize(serializer, item);

      var uint32Serializer = CompressedUInt32Serializer.Instance;
      uint32Serializer.Serialize(serializer, item.ChannelCount);

      foreach (var name in item.ChannelNames)
      {
        StringSerializer.Instance.Serialize(serializer, name);
      }
    }

    protected override _SerializableSemanticBuffer _InternalDeserialize
    (
      BinaryDeserializer deserializer,
      uint width,
      uint height,
      bool isKeyFrame,
      Matrix4x4 view,
      NativeArray<UInt16> data,
      Matrix4x4 arCameraIntrinsics
    )
    {
      var uint32Deserializer = CompressedUInt32Serializer.Instance;
      uint channels = uint32Deserializer.Deserialize(deserializer);
      string[] names = new string[channels];
      for (int i = 0; i < channels; i++)
      {
        names[i] = StringSerializer.Instance.Deserialize(deserializer);
      }

      return new _SerializableSemanticBuffer
        (width, height, isKeyFrame, view, data, channels, names, arCameraIntrinsics);
    }
  }
}
