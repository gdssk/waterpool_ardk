// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;

using UnityEngine;

namespace Niantic.ARDK.AR.Camera
{
  [Serializable]
  internal sealed class _SerializableARCamera:
    IARCamera
  {
    internal _SerializableARCamera()
    {
    }
    
    internal _SerializableARCamera
    (
      TrackingState trackingState,
      TrackingStateReason trackingStateReason,
      Resolution imageResolution,
      Resolution cpuImageResolution,
      Matrix4x4 intrinsics,
      Matrix4x4 cpuIntrinsics,
      Matrix4x4 transform,
      Matrix4x4 projectionMatrix,
      float worldScale,
      Matrix4x4 estimatedProjectionMatrix,
      Matrix4x4 estimatedViewMatrix
    )
    {
      TrackingState = trackingState;
      TrackingStateReason = trackingStateReason;
      ImageResolution = imageResolution;
      CPUImageResolution = cpuImageResolution;
      Intrinsics = intrinsics;
      CPUIntrinsics = cpuIntrinsics;
      Transform = transform;
      ProjectionMatrix = projectionMatrix;
      WorldScale = worldScale;
      _estimatedProjectionMatrix = estimatedProjectionMatrix;
      _estimatedViewMatrix = estimatedViewMatrix;
    }

    public TrackingState TrackingState { get; internal set; }
    public TrackingStateReason TrackingStateReason { get; internal set; }
    public Resolution ImageResolution { get; internal set; }
    public Resolution CPUImageResolution { get; internal set; }
    public Matrix4x4 Intrinsics { get; internal set; }
    public Matrix4x4 CPUIntrinsics { get; internal set; }
    public Matrix4x4 Transform { get; internal set; }
    public Matrix4x4 ProjectionMatrix { get; internal set; }
    public float WorldScale { get; internal set; }
    
    // TODO(grayson): maybe pass along estimated parameters to determine if we should calculate
    // new matrices?
    internal Matrix4x4 _estimatedProjectionMatrix;
    public Matrix4x4 CalculateProjectionMatrix
    (
      ScreenOrientation orientation,
      int viewportWidth,
      int viewportHeight,
      float nearClipPlane,
      float farClipPlane
    )
    {
      return _estimatedProjectionMatrix;
    }

    internal Matrix4x4 _estimatedViewMatrix;
    public Matrix4x4 GetViewMatrix(ScreenOrientation orientation)
    {
      return _estimatedViewMatrix;
    }

    public Vector2 ProjectPoint
    (
      Vector3 point,
      ScreenOrientation orientation,
      int viewportWidth,
      int viewportHeight
    )
    {
      // TODO(grayson): send message to project point and return result!
      return new Vector2();
    }
    
    void IDisposable.Dispose()
    {
      // Do nothing. This object is fully managed.
    }
  }
}
