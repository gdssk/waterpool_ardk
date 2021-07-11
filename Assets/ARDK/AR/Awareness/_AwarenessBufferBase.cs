// Copyright 2021 Niantic, Inc. All Rights Reserved.

using UnityEngine;

namespace Niantic.ARDK.AR.Awareness
{
  internal abstract class _AwarenessBufferBase: IAwarenessBuffer
  {
    private float _cameraImageFocalLengthRatio;

    internal _AwarenessBufferBase
      (uint width, uint height, bool isKeyframe, Matrix4x4 arCameraIntrinsics)
    {
      var imageCenter = arCameraIntrinsics.GetColumn(3);
      var nativeCameraImageHeight = 2.0f * Mathf.Max(imageCenter.x, imageCenter.y);
      var nativeCameraImageFocalLength = arCameraIntrinsics.m11;

      // the ratio scales a height directly from the camera image height, so we can
      // figure out the change in focal length related to the height
      _cameraImageFocalLengthRatio = nativeCameraImageFocalLength / nativeCameraImageHeight;

      IsKeyframe = isKeyframe;
      Width = width;
      Height = height;
      ARCameraIntrinsics = arCameraIntrinsics;
    }

    public float FocalLength
    {
      get
      {
        return Height * _cameraImageFocalLengthRatio;
      }
    }

    public bool IsKeyframe { get; private set; }
    public uint Width { get; private set; }
    public uint Height { get; private set; }
    public Matrix4x4 ARCameraIntrinsics { get; private set; }

    public abstract Matrix4x4 ViewMatrix { get; }

    public float GetCameraFieldOfView(UnityEngine.Camera sceneCamera)
    {
      var screenHeight = Mathf.Max(sceneCamera.pixelHeight, sceneCamera.pixelWidth);
      var screenFocalLengthMatchBuffer = GetFocalLengthMatchScreen(sceneCamera);

      return
        2.0f *
        Mathf.Atan((screenHeight * 0.5f) / screenFocalLengthMatchBuffer) *
        Mathf.Rad2Deg;
    }

    /// Get the zoom scale factor from buffer
    /// @param sceneCamera Camera from scene
    /// @note Method kept internal until we figure out how to help users deal with side padding.
    private float GetZoomScaleFactor(UnityEngine.Camera sceneCamera)
    {
      var screenFocalLengthMatchCamera = GetFocalLengthMatchCamera(sceneCamera);

      var screenFocalLengthMatchBuffer =
        GetFocalLengthMatchScreen(sceneCamera);

      return screenFocalLengthMatchBuffer / screenFocalLengthMatchCamera;
    }

    /// @note Method kept private until we figure out how to help users deal with side padding.
    private float GetFocalLengthMatchScreen(UnityEngine.Camera sceneCamera)
    {
      var screenHeight = Mathf.Max(sceneCamera.pixelHeight, sceneCamera.pixelWidth);
      var screenWidth = Mathf.Min(sceneCamera.pixelHeight, sceneCamera.pixelWidth);

      var bufferAspect = Height / (float)Width;
      var screenAspect = screenHeight / screenWidth;

      if (screenAspect < bufferAspect)
      {
        //image is too narrow, we need to match width
        return screenWidth * FocalLength / Width;
      }
      else
      {
        //image is too wide, we need to match height
        return screenHeight * FocalLength / Height;
      }
    }

    /// Get the focal length from the ARCamera's focal length scaled to the height of the screen
    /// @param sceneCamera Camera from scene.
    /// @note Method kept private until we figure out how to help users deal with side padding.
    private float GetFocalLengthMatchCamera(UnityEngine.Camera sceneCamera)
    {
      var screenHeight = Mathf.Max(sceneCamera.pixelHeight, sceneCamera.pixelWidth);
      return screenHeight * _cameraImageFocalLengthRatio;
    }

    public Rect GetCroppedRect(int destinationWidth, int destinationHeight)
    {
      return GetCroppedRect((int)Width, (int)Height, destinationWidth, destinationHeight);
    }

    private Rect GetCroppedRect(int srcWidth, int srcHeight, int dstWidth, int dstHeight)
    {
      var srcRatio = srcWidth * 1f / srcHeight;
      var viewRatio = dstWidth * 1f / dstHeight;
      var croppedWidth = srcWidth;
      var croppedHeight = srcHeight;
      var startX = 0;
      var startY = 0;

      if (dstWidth > 0 && dstHeight > 0)
      {
        if (srcRatio > viewRatio)
        {
          // Source image is wider than view, crop the width
          croppedWidth = Mathf.RoundToInt(srcHeight * viewRatio);
          startX = Mathf.RoundToInt((srcWidth - croppedWidth) / 2.0f);
        }
        else
        {
          // Source image is slimmer than view, pad the width
          var pad = Mathf.RoundToInt((srcWidth - (int)(srcHeight * viewRatio)) / 2.0f);
          croppedWidth = srcWidth - 2 * pad;
          startX = pad;
        }
      }

      return new Rect(startX, startY, croppedWidth, croppedHeight);
    }
  }
}
