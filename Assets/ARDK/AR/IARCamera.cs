// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using UnityEngine;

namespace Niantic.ARDK.AR
{
  /// <summary>
  /// Interface for platform specific implementations of an ARCamera. Contains information about the
  /// camera position and imaging characteristics for a captured video frame in an AR session.
  /// </summary>
  public interface IARCamera:
    IDisposable
  {
    /// <summary>
    /// The general quality of position tracking available when the camera captured a frame.
    /// @remarks Use this property to keep your users informed as well as internally for peerState 
    /// and whether to process the enclosed data or not.
    /// </summary>
    TrackingState TrackingState { get; }

    /// <summary>
    /// A possible diagnosis for limited position tracking quality as of when the camera 
    /// captured a frame.
    /// @note This is an iOS-only value.
    /// </summary>
    TrackingStateReason TrackingStateReason { get; }

    /// <summary>
    /// The width and height of the captured image, in pixels.
    /// </summary>
    Resolution ImageResolution { get; }

    /// <summary>
    /// The resolution of the CPU captured image, in pixels.
    /// </summary>
    Resolution CPUImageResolution { get; }

    /// <summary>
    /// @brief A matrix that converts between the 2D camera plane and 3D world coordinate space.
    /// @remarks Takes the form, where (fx, fy) is the pixel focal length and (ox, oy) is the 
    /// principal point location:
    /// ```
    /// | fx 0  0 ox |
    /// | 0  fy 0 oy |
    /// | 0  0  1 0  |
    /// | 0  0  0 1  |
    /// ```
    /// </summary>
    Matrix4x4 Intrinsics { get; }

    /// <summary>
    /// @brief A matrix that converts between the 2D camera plane and 3D world coordinate space for the 
    /// CPU image.
    /// @remarks Takes the form, where (fx, fy) is the pixel focal length and (ox, oy) is the 
    /// principal point location:
    /// ```
    /// | fx 0  0 ox |
    /// | 0  fy 0 oy |
    /// | 0  0  1 0  |
    /// | 0  0  0 1  |
    /// ```
    /// </summary>
    Matrix4x4 CPUIntrinsics { get; }

    /// <summary>
    /// The position and orientation of the camera in world coordinate space.
    /// @remark This transform's coordinate space is always constant relative to the device 
    /// orientation.
    /// </summary>
    Matrix4x4 Transform { get; }

    /// <summary>
    /// A transform matrix appropriate for rendering 3D content to match the image captured by 
    /// the camera.
    /// @remark This property is equivalent to Camera.CalculateProjectionMatrix called with the 
    /// captured image's properties and default zNear and zFar of 0.001 and 1000.0, respectively.
    /// </summary>
    Matrix4x4 ProjectionMatrix { get; }

    /// <summary>
    /// The scaling factor applied to this camera's Transform.
    /// </summary>
    float WorldScale { get; }

    /// <summary>
    /// @brief Returns a transform matrix appropriate for rendering 3D content to match the 
    /// image captured by the camera, using the specified parameters.
    /// @param orientation The current orientation of the interface.
    /// @param viewportWidth Viewport width, in pixels.
    /// @param viewportHeight Viewport height, in pixels.
    /// @param nearClipPlane Near clip plane, in meters.
    /// @param farClipPlane Far clip plane, in meters.
    /// @note Returns a pre-calculated value in Remote Debugging.
    /// </summary>
    Matrix4x4 CalculateProjectionMatrix
    (
      ScreenOrientation orientation,
      int viewportWidth,
      int viewportHeight,
      float nearClipPlane,
      float farClipPlane
    );

    /// <summary>
    /// @brief Returns a transform matrix for converting from world space to camera space.
    /// @param orientation The current orientation of the interface.
    /// @note Returns a pre-calculated value in Remote Debugging.
    /// </summary>
    Matrix4x4 GetViewMatrix(ScreenOrientation orientation);

    /// <summary>
    /// @brief Returns the projection of a point from the 3D world space detected by the session into 
    /// the 2D space of a view rendering the scene.
    /// @param point A point in 3D world space.
    /// @param orientation The current orientation of the interface.
    /// @param viewportWidth Viewport width, in pixels.
    /// @param viewportHeight Viewport height, in pixels.
    /// @note This is an iOS-only method.
    /// @note Not currently supported in Remote Debugging.
    /// </summary>
    Vector2 ProjectPoint
    (
      Vector3 point,
      ScreenOrientation orientation,
      int viewportWidth,
      int viewportHeight
    );
  }
}
