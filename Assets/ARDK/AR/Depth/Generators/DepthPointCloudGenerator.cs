// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Runtime.InteropServices;

using Niantic.ARDK.Utilities;
using Niantic.ARDK.AR.Awareness;
using Niantic.ARDK.Utilities.Logging;

using UnityEngine;

namespace Niantic.ARDK.AR.Depth.Generators
{
  /// This class takes in a frame of DepthBuffer raw data and generates a point cloud based
  /// on it and the camera intrinsics.
  public sealed class DepthPointCloudGenerator:
    IDisposable
  {
    private const string GENERATOR_SHADER_NAME = "DepthPointCloudGenerator";

    private static readonly ComputeShader _pointCloudShader =
      (ComputeShader)Resources.Load(GENERATOR_SHADER_NAME);

    private const string KERNEL_NAME = "Generate";

#region Shader Handles
    private static readonly int DEPTH_BUFFER_WIDTH_HANDLE =
      Shader.PropertyToID("DepthBufferWidth");

    private static readonly int MIN_DEPTH_HANDLE =
      Shader.PropertyToID("MinDepth");

    private static readonly int MAX_DEPTH_HANDLE =
      Shader.PropertyToID("MaxDepth");

    private static readonly int VERTICAL_OFFSET_HANDLE =
      Shader.PropertyToID("VerticalOffsetPerMeter");

    private static readonly int UPPER_LEFT_NEAR_HANDLE =
      Shader.PropertyToID("UpperLeftNear");

    private static readonly int UPPER_LEFT_FAR_HANDLE =
      Shader.PropertyToID("UpperLeftFar");

    private static readonly int DELTA_X_NEAR_HANDLE =
      Shader.PropertyToID("DeltaXNear");

    private static readonly int DELTA_X_FAR_HANDLE =
      Shader.PropertyToID("DeltaXFar");

    private static readonly int DELTA_Y_NEAR_HANDLE =
      Shader.PropertyToID("DeltaYNear");

    private static readonly int DELTA_Y_FAR_HANDLE =
      Shader.PropertyToID("DeltaYFar");

    private static readonly int POINT_CLOUD_HANDLE =
      Shader.PropertyToID("PointCloud");

    private static readonly int DEPTH_HANDLE =
      Shader.PropertyToID("Depth");
#endregion

    [Serializable]
    public sealed class Settings
    {
      /// This is a fixup scale value that pulls points downward larger amounts as they go further
      /// from the camera's position. At the present it is necessary to compensate for the fact that
      /// current models believe the floor to be a large bowl, with you standing in the centermost,
      /// deepest part.
      public float VerticalOffsetPerMeter = -0.025f;

      public Settings Copy()
      {
        return
          new Settings
          {
            VerticalOffsetPerMeter = this.VerticalOffsetPerMeter
          };
      }
    }

    private Settings _settings;
    private int _kernel;

    private uint _kernelThreadsX;
    private uint _kernelThreadsY;

    private ComputeBuffer _pointCloudBuffer;
    private ComputeBuffer _depthComputeBuffer;
    private Vector3[] _pointCloud;

    /// The output of this class. Contains a point for every point on the DepthBuffer, stored
    /// as a flat two-dimensional array.
    public Vector3[] PointCloud
    {
      get { return _pointCloud; }
      private set { _pointCloud = value; }
    }

    /// Constructs a new generator.
    /// @param settings User-controlled settings specific to this generator. Cached.
    /// @param depthBuffer Depth buffer used to initialize the generator. Not cached.
    public DepthPointCloudGenerator(Settings settings, IDepthBuffer rawDepthBuffer)
    {
      var depthBuffer = rawDepthBuffer.RotateToScreenOrientation();

      _settings = settings;
      _kernel = _pointCloudShader.FindKernel(KERNEL_NAME);

      uint kernelThreadsZ;
      _pointCloudShader.GetKernelThreadGroupSizes(
          _kernel,
          out _kernelThreadsX,
          out _kernelThreadsY,
          out kernelThreadsZ
        );

      // Settings constant parameters
      _pointCloudShader.SetInt(DEPTH_BUFFER_WIDTH_HANDLE, (int)depthBuffer.Width);
      _pointCloudShader.SetFloat(VERTICAL_OFFSET_HANDLE, _settings.VerticalOffsetPerMeter);
      _pointCloudShader.SetFloat(MIN_DEPTH_HANDLE, depthBuffer.NearDistance);
      _pointCloudShader.SetFloat(MAX_DEPTH_HANDLE, depthBuffer.FarDistance);

      // Setting up input data buffer
      _depthComputeBuffer =
        new ComputeBuffer
        (
          (int)(depthBuffer.Width * depthBuffer.Height),
          Marshal.SizeOf(typeof(float))
        );

      _pointCloudShader.SetBuffer(_kernel, DEPTH_HANDLE, _depthComputeBuffer);

      // Setting up output data buffer
      _pointCloudBuffer =
        new ComputeBuffer
        (
          (int)(depthBuffer.Width * depthBuffer.Height),
          Marshal.SizeOf(typeof(Vector3))
        );

      _pointCloud = new Vector3[depthBuffer.Width * depthBuffer.Height];
      _pointCloudBuffer.SetData(_pointCloud);
      _pointCloudShader.SetBuffer(_kernel, POINT_CLOUD_HANDLE, _pointCloudBuffer);
    }

    ~DepthPointCloudGenerator()
    {
      ARLog._Error("DepthPointCloudGenerator must be released by calling Dispose().");
    }

    public void Dispose()
    {
      GC.SuppressFinalize(this);

      var depthBuffer = _depthComputeBuffer;
      if (depthBuffer != null)
      {
        _depthComputeBuffer = null;
        depthBuffer.Release();
      }

      var pointCloudBuffer = _pointCloudBuffer;
      if (pointCloudBuffer != null)
      {
        _pointCloudBuffer = null;
        pointCloudBuffer.Release();
      }
    }

    /// <summary>
    /// Uses the compute shaders to generate the a point cloud from the depth image. Each pixel
    /// in the depth image will be turned into a 3d point in world space (defined by the
    /// inverseViewMat and the focal length). Each 3d point can optionally be categorized.
    /// </summary>
    /// @param depthBuffer A depth buffer with which to generate a point cloud
    /// @returns A point cloud based on the depth buffer
    public Vector3[] GeneratePointCloud(IDepthBuffer rawDepthBuffer)
    {
      var depthBuffer = rawDepthBuffer.RotateToScreenOrientation();

      var inverseViewMat = depthBuffer.ViewMatrix.inverse;
      Vector3 upperLeftNear, xDeltaNear, yDeltaNear;

      CreateCornerPointAndDeltas
      (
        out upperLeftNear,
        out xDeltaNear,
        out yDeltaNear,
        (int)depthBuffer.Width,
        (int)depthBuffer.Height,
        depthBuffer.FocalLength,
        inverseViewMat,
        depthBuffer.NearDistance
      );

      Vector3 upperLeftFar;
      Vector3 xDeltaFar;
      Vector3 yDeltaFar;

      CreateCornerPointAndDeltas
      (
        out upperLeftFar,
        out xDeltaFar,
        out yDeltaFar,
        (int)depthBuffer.Width,
        (int)depthBuffer.Height,
        depthBuffer.FocalLength,
        inverseViewMat,
        depthBuffer.FarDistance
      );

      // set the reference data for this frame
      _pointCloudShader.SetVector(UPPER_LEFT_NEAR_HANDLE, upperLeftNear);
      _pointCloudShader.SetVector(DELTA_X_NEAR_HANDLE, xDeltaNear);
      _pointCloudShader.SetVector(DELTA_Y_NEAR_HANDLE, yDeltaNear);

      _pointCloudShader.SetVector(UPPER_LEFT_FAR_HANDLE, upperLeftFar);
      _pointCloudShader.SetVector(DELTA_X_FAR_HANDLE, xDeltaFar);
      _pointCloudShader.SetVector(DELTA_Y_FAR_HANDLE, yDeltaFar);

      // update the depth image
      _depthComputeBuffer.SetData(depthBuffer.Data);

      var threadGroupX = (int)Mathf.Max((float)depthBuffer.Width / _kernelThreadsX, 1.0f);
      var threadGroupY = (int)Mathf.Max((float)depthBuffer.Height / _kernelThreadsY, 1.0f);

      // TODO : Profile this Dispatch & Get. Maybe we can avoid having the main thread wait?
      // calculate the point cloud
      _pointCloudShader.Dispatch(_kernel, threadGroupX, threadGroupY, 1);

      // copy the point cloud into the local buffer
      _pointCloudBuffer.GetData(_pointCloud);

      return _pointCloud;
    }

    /// Helper method to create a world space position of a viewport coordinate. This is
    /// different than the stock unity method because it uses focal length instead of a
    /// perspective matrix. The provided distance is interpreted as perpendicular to the view
    /// plane (parallel to the view direction).
    /// @param invCamMat Inverse of the camera matrix
    /// @param coord normalized (0-1) screen coordinate where 0,0 is lower left
    /// @param width width of viewport in pixels
    /// @param height height of viewport in pixels
    /// @param focalLength focal length in pixels
    /// @param distance view direction distance of output point in meters
    /// @returns world position corresponding to the image coordinate
    private Vector3 ImageCoordToWorldPosition
    (
      Matrix4x4 invCamMat,
      Vector2 coord,
      float width,
      float height,
      float focalLength,
      float distance
    )
    {
      var x = (Vector3)invCamMat.GetColumn(0);
      var y = (Vector3)invCamMat.GetColumn(1);
      var z = (Vector3)invCamMat.GetColumn(2);
      var wp = (Vector3)invCamMat.GetColumn(3);

      coord.x = width * (coord.x - 0.5f);
      coord.y = height * (coord.y - 0.5f);
      Vector3 result = wp + z * distance;

      float scale = distance / focalLength;

      result += x * (scale * coord.x);
      result += y * (scale * coord.y);

      return result;
    }

    /// Utility method to calculate the values needed by the compute shader when it generates the
    /// point cloud. It generates the upper left corner of the viewport in world space, along with
    /// x and y deltas that represent the distance in world space between pixels in the viewport.
    /// The values are all calculated from the camera position at a supplided distance that is
    /// perpendicular to the view plane (parallel to the camera view direction)
    /// @param upperLeftCorner The upper left corner of the viewport inworld space
    /// @param xDelta World space "width" of a depth pixel at this depth
    /// @param yDelta World space "height" of a depth pixel at this depth
    /// @param imageWidth Width of the depth image
    /// @param imageHeight Height of the depth image
    /// @param focalLength Focal length of the depth buffer
    /// @param invCamMat Inverse of the camera matrix
    /// @param depth Depth at which to generate world point info
    private void CreateCornerPointAndDeltas
    (
      out Vector3 upperLeftCorner,
      out Vector3 xDelta,
      out Vector3 yDelta,
      int imageWidth,
      int imageHeight,
      float focalLength,
      Matrix4x4 invCamMat,
      float depth
    )
    {
      upperLeftCorner =
        ImageCoordToWorldPosition
        (
          invCamMat,
          Vector2.up,
          imageWidth,
          imageHeight,
          focalLength,
          depth
        );

      var lowerLeft =
        ImageCoordToWorldPosition
        (
          invCamMat,
          Vector2.zero,
          imageWidth,
          imageHeight,
          focalLength,
          depth
        );

      var upperRight =
        ImageCoordToWorldPosition
        (
          invCamMat,
          Vector2.one,
          imageWidth,
          imageHeight,
          focalLength,
          depth
        );

      xDelta = (upperRight - upperLeftCorner) / (float)imageWidth;
      yDelta = (lowerLeft - upperLeftCorner) / (float)imageHeight;
    }
  }
}
