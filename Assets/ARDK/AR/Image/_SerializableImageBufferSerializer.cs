// Copyright 2021 Niantic, Inc. All Rights Reserved.

using Niantic.ARDK.Utilities.BinarySerialization;
using Niantic.ARDK.Utilities.BinarySerialization.ItemSerializers;

using UnityEngine;

namespace Niantic.ARDK.AR.Image
{
  internal sealed class _SerializableImageBufferSerializer:
    BaseItemSerializer<_SerializableImageBuffer>
  {
    internal static readonly _SerializableImageBufferSerializer _instance =
      new _SerializableImageBufferSerializer();

    private _SerializableImageBufferSerializer()
    {
    }
    protected override void DoSerialize(BinarySerializer serializer, _SerializableImageBuffer item)
    {
      EnumSerializer.ForType<ImageFormat>().Serialize(serializer, item.Format);

      var compressionLevel = item.CompressionLevel;
      CompressedInt32Serializer.Instance.Serialize(serializer, compressionLevel);

      if (compressionLevel == 0)
        serializer.Serialize(item.Planes);
      else
      {
        var compressedImage =
          _VideoStreamHelper._CompressForVideo(item.Planes, item.Format, compressionLevel);
        
        serializer.Serialize(compressedImage);
      }
    }

    protected override _SerializableImageBuffer DoDeserialize(BinaryDeserializer deserializer)
    {
      var format = EnumSerializer.ForType<ImageFormat>().Deserialize(deserializer);
      int compressionLevel = CompressedInt32Serializer.Instance.Deserialize(deserializer);
      var planesOrCompressed = deserializer.Deserialize();

      _SerializableImagePlanes planes;
      if (compressionLevel == 0)
        planes = (_SerializableImagePlanes)planesOrCompressed;
      else
      {
        var compressedImage = (CompressedImage)planesOrCompressed; 
        planes = _VideoStreamHelper._DecompressForVideo(compressedImage);
      }

      return new _SerializableImageBuffer(format, planes, compressionLevel);
    }
  }
}
