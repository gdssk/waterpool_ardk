// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;

using ARDK.VirtualStudio.AR.Camera;

using Niantic.ARDK.AR;
using Niantic.ARDK.AR.Camera;
using Niantic.ARDK.AR.Frame;
using Niantic.ARDK.AR.Image;
using Niantic.ARDK.Utilities;

using Unity.Collections;

using UnityEngine;
using UnityEngine.Assertions;

using Object = UnityEngine.Object;

namespace Niantic.ARDK.VirtualStudio.AR.Mock
{
  internal sealed class _MockFrameBufferProvider:
    IDisposable
  {
    // Rendering data
    private readonly Material _colorConversionMaterial;
    private readonly RenderTexture _mockRenderTexture;
    private readonly RenderTexture _convertedRenderTexture;
    private readonly Texture2D _mockTexture;
    private readonly Shader _colorConversionShader;

    // ARSession data
    private readonly _MockARSession _arSession;
    private readonly float _timeBetweenFrames;
    private float _timeSinceLastFrame;

    // ARFrame data
    private readonly Transform _deviceCamTransform;
    private readonly _SerializableARCamera _cachedSerializedCamera;
    private readonly int _imageWidth;
    private readonly int _imageHeight;

    public _MockFrameBufferProvider(_MockARSession mockARSession, Camera mockDeviceCamera)
    {
      _colorConversionShader = Resources.Load<Shader>("RGBAToBGRA");
      Assert.IsNotNull(_colorConversionShader, "Could not find RGBAToBGRA.shader");

      _arSession = mockARSession;
      _deviceCamTransform = mockDeviceCamera.transform;
      _timeBetweenFrames = 1f / _MockCameraConfiguration.FPS;

      _imageWidth = mockDeviceCamera.pixelWidth;
      _imageHeight = mockDeviceCamera.pixelHeight;
      _colorConversionMaterial = new Material(_colorConversionShader);

      // Initialize RenderTexture with screen size
      _mockRenderTexture =
        new RenderTexture
        (
          _imageWidth,
          _imageHeight,
          16,
          RenderTextureFormat.ARGB32
        );

      _mockRenderTexture.Create();

      // Assign the MockSceneCamera to render to the _mockRenderTexture
      mockDeviceCamera.targetTexture = _mockRenderTexture;

      _convertedRenderTexture =
        new RenderTexture
        (
          _imageWidth,
          _imageHeight,
          16,
          RenderTextureFormat.ARGB32
        );

      _mockTexture = new Texture2D(_imageWidth, _imageHeight, TextureFormat.ARGB32, false);

      var resolution = new Resolution { width = _imageWidth, height = _imageHeight };

      _cachedSerializedCamera =
        new _SerializableARCamera
        {
          CPUImageResolution = resolution,
          ImageResolution = resolution,
          ProjectionMatrix = mockDeviceCamera.projectionMatrix,
          _estimatedProjectionMatrix = mockDeviceCamera.projectionMatrix,
          TrackingState = TrackingState.Normal,
          TrackingStateReason = TrackingStateReason.None
        };

      _UpdateLoop.Tick += Update;
    }

    private bool _isDisposed;
    public void Dispose()
    {
      if (_isDisposed)
        return;

      _isDisposed = true;

      if (Application.isEditor)
        Object.DestroyImmediate(_mockTexture);
      else
        Object.Destroy(_mockTexture);

      _mockRenderTexture.Release();
      _convertedRenderTexture.Release();
    }

    private void Update()
    {
      if (_arSession != null && _arSession.State == ARSessionState.Running)
      {
        _timeSinceLastFrame += Time.deltaTime;
        if (_timeSinceLastFrame >= _timeBetweenFrames)
        {
          _timeSinceLastFrame = 0;

          _cachedSerializedCamera._estimatedViewMatrix = _deviceCamTransform.worldToLocalMatrix;
          _cachedSerializedCamera.Transform = _deviceCamTransform.localToWorldMatrix;

          var serializedFrame =
            new _SerializableARFrame
            (
              capturedImageBuffer: _GetFrameBuffer(),
              depthBuffer: null,
              semanticBuffer: null,
              camera: _cachedSerializedCamera,
              lightEstimate: null,
              anchors: null,
              maps: null,
              worldScale: 1.0f,
              estimatedDisplayTransform: Matrix4x4.identity
            );

          _arSession.UpdateFrame(serializedFrame);
        }
      }
    }

    internal _SerializableImageBuffer _GetFrameBuffer()
    {
      var mockData = _GetRawData();
      var plane =
        new _SerializableImagePlane
        (
          mockData,
          _imageWidth,
          _imageHeight,
          _imageWidth * 4,
          4
        );

      var buffer =
        new _SerializableImageBuffer
        (
          ImageFormat.BGRA,
          new _SerializableImagePlanes(new[] { plane }),
          75
        );

      return buffer;
    }

    private NativeArray<byte> _GetRawData()
    {
      // Convert the RGBA render from the MockSceneCamera to an BGRA render
      Graphics.Blit(_mockRenderTexture, _convertedRenderTexture, _colorConversionMaterial);

      // Copy the BGRA RenderTexture (GPU) into a Texture2D (CPU)
      var currentRt = RenderTexture.active;

      RenderTexture.active = _convertedRenderTexture;
      _mockTexture.ReadPixels(new Rect(0, 0, _imageWidth, _imageHeight), 0, 0);
      _mockTexture.Apply();

      RenderTexture.active = currentRt;

      var data = _mockTexture.GetRawTextureData();
      return new NativeArray<byte>(data, Allocator.Persistent);
    }
  }
}