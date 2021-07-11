// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;

using UnityEngine;

namespace Niantic.ARDK.AR.Awareness
{
  public interface IDepthBuffer: IDataBuffer<float>, IDisposable
  {
    /// The minimum distance from the camera (in meters) captured by this depth buffer.
    /// Depths closer in will be assigned this distance.
    float NearDistance { get; }

    /// The maximum distance from the camera (in meters) captured by this depth buffer.
    /// Depths farther out will be assigned this distance.
    float FarDistance { get; }

    /// Update (or create, if needed) a texture with this depth buffer's data.
    /// @param croppedRect
    ///   Rectangle defining how to crop the buffer's data before copying to the texture.
    /// @param texture
    ///   Reference to the texture to copy to. This method will create a texture if the reference
    ///   is null.
    /// @param format
    ///   Format of the texture.
    /// @param depthConversion
    ///   An optional function to run on each pixel of the buffer while copying it to the texture.
    ///   For calculation intensive functions, it's highly recommended to utilize the GPU instead
    ///   of this method, which will run on the CPU.
    /// @returns True if the buffer was successfully copied to the given texture.
    bool CreateOrUpdateTexture
    (
      Rect croppedRect,
      ref Texture2D texture,
      TextureFormat format,
      Func<float, float> depthConversion = null
    );

    /// Rotates the depth buffer so it is oriented to the screen
    /// @returns
    ///   A new depth buffer rotated.
    IDepthBuffer RotateToScreenOrientation();

    /// Interpolate the depth buffer using the given camera and viewport information. Since the
    /// depth buffer served by an ARFrame was likely generated using a camera image from a previous
    /// frame, always interpolate the buffer in order to get the best depth estimation.
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
    ///   in depths more accurate for closer pixels, but pixels further away will move faster
    ///   than they should. Use 0.5f if your subject in the scene is always closer than ~2 meters
    ///   from the device, and use 1.0f if your subject is further away most of the time.
    /// @returns A new IDepthBuffer with data interpolated using the camera and viewport inputs.
    IDepthBuffer Interpolate
    (
      IARCamera arCamera,
      int viewportWidth,
      int viewportHeight,
      float backProjectionDistance = 0.95f
    );

    /// Fits the depth buffer to the given dimensions.
    /// @param viewportWidth
    ///   Width of the viewport. In most cases this equals the screen resolution's width.
    /// @param viewportHeight
    ///   Height of the viewport. In most cases this equals the screen resolution's height.
    /// @returns
    ///   A new buffer sized to the given viewport dimensions,
    ///   and rotated to the screen rotation.
    IDepthBuffer FitToViewport
    (
      int viewportWidth,
      int viewportHeight
    );
  }
}
