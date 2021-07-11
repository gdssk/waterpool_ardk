// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;

using Niantic.ARDK.Utilities.Logging;

using UnityEngine;
using UnityEngine.Rendering;

using Object = UnityEngine.Object;

namespace Niantic.ARDK.AR.Depth.Effects
{
  /// This class takes a disparity texture input and generates/manipulates the vertices of a
  /// Unity mesh in order to create an occlusion effect.
  /// It has options for two modes: increased precision when occluded objects are nearer to the
  /// camera, and increased precision when occluded objects are further from the camera.
  public sealed class DepthMeshOcclusionEffect
  {
    private const string OCCLUSION_SHADER_NAME = "ARDK/Effects/DepthMeshOcclusionEffect";
    private static readonly Shader _occlusionShader = Shader.Find(OCCLUSION_SHADER_NAME);

    private static readonly int DISPARITY_TEXTURE_HANDLE = Shader.PropertyToID("_disparityTexture");
    private static readonly int SUPPRESSION_TEXTURE_HANDLE = Shader.PropertyToID("_suppressionTexture");
    private static readonly int COLOR_MASK_HANDLE = Shader.PropertyToID("_colorMask");
    private static readonly int MIN_DEPTH_HANDLE = Shader.PropertyToID("_minDepth");
    private static readonly int MAX_DEPTH_HANDLE = Shader.PropertyToID("_maxDepth");

    private CommandBuffer _currentCommandBuffer;
    private UnityEngine.Mesh _mesh;
    private float _minDepth;
    private float _maxDepth;

    private Material _occludeMaterial;

    private Texture2D _disparityTexture;
    private Texture2D _suppressionTexture;
    private short _suppressionMask;
    private UnityEngine.Camera _targetCamera;

    /// Returns whether Occlusion is currently Initialized
    public bool OcclusionInitialized { get; private set; }

    private bool _occlusionEnabled;

    /// <summary>
    /// Ennables and disables occlusion effect
    /// </summary>
    public bool OcclusionEnabled
    {
      get
      {
        return _occlusionEnabled;
      }
      set
      {
        _occlusionEnabled = value;
        ResetCommandBuffer();
      }
    }

    public enum ColorMask
    {
      None = 0, // RGBA: 0000
      Disparity = 5, // RGBA: 0101
      UV = 11, // RGBA: 1011
      All = 15, // RGBA: 1111
    }

    private ColorMask _debugColorMask = ColorMask.None;

    /// <summary>
    /// Sets the debug color mask for showing information about the occlusion mesh.
    /// Disparity values are shown in the blue channel.
    /// UVs of the disparity texture are shown in the red and green channels, respectively.
    /// </summary>
    public ColorMask DebugColorMask
    {
      get
      {
        return _debugColorMask;
      }
      set
      {
        _debugColorMask = value;
        SetColorMask();
        ResetCommandBuffer();
      }
    }

    // TODO(awetherington) : Support for multiple suppression textures?
    /// <summary>
    /// Initializes Occlusion mesh on Target Camera using DisparityTexture.
    /// </summary>
    /// <param name="targetCamera">Camera that Mesh is applied to</param>
    /// <param name="disparityTexture">Disparity Texture used to create mesh and update occlusion.</param>
    /// <param name="occlusionEnabledAtStart">Whether or not the occlusion effect is enabled immediately</param>
    /// <param name="occlusionPrecision">How to optimize occlusions</param>
    public DepthMeshOcclusionEffect
    (
      UnityEngine.Camera targetCamera,
      Texture2D disparityTexture,
      float minDepth,
      float maxDepth,
      bool occlusionEnabledAtStart,
      Texture2D suppressionTexture = null
    )
    {
      _targetCamera = targetCamera;
      _disparityTexture = disparityTexture;
      _occlusionEnabled = occlusionEnabledAtStart;
      _suppressionTexture = suppressionTexture;

      if (_disparityTexture == null)
      {
        ARLog._Error("No Disparity Texture provided. Occlusion Mesh Not Created");
        return;
      }

      if (_targetCamera == null)
      {
        ARLog._Error("No Target Camera provided. Occlusion Mesh Not Created");
        return;
      }
      _minDepth = minDepth;
      _maxDepth = maxDepth;

      SetMesh();
      ResetDepthMaterial();
      ResetCommandBuffer();
      OcclusionInitialized = true;
    }

    ~DepthMeshOcclusionEffect()
    {
      ARLog._Error
      (
        "~DepthMeshOcclusionEffect invoked. This component should be destroyed by calling Destroy()."
      );

      // TODO: Remove this call when we don't hit the LogError again. The destructor runs on a secondary thread
      // and Destroy isn't thread safe. If it works, it is "by luck", which means it probably crashes sometimes,
      // even if we don't see it.
      Destroy();
    }

    /// <summary>
    /// Destroys Occlusion Mesh and removes
    /// </summary>
    public void Destroy()
    {
      GC.SuppressFinalize(this);

      if (!OcclusionInitialized)
        return;

      RemoveCommandBuffer();

      Object.Destroy(_occludeMaterial);
      Object.Destroy(_mesh);
      OcclusionInitialized = false;
    }

    private void RemoveCommandBuffer()
    {
      if (_currentCommandBuffer != null)
      {
        if (_targetCamera != null)
          DepthMeshBufferHelper.RemoveCommandBuffer(_targetCamera, _currentCommandBuffer);
        _currentCommandBuffer.Release();
        _currentCommandBuffer = null;
      }
    }

    private void ResetCommandBuffer()
    {
      RemoveCommandBuffer();

      if (_occlusionEnabled)
      {
        _currentCommandBuffer = new CommandBuffer();
        _currentCommandBuffer.DrawMesh(_mesh, Matrix4x4.identity, _occludeMaterial);

        DepthMeshBufferHelper.AddCommandBuffer(_targetCamera, _currentCommandBuffer);
      }
    }

    private void ResetDepthMaterial()
    {
      _occludeMaterial = new Material(_occlusionShader);
      _occludeMaterial.SetFloat(MIN_DEPTH_HANDLE, _minDepth);
      _occludeMaterial.SetFloat(MAX_DEPTH_HANDLE, _maxDepth);
      _occludeMaterial.SetTexture(DISPARITY_TEXTURE_HANDLE, _disparityTexture);
      if(_suppressionTexture != null)
        _occludeMaterial.SetTexture(SUPPRESSION_TEXTURE_HANDLE, _suppressionTexture);
    }

    private void SetColorMask()
    {
      // We expect that when this is called, the occlude material has already been set.
      _occludeMaterial.SetFloat(COLOR_MASK_HANDLE, (int)_debugColorMask);
    }

    private void SetMesh()
    {
      if (_disparityTexture == null)
        return;

      var width = _disparityTexture.width;
      var height = _disparityTexture.height;

      if (_mesh == null)
      {
        _mesh = new UnityEngine.Mesh();
        _mesh.MarkDynamic();
      }

      _mesh.indexFormat = width * height >= 65534 ? IndexFormat.UInt32 : IndexFormat.UInt16;

      var numPoints = width * height;
      var vertices = new Vector3[numPoints];
      var uvs = new Vector2[numPoints];
      var numTriangles = 2 * (width - 1) * (height - 1); // just under 2 triangles per point, total

      // Map vertex indices to triangle in triplets
      var triangleIdx = new int[numTriangles * 3]; // 3 vertices per triangle
      var startIndex = 0;

      for (var i = 0; i < width * height; ++i)
      {
        var h = i / width;
        var w = i % width;
        uvs[i] = new Vector2((float)w / (width - 1), (float)h / (height - 1));

        if (h == height - 1 || w == width - 1)
          continue;

        // Triangle indices are counter-clockwise to face you
        triangleIdx[startIndex] = i;
        triangleIdx[startIndex + 1] = i + width;
        triangleIdx[startIndex + 2] = i + width + 1;
        triangleIdx[startIndex + 3] = i;
        triangleIdx[startIndex + 4] = i + width + 1;
        triangleIdx[startIndex + 5] = i + 1;
        startIndex += 6;
      }

      _mesh.vertices = vertices;
      _mesh.uv = uvs;
      _mesh.triangles = triangleIdx;
    }
  }
}
