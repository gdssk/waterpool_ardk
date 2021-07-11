// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;

using UnityEngine;

namespace Niantic.ARDK.AR.Awareness
{
  public interface ISemanticBuffer : IDataBuffer<UInt16>, IDisposable
  {
    /// The number of channels contained in this buffer.
    UInt32 ChannelCount { get; }

    /// An array of semantic class names, in the order their channels appear in the data.
    string[] ChannelNames { get; }

    /// Get the channel index of a specified semantic class.
    /// @param channelName Name of semantic class.
    /// @returns The index of the specified semantic class, or -1 if the channel does not exist.
    int GetChannelIndex(string channelName);

    /// Get a mask with only the specified channel's bit enabled. Can be used to quickly check if
    /// a channel exists at a particular pixel in this semantic buffer.
    /// @param channelIndex Channel index of the semantic class to mask for.
    /// @returns A mask with only the specified channel's bit enabled.
    UInt16 GetChannelTextureMask(int channelIndex);

    /// Get a mask with only the specified channels' bits enabled. Can be used to quickly check if
    /// a set of channels exists at a particular pixel in this semantic buffer.
    /// @param channelIndices Channel indices of the semantic classes to mask for.
    /// @returns A mask with only the specified channels' bits enabled.
    UInt16 GetChannelTextureMask(int[] channelIndices);

    /// Get a mask with only the specified channel's bit enabled. Can be used to quickly check if
    /// a channel exists at a particular pixel in this semantic buffer.
    /// @param channelName Name of the semantic class to mask for.
    /// @returns A mask with only the specified channel's bit enabled.
    UInt16 GetChannelTextureMask(string channelName);

    /// Get a mask with only the specified channels' bits enabled. Can be used to quickly check if
    /// a set of channels exists at a particular pixel in this semantic buffer.
    /// @param channelNames Names of the semantic classes to mask for.
    /// @returns A mask with only the specified channels' bits enabled.
    UInt16 GetChannelTextureMask(string[] channelNames);

    /// Check if a pixel in this semantic buffer contains a certain channel.
    /// @param x Pixel position on the x-axis.
    /// @param y Pixel position on the y-axis.
    /// @param channelIndex Channel index of the semantic class to look for.
    /// @returns True if the channel exists at the given coordinates.
    bool DoesChannelExistAt(int x, int y, int channelIndex);

    /// Check if a pixel in this semantic buffer contains a certain channel.
    /// @param x Pixel position on the x-axis.
    /// @param y Pixel position on the y-axis.
    /// @param channelName Name of the semantic class to look for.
    /// @returns True if the channel exists at the given coordinates.
    bool DoesChannelExistAt(int x, int y, string channelName);

    /// Check if a pixel in this semantic buffer contains a certain channel.
    /// This method samples the semantics buffer using normalised texture coordinates.
    /// @param uv Normalised texture coordinates. The bottom-left is (0,1); the top-right is (1,0).
    /// @channelIndex Channel index of the semantic class to look for.
    /// @returns True if the channel exists at the given coordinates.
    bool DoesChannelExistAt(Vector2 uv, int channelIndex);

    /// Check if a pixel in this semantic buffer contains a certain channel.
    /// This method samples the semantics buffer using normalised texture coordinates.
    /// @param uv Normalised texture coordinates. The bottom-left is (0,1); the top-right is (1,0).
    /// @channelName Name of the semantic class to look for.
    /// @returns True if the channel exists at the given coordinates.
    bool DoesChannelExistAt(Vector2 uv, string channelName);

    /// Check if a pixel in this semantic buffer contains a certain channel.
    /// This method samples the semantics buffer using normalised viewport coordinates.
    /// @param point
    ///   Normalised viewport coordinates. The bottom-left is (0,0); the top-right is (1,1).
    /// @param viewportWidth
    ///   Width of the viewport. In most cases this equals to the rendering camera's pixel width.
    /// @param viewportHeight
    ///   Height of the viewport. In most cases this equals to the rendering camera's pixel height.
    /// @channelIndex Channel index of the semantic class to look for.
    /// @returns True if the channel exists at the given coordinates.
    bool DoesChannelExistAt(Vector2 point, int viewportWidth, int viewportHeight, int channelIndex);

    /// Check if a pixel in this semantic buffer contains a certain channel.
    /// This method samples the semantics buffer using normalised viewport coordinates.
    /// @param point
    ///   Normalised viewport coordinates. The bottom-left is (0,0); the top-right is (1,1).
    /// @param viewportWidth
    ///   Width of the viewport. In most cases this equals to the rendering camera's pixel width.
    /// @param viewportHeight
    ///   Height of the viewport. In most cases this equals to the rendering camera's pixel height.
    /// @channelName Name of the semantic class to look for.
    /// @returns True if the channel exists at the given coordinates.
    bool DoesChannelExistAt(Vector2 point, int viewportWidth, int viewportHeight, string channelName);

    /// Check if a certain channel exists anywhere in this buffer.
    /// @channelIndex Channel index of the semantic class to look for.
    /// @returns True if the channel exists.
    bool DoesChannelExist(int channelIndex);

    /// Check if a certain channel exists anywhere in this buffer.
    /// @channelName Name of the semantic class to look for.
    /// @returns True if the channel exists.
    bool DoesChannelExist(string channelName);

    /// Update (or create, if needed) a texture with this data of one of this buffer's channels.
    /// @param croppedRect
    ///   Rectangle defining how to crop the buffer's data before copying to the texture.
    /// @param texture
    ///   Reference to the texture to copy to. This method will create a texture if the reference
    ///   is null.
    /// @param format
    ///   Format of the texture.
    /// @param channelIndex
    ///   Channel index of the semantic class to copy.
    /// @returns True if the buffer was successfully copied to the given texture.
    bool CreateOrUpdateTexture
      (Rect croppedRect, ref Texture2D texture, TextureFormat format, int channelIndex);


    /// Rotates the semantic buffer so it is oriented to the screen
    /// @returns
    ///   A new semantic buffer rotated.
    ISemanticBuffer RotateToScreenOrientation();

    /// Interpolate the semantic buffer using the given camera and viewport information. Since the
    /// semantic buffer served by an ARFrame was likely generated using a camera image from a previous
    /// frame, always interpolate the buffer in order to get the best semantic segmentation output.
    /// @param arCamera
    ///   ARCamera with the pose to interpolate this buffer to.
    /// @param viewportWidth
    ///   Width of the viewport. In most cases this equals to the rendering camera's pixel width.
    ///   This is used to calculate the new projection matrix.
    /// @param viewportHeight
    ///   Height of the viewport. In most cases this equals to the rendering camera's pixel height.
    ///   This is used to calculate the new projection matrix.
    /// @param backProjectionDistance
    ///   This value sets the normalized distance of the back-projection plane. Lower values result
    ///   in outputs more accurate for closer pixels, but pixels further away will move faster
    ///   than they should. Use 0.5f if your subject in the scene is always closer than ~2 meters
    ///   from the device, and use 1.0f if your subject is further away most of the time.
    /// @returns A new semantic buffer with data interpolated using the camera and viewport inputs.
    ISemanticBuffer Interpolate
    (
      IARCamera arCamera,
      int viewportWidth,
      int viewportHeight,
      float backProjectionDistance = 0.95f
    );

    /// Sizes the semantic buffer to the given dimensions.
    /// @param viewportWidth
    ///   Width of the viewport. In most cases this equals to the rendering camera's pixel width.
    /// @param viewportHeight
    ///   Height of the viewport. In most cases this equals to the rendering camera's pixel height.
    /// @returns
    ///   A new buffer sized to the given viewport dimensions,
    ///   and rotated to the screen rotation
    ISemanticBuffer FitToViewport
    (
      int viewportWidth,
      int viewportHeight
    );
  }
}
