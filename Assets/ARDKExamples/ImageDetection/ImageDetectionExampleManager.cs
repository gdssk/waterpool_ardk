// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using Niantic.ARDK.AR;
using Niantic.ARDK.AR.Configuration;
using Niantic.ARDK.AR.Anchors;
using Niantic.ARDK.AR.ARSessionEventArgs;
using Niantic.ARDK.AR.ReferenceImage;
using Niantic.ARDK.Utilities;
using Niantic.ARDK.Utilities.Collections;

using UnityEngine;

namespace Niantic.ARDKExamples
{
  // Image Detection example. Shows how to create ARReferenceImages using both raw bytes
  // and a local file path, both synchronously and asynchronously. See the "Detecting Images"
  // page in the User Manual for further information on how to optimally detect images and
  // use image anchors.
  //
  // @note
  //   The image of the chessboard used as the "Image from Path" is actually a very bad example
  //   of the sort of image that's best suited for image detection, because it has many repeating
  //   geometric features. Bad reference images will still be detected, but it takes longer and
  //   may not track as accurately.
  public class ImageDetectionExampleManager:
    MonoBehaviour
  {
    // Prefab to spawn on top of detected images
    public GameObject Plane = null;

    // Whether or not to use async image creation/database set
    public bool _createAndSetImageReferencesAsync = false;

    [Header("Image from Bytes")]
    // Raw bytes of the jpg image used to test creating an image reference from a byte buffer
    public TextAsset _rawImageBytes = null;

    /// Size (meters) of the raw bytes image in physical form.
    public float _rawImagePhysicalSize = 0.22f;

    [Header("Image from Path")]
    // Raw bytes of the jpg image used to test creating an image reference from a local file.
    public TextAsset _filePathImageBytes = null;

    // Size (meters) of the file path image in physical form.
    public float _filePathImagePhysicalSize = 0.3f;

    private IARSession _arSession;
    private Dictionary<Guid, GameObject> _detectedImages = new Dictionary<Guid, GameObject>();
    private HashSet<IARReferenceImage> imageSet = new HashSet<IARReferenceImage>();

    private string _tempFilePath = null;

    private void Start()
    {
#if UNITY_EDITOR
      Debug.LogError("This example only works on device");
#else
      // Write the bytes into a temporary location to emulate creating an
      // ARReferenceImage from a local file.
      _tempFilePath = Path.Combine(Application.temporaryCachePath, "chess.jpg");
      File.WriteAllBytes(_tempFilePath, _filePathImageBytes.bytes);
#endif
    }

    public void Init()
    {
      _arSession = ARSessionFactory.Create();

      _arSession.SessionFailed += args => Debug.Log(args.Error);
      _arSession.AnchorsAdded += OnAnchorsAdded;
      _arSession.AnchorsUpdated += OnAnchorsUpdated;
      _arSession.AnchorsRemoved += OnAnchorsRemoved;

      var config = ARWorldTrackingConfigurationFactory.Create();
      config.IsAutoFocusEnabled = true;

      if (_createAndSetImageReferencesAsync)
        StartCoroutine(SetImagesAndRunAsync(config));
      else
        SetImagesAndRun(config);
    }

    private void SetImagesAndRun(IARWorldTrackingConfiguration config)
    {
      // Create an ARReferenceImage from a local file path.
      var imageFromPath =
        ARReferenceImageFactory.Create
        (
          "chess",
          _tempFilePath,
          _filePathImagePhysicalSize)
        ;

      if (imageFromPath != null)
        imageSet.Add(imageFromPath);

      // Create an ARReferenceImage from raw bytes. In a real application, these bytes
      // could have been received over the network
      var rawByteBuffer = _rawImageBytes.bytes;
      var imageFromBuffer =
        ARReferenceImageFactory.Create
        (
          "earth",
          rawByteBuffer,
          rawByteBuffer.Length,
          _rawImagePhysicalSize
        );

      if (imageFromBuffer != null)
        imageSet.Add(imageFromBuffer);

      // Set the images in the config, then run the session
      config.DetectionImages = imageSet.AsArdkReadOnly();
      _arSession.Run(config);

      Debug.Log("Init complete. Point the camera at known images to detect them.");
    }

    private IEnumerator SetImagesAndRunAsync(IARWorldTrackingConfiguration config)
    {
      var imageProcessed = false;

      // Asynchronously create an ARReferenceImage from a local file path.
      ARReferenceImageFactory.CreateAsync
      (
        "chess",
        _tempFilePath,
        _filePathImagePhysicalSize,
        resultImage =>
        {
          if (resultImage != null)
            imageSet.Add(resultImage);

          imageProcessed = true;
        }
      );

      // This is not what you want from a real async task, but it works for testing.
      while (imageProcessed == false)
        yield return null;

      imageProcessed = false;

      // Asynchronously create an ARReferenceImage from raw bytes.
      var rawByteBuffer = _rawImageBytes.bytes;

      ARReferenceImageFactory.CreateAsync
      (
        "earth",
        rawByteBuffer,
        rawByteBuffer.Length,
        _rawImagePhysicalSize,
        resultImage =>
        {
          if (resultImage != null)
            imageSet.Add(resultImage);

          imageProcessed = true;
        }
      );

      while (imageProcessed == false)
        yield return null;

      // Set up the ARSession to run once the DetectionImages are set
      config.SetDetectionImagesAsync
      (
        imageSet.AsArdkReadOnly(),
        delegate
        {
          _arSession.Run(config);
        }
      );
    }

    private void OnAnchorsAdded(AnchorsArgs args)
    {
      foreach (var anchor in args.Anchors)
      {
        if (anchor.AnchorType != AnchorType.Image)
          continue;

        var imageAnchor = (IARImageAnchor) anchor;
        Debug.Log("Image found: " + imageAnchor.ReferenceImage.Name);

        var newPlane = Instantiate(Plane);
        _detectedImages[anchor.Identifier] = newPlane;

        UpdatePlaneTransform(imageAnchor);
      }
    }

    private void OnAnchorsUpdated(AnchorsArgs args)
    {
      foreach (var anchor in args.Anchors)
      {
        if (!_detectedImages.ContainsKey(anchor.Identifier))
          continue;

        var imageAnchor = anchor as IARImageAnchor;
        UpdatePlaneTransform(imageAnchor);
      }
    }

    private void OnAnchorsRemoved(AnchorsArgs args)
    {
      foreach (var anchor in args.Anchors)
      {
        if (!_detectedImages.ContainsKey(anchor.Identifier))
          continue;

        Destroy(_detectedImages[anchor.Identifier]);
        _detectedImages.Remove(anchor.Identifier);
      }
    }

    private void UpdatePlaneTransform(IARImageAnchor imageAnchor)
    {
      var identifier = imageAnchor.Identifier;

      _detectedImages[identifier].transform.position = imageAnchor.Transform.ToPosition();
      _detectedImages[identifier].transform.rotation = imageAnchor.Transform.ToRotation();

      var localScale = _detectedImages[identifier].transform.localScale;
      localScale.x = imageAnchor.ReferenceImage.PhysicalSize.x;
      localScale.z = imageAnchor.ReferenceImage.PhysicalSize.y;
      _detectedImages[identifier].transform.localScale = localScale;
    }

    private void OnDestroy()
    {
      File.Delete(_tempFilePath);

      if (_arSession != null)
        _arSession.Dispose();
    }
  }
}
