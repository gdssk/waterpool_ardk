// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Niantic.ARDK.Utilities.Logging;

using UnityEngine;

namespace Niantic.ARDK.Extensions.Meshing
{
  internal class _MeshGenerator
  {
    private _MeshDataParser _parser;

    private GameObject _root;
    private GameObject _prefab;

    private Material _invisibleMaterial;
    private bool _usingInvisibleMaterial;

    private int _colliderUpdateThrottle;

    private Dictionary<Vector3Int, GameObject> _blockObjects = new Dictionary<Vector3Int, GameObject>();

    public IReadOnlyDictionary<Vector3Int, GameObject> BlockObjects { get; }

    public Action<GameObject> BlockObjectUpdated;
    public Action<GameObject> ColliderUpdated;

    public _MeshGenerator
    (
      _MeshDataParser parser,
      GameObject root,
      GameObject prefab,
      Material invisibleMaterial,
      int colliderUpdateThrottle
    )
    {
      _parser = parser;
      _root = root;
      _prefab = prefab;
      _invisibleMaterial = invisibleMaterial;
      _colliderUpdateThrottle = colliderUpdateThrottle;

      parser.MeshBlockUpdated += OnMeshBlockUpdated;
      parser.MeshBlockObsoleted += OnMeshBlockObsoleted;
      parser.MeshCleared += Clear;

      BlockObjects = new ReadOnlyDictionary<Vector3Int, GameObject>(_blockObjects);
    }

    public void Clear()
    {
      foreach (var go in _blockObjects.Values)
        GameObject.Destroy(go);

      _blockObjects.Clear();
    }

    public bool TryGetBlockObject(Vector3Int blockCoords, out GameObject blockObject)
    {
      return _blockObjects.TryGetValue(blockCoords, out blockObject);
    }

    private void OnMeshBlockUpdated(Vector3Int blockCoords)
    {
      if (!_blockObjects.ContainsKey(blockCoords))
        AddMeshBlock(blockCoords);

      UpdateMeshCollider(blockCoords);

      BlockObjectUpdated?.Invoke(_blockObjects[blockCoords]);
    }

    private void OnMeshBlockObsoleted(Vector3Int blockCoords)
    {
      GameObject.Destroy(_blockObjects[blockCoords].gameObject);
      _blockObjects.Remove(blockCoords);
    }

    public void SetUseInvisibleMaterial(bool useInvisible)
    {
      _usingInvisibleMaterial = useInvisible;

      Material newSharedMaterial = null;
      if (!useInvisible)
      {
        if (_prefab == null)
        {
          ARLog._Error("Failed to change the mesh material because no mesh prefab was set.");
          return;
        }

        MeshRenderer meshRenderer = _prefab.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
          ARLog._Error("Failed to change the mesh material because the mesh prefab lacks a MeshRenderer.");
          return;
        }

        newSharedMaterial = meshRenderer.sharedMaterial;
        if (newSharedMaterial == null)
        {
          ARLog._Error
          (
            "Failed to change the mesh material because the mesh prefab's MeshRenderer component " +
            "lacks a shared material."
          );

          return;
        }
      }
      else
      {
        newSharedMaterial = _invisibleMaterial;
        if (newSharedMaterial == null)
        {
          ARLog._Error("Failed to change the mesh material because no invisible material was set.");
          return;
        }
      }

      foreach (var blockObject in _blockObjects.Values)
      {
        var blockRenderer = blockObject.GetComponent<MeshRenderer>();
        if (blockRenderer)
          blockRenderer.material = newSharedMaterial;
      }
    }

    public void AddMeshBlock(Vector3Int blockCoords)
    {
      if (!_parser.Blocks.TryGetValue(blockCoords, out MeshBlock meshBlock))
      {
        ARLog._Error("No MeshBlock found at block coordinates: " + blockCoords);
        return;
      }

      var go = GameObject.Instantiate(_prefab, _root.transform, true);
      go.transform.localScale = Vector3.one;
      go.name = _prefab.name + blockCoords;

      var meshFilter = go.GetComponent<MeshFilter>();
      meshFilter.mesh = meshBlock.Mesh;

      if (_usingInvisibleMaterial && _invisibleMaterial != null)
      {
        var meshRenderer =
          go.GetComponent<MeshRenderer>();

        if (meshRenderer != null)
          meshRenderer.material = _invisibleMaterial;
      }

      _blockObjects[blockCoords] = go;
    }

    public void UpdateMeshCollider(Vector3Int blockCoords)
    {
      if (!_blockObjects.TryGetValue(blockCoords, out GameObject blockObject))
      {
        ARLog._Error("No mesh GameObject found at block coordinates: " + blockCoords);
        return;
      }

      var meshCollider = blockObject.GetComponent<MeshCollider>();
      if (meshCollider == null)
        return;

      var meshBlock = _parser.Blocks[blockCoords];

      // update the collider less often for optimal performance
      int minColliderUpdateVersion =
        meshBlock.ColliderVersion +
        Math.Max(_colliderUpdateThrottle, 0) +
        1;

      var colliderNeedsUpdate =
        meshBlock.ColliderVersion < 0 ||
        meshBlock.Version >= minColliderUpdateVersion;

      if (colliderNeedsUpdate)
      {
        meshCollider.sharedMesh = meshBlock.Mesh;
        meshBlock.ColliderVersion = meshBlock.Version;

        ColliderUpdated?.Invoke(blockObject);
      }
    }
  }
}
