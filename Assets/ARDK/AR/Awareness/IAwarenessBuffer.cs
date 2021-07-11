// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;

using UnityEngine;

namespace Niantic.ARDK.AR.Awareness
{
  public interface IAwarenessBuffer
  {
    /// Focal length of the ARCamera when this buffer was generated,
    /// scaled by the height of this buffer.
    float FocalLength { get; }

    /// View matrix of the ARCamera when this buffer was generated.
    Matrix4x4 ViewMatrix { get; }

    /// True if this buffer is a keyframe (i.e. not interpolated).
    bool IsKeyframe { get; }

    /// Width of the buffer.
    UInt32 Width { get; }

    /// Height of the buffer.
    UInt32 Height { get; }

    /// Intrinsics of the ARCamera when this buffer was generated.
    Matrix4x4 ARCameraIntrinsics { get; }

    /// Get the field of view of the ARCamera when this buffer was generated, corrected to
    /// account for any difference in the scene camera's and buffer's aspect ratios.
    /// @param sceneCamera Unity camera that is rendering to the screen.
    /// @returns Corrected field of view.
    float GetCameraFieldOfView(UnityEngine.Camera sceneCamera);

    /// Get a rectangle defining how to crop the buffer's in order to match the given aspect ratio.
    /// @note
    ///   This will not crop the buffer vertically (i.e. it will only crop horizontally).
    ///   Instead, if required, blank columns will be added to the right and left sides in
    ///   order to avoid loosing data.
    /// @param destinationWidth Width of the destination viewport.
    /// @param destinationHeight Height of the destination viewport.
    /// @returns
    ///   Rectangle defining how to crop the buffer in order to match the destination aspect ratio.
    Rect GetCroppedRect(int destinationWidth, int destinationHeight);
  }
}
