// Copyright 2021 Niantic, Inc. All Rights Reserved.

using Niantic.ARDK.AR.Depth.Effects;
using Niantic.ARDK.Internals.EditorUtilities;
using Niantic.ARDK.Utilities.Logging;

using UnityEngine;

namespace Niantic.ARDK.Extensions.Depth
{
  /// <summary>
  /// This helper can be placed in a scene to easily add occlusions, with minimal setup time. It
  /// reads synchronized depth output from ARFrame, and feeds it into an DepthMeshOcclusionEffect
  /// that then performs the actual shader occlusion. Both precision options of
  /// DepthMeshOcclusionEffect are available, and can be toggled between.
  /// </summary>
  [RequireComponent(typeof(ARDepthManager))]
  public class DepthMeshOcclusionManager:
    UnityLifecycleDriver
  {
    [SerializeField]
    [_Autofill]
    private Camera _mainCamera = null;

    [SerializeField]
    [_Autofill]
    private ARDepthManager _depthManager;

    [SerializeField]
    private ARSemanticSegmentationManager _semanticSegmentationManager;

    [SerializeField]
    private bool _occlusionEnabledOnStart = true;

    [SerializeField]
    [Tooltip("Name of the semantic channel that should be pushed to the max depth in the occlusion mesh.")]
    private string _suppressionChannel = null;

    private DepthMeshOcclusionEffect _occlusionEffect;

    private int _suppressionChannelIndex = -1;
    private Texture2D _suppressionTexture;
    private Rect _semanticCroppedRect;

    private bool _gotFirstDepthUpdate;
    private bool _gotFirstSemanticUpdate;

    public DepthMeshOcclusionEffect.ColorMask DebugColorMask
    {
      get
      {
        if (_occlusionEffect == null)
        {
          ARLog._Warn
          (
            "Tried to get occlusion debug color mask, but occlusions were not initialized."
          );

          return DepthMeshOcclusionEffect.ColorMask.None;
        }

        return _occlusionEffect.DebugColorMask;
      }
      set
      {
        _occlusionEffect.DebugColorMask = value;
      }
    }

    /// A helper function for debug use, allowing users to easily connect some simple trigger such
    /// as a UI Button to change visualization modes.
    public void CycleDebugColorMask()
    {
      switch (DebugColorMask)
      {
        case DepthMeshOcclusionEffect.ColorMask.None:
          DebugColorMask = DepthMeshOcclusionEffect.ColorMask.Disparity;
          break;

        case DepthMeshOcclusionEffect.ColorMask.Disparity:
          DebugColorMask = DepthMeshOcclusionEffect.ColorMask.UV;
          break;

        case DepthMeshOcclusionEffect.ColorMask.UV:
          DebugColorMask = DepthMeshOcclusionEffect.ColorMask.All;
          break;

        case DepthMeshOcclusionEffect.ColorMask.All:
          DebugColorMask = DepthMeshOcclusionEffect.ColorMask.None;
          break;
      }
    }

    protected override void InitializeImpl()
    {
      base.InitializeImpl();

      _depthManager.DepthBufferUpdated += OnDepthBufferUpdated;

      // The ARDepthManager handles creating the texture, so enable its FitToViewport
      _depthManager.FitToViewport = true;

      if (!string.IsNullOrEmpty(_suppressionChannel))
      {
        if (_semanticSegmentationManager == null)
        {
          ARLog._Error
          (
            "A SemanticSegmentationManager is required to use the " +
            "DepthMeshOcclusionManager's SuppressionChannel feature."
          );
        }
        else
        {
          _semanticSegmentationManager.SemanticBufferUpdated += OnSemanticBufferUpdated;

          // The suppression texture is created in this class, so disable its FitToViewport because
          // calling ISemanticBuffer.FitToViewport and then ISemanticBuffer.GetOrCreateTexture is a
          // little less performant than just calling GetOrCreateTexture with a non-zero cropping Rect.
          _semanticSegmentationManager.FitToViewport = false;
        }
      }
    }

    protected override void DeinitializeImpl()
    {
      base.DeinitializeImpl();

      if (_occlusionEffect == null)
        return;

      if (_mainCamera != null)
      {
        _mainCamera.clearFlags = _originalClearFlags;
        _mainCamera.backgroundColor = _originalBackgroundColor;
        _mainCamera.depthTextureMode = _originalDepthTextureMode;
        _mainCamera.nearClipPlane = _originalNearClipPlane;
        _mainCamera.farClipPlane = _originalFarClipPlane;
      }

      _occlusionEffect.Destroy();
      _occlusionEffect = null;
    }

    protected override void EnableFeaturesImpl()
    {
      base.EnableFeaturesImpl();
      ToggleOcclusion(true);
    }

    protected override void DisableFeaturesImpl()
    {
      base.DisableFeaturesImpl();
      ToggleOcclusion(false);
    }

    private void ToggleOcclusion(bool isEnabled)
    {
      if (_occlusionEffect == null)
        return;

      _occlusionEffect.OcclusionEnabled = isEnabled;
      ARLog._DebugFormat("Depth based occlusion {0}.", false, (isEnabled ? "enabled" : "disabled"));
    }

    private void OnDepthBufferUpdated(ARDepthManager.DepthBuffersUpdatedArgs args)
    {
      _gotFirstDepthUpdate = true;
      _depthManager.DepthBufferUpdated -= OnDepthBufferUpdated;

      // If we either don't need to wait for the semantics update or if we've already gotten
      // the first semantics update, then we're ready to initialize occlusion.
      if (string.IsNullOrEmpty(_suppressionChannel) || _gotFirstSemanticUpdate)
        InitializeOcclusion();
    }

    private void OnSemanticBufferUpdated(ARSemanticSegmentationManager.SemanticBufferUpdatedArgs args)
    {
      var semanticBuffer = _semanticSegmentationManager.LatestSemanticBuffer;

      if (!_gotFirstSemanticUpdate)
      {
        _gotFirstSemanticUpdate = true;
        _suppressionChannelIndex = semanticBuffer.GetChannelIndex(_suppressionChannel);

        _semanticCroppedRect =
          semanticBuffer.GetCroppedRect
          (
            _semanticSegmentationManager.Camera.pixelWidth,
            _semanticSegmentationManager.Camera.pixelHeight
          );
      }

      semanticBuffer.CreateOrUpdateTexture
      (
        _semanticCroppedRect,
        ref _suppressionTexture,
        TextureFormat.ARGB32,
        _suppressionChannelIndex
      );

      if (_occlusionEffect == null && _gotFirstDepthUpdate)
        InitializeOcclusion();
    }

    private CameraClearFlags _originalClearFlags;
    private Color _originalBackgroundColor;
    private DepthTextureMode _originalDepthTextureMode;
    private float _originalNearClipPlane;
    private float _originalFarClipPlane;

    private void InitializeOcclusion()
    {
      if (_occlusionEffect != null)
        return;

      _originalClearFlags = _mainCamera.clearFlags;
      _originalBackgroundColor = _mainCamera.backgroundColor;
      _originalDepthTextureMode = _mainCamera.depthTextureMode;
      _originalNearClipPlane = _mainCamera.nearClipPlane;
      _originalFarClipPlane = _mainCamera.farClipPlane;

      _mainCamera.clearFlags = CameraClearFlags.Color;
      _mainCamera.backgroundColor = Color.black;
      _mainCamera.depthTextureMode = DepthTextureMode.Depth;

      var nearDistance = _depthManager.LatestDepthBuffer.NearDistance;
      var farDistance = _depthManager.LatestDepthBuffer.FarDistance;

      _mainCamera.nearClipPlane = nearDistance;
      _mainCamera.farClipPlane = farDistance;

      _occlusionEffect =
        new DepthMeshOcclusionEffect
        (
          _mainCamera,
          _depthManager.DisparityTexture,
          nearDistance,
          farDistance,
          _occlusionEnabledOnStart,
          _suppressionTexture
        );

      ARLog._Debug("Depth mesh occlusion effect initialized.");
    }
  }
}
