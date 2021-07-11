// Copyright 2021 Niantic, Inc. All Rights Reserved.

using Niantic.ARDK.AR;
using Niantic.ARDK.AR.ARSessionEventArgs;
using Niantic.ARDK.AR.Awareness;
using Niantic.ARDK.Extensions;

using UnityEngine;
using UnityEngine.UI;

namespace Niantic.ARDKExamples
{
  public class MeshingExampleManager: MonoBehaviour
  {
    [SerializeField]
    private ARSessionManager _arSessionManager;

    [Header("UI")]
    [SerializeField]
    private Text _meshStatusText = null;

    [SerializeField]
    private GameObject _loadingStatusPanel = null;

    private bool _contextAwarenessLoadComplete = false;

    private void Start()
    {
      ShowLoadingScenePanel(false);
      ARSessionFactory.SessionInitialized += OnSessionInitialized;
    }

    private void OnDestroy()
    {
      ARSessionFactory.SessionInitialized -= OnSessionInitialized;

      if (_arSessionManager.ARSession != null)
        _arSessionManager.ARSession.MeshUpdated -= OnMeshUpdated;
    }

    private void OnSessionInitialized(AnyARSessionInitializedArgs args)
    {
      args.Session.MeshUpdated += OnMeshUpdated;

      _contextAwarenessLoadComplete = false;
      ShowLoadingScenePanel(true);
    }

    private void Update()
    {
      if (_arSessionManager.ARSession != null && !_contextAwarenessLoadComplete)
      {
        var status =
          _arSessionManager.ARSession.GetAwarenessInitializationStatus
          (
            out AwarenessInitializationError error,
            out string errorMessage
          );

        if (status == AwarenessInitializationStatus.Ready)
        {
          _contextAwarenessLoadComplete = true;
          ShowLoadingScenePanel(false);
        }
        else if (status == AwarenessInitializationStatus.Failed)
        {
          _contextAwarenessLoadComplete = true;
          Debug.LogErrorFormat
          (
            "Failed to initialize Context Awareness processes. Error: {0} ({1})",
            error,
            errorMessage
          );
        }
      }
    }

    private void OnMeshUpdated(MeshUpdatedArgs args)
    {
      int version = args.Mesh.GetBlockMeshInfo(out int blocks, out int vertices, out int faces);

      if (!_contextAwarenessLoadComplete)
      {
        // clear the popup in case meshing uses LIDAR and won't load context awareness
        _contextAwarenessLoadComplete = true;
        ShowLoadingScenePanel(false);
      }
      
      if (_meshStatusText != null)
      {
        _meshStatusText.text = "Mesh v" +
          version +
          "\nb: " +
          (blocks / 6) +
          " v: " +
          (vertices / 6) +
          " f: " +
          (faces / 3);
      }
    }

    private void ShowLoadingScenePanel(bool toggle)
    {
      if (_loadingStatusPanel)
        _loadingStatusPanel.gameObject.SetActive(toggle);
    }
  }
}
