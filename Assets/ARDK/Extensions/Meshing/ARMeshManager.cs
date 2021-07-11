// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Niantic.ARDK.AR;
using Niantic.ARDK.AR.ARSessionEventArgs;
using Niantic.ARDK.AR.Configuration;
using Niantic.ARDK.AR.Mesh;
using Niantic.ARDK.Utilities;
using Niantic.ARDK.Utilities.Collections;
using Niantic.ARDK.Utilities.Logging;

using UnityEngine;

namespace Niantic.ARDK.Extensions.Meshing
{
  /// This helper can be placed in a scene to easily add environment meshes.
  /// It reads meshing output from the ARSession, and instantiates mesh prefabs loaded with
  ///  components and materials for the desired behavior and rendering.
  /// Mesh visibility can be toggled on and off, using a depth mask material for occlusion effect.
  ///
  /// This helper exposes events for the mesh updating and clearing; also convenience methods and
  ///  properties for accessing the underlying mesh data.
  ///
  /// This script is likely to evolve and improve over the next releases, as more sophisticated
  ///  usages of the mesh are identified.
  public class ARMeshManager: ARSessionListener
  {
    [Header("AR Configuration Properties")]
    [SerializeField]
    [Tooltip("Target number of times per second to run the mesh update routine.")]
    private uint _targetFrameRate = 20;

    [SerializeField]
    [Tooltip("Target size of a mesh block in meters")]
    private float _targetBlockSize = 1.4f;

    [Header("Mesh Object Generation Settings")]
    [SerializeField]
    [Tooltip("When true, mesh block GameObjects will not be updated (the ARSession will still surface mesh updates).")]
    private bool _areBlockUpdatesPaused;

    [SerializeField]
    [Tooltip("Parent of every block (piece of mesh). If empty, this is assigned to the component's GameObject in Initialize().")]
    private GameObject _meshRoot;

    /// This GameObject requires a MeshFilter component, and will update a MeshCollider component if
    /// able. A MeshRenderer component is optional, but required for the SetUseInvisibleMaterial method.
    [SerializeField]
    [Tooltip("The GameObject to instantiate and update for each mesh block.")]
    private GameObject _meshPrefab;

    /// A value of zero or lower means the MeshCollider updates every time.
    /// A throttle is sometimes needed because MeshCollider updates are a lot more expensive than
    /// MeshRenderer updates.
    [SerializeField]
    [Tooltip("The number of mesh updates to skip between two consecutive MeshCollider updates.")]
    private int _colliderUpdateThrottle = 10;

    [Header("Mesh Visibility Settings")]
    [SerializeField]
    [Tooltip("When true, mesh blocks are rendered using InvisibleMaterial instead of the prefab's default material.")]
    private bool _useInvisibleMaterial = false;

    [SerializeField]
    [Tooltip("(Optional) Used as a substitution material when the mesh is hidden (a depth mask material should typically be used here).")]
    private Material _invisibleMaterial;

    private bool _hasParserAndGenerator;
    private _MeshDataParser _parser;
    private _MeshGenerator _generator;

    // Used to track when the Inspector-public variables are changed in OnValidate
    private uint _prevTargetFrameRate;
    private float _prevTargetBlockSize;

    public uint TargetFrameRate
    {
      get
      {
        return _targetFrameRate;
      }
      set
      {
        if (value != _targetFrameRate)
        {
          _targetFrameRate = value;
          RaiseConfigurationChanged();
        }
      }
    }

    public float TargetBlockSize
    {
      get
      {
        return _targetBlockSize;
      }
      set
      {
        if (!Mathf.Approximately(_targetBlockSize, value))
        {
          _targetBlockSize = value;
          RaiseConfigurationChanged();
        }
      }
    }

    /// False if the mesh objects are visible (i.e. it renders using the prefab's default material)
    /// and true if the mesh objects are hidden (i.e. it uses the invisible material).
    public bool UseInvisibleMaterial
    {
      get { return _useInvisibleMaterial; }
      set { SetUseInvisibleMaterial(value);}
    }

    /// When true, mesh block GameObjects will not be updated
    /// (a running ARSession will still surface mesh updates).
    public bool AreBlockUpdatesPaused
    {
      get { return _areBlockUpdatesPaused; }
      set { _areBlockUpdatesPaused = value; }
    }

    /// Called when all mesh blocks have been updated with info from the the latest mesh update.
    public event ArdkEventHandler<MeshBlocksUpdatedArgs> MeshBlocksUpdated;

    /// Called when all mesh blocks have been cleared.
    public event ArdkEventHandler<MeshBlocksUpdatedArgs> MeshBlocksCleared;

    protected override void InitializeImpl()
    {
      base.InitializeImpl();

      if (!_meshRoot)
        _meshRoot = gameObject;

      if (!_meshPrefab)
      {
        ARLog._Warn("No mesh prefab set on the ARMeshManager. No mesh blocks will be generated.");
        return;
      }

      _parser = new _MeshDataParser();

      _generator =
        new _MeshGenerator
        (
          _parser,
          _meshRoot,
          _meshPrefab,
          _invisibleMaterial,
          _colliderUpdateThrottle
        );

      _hasParserAndGenerator = true;
      SetUseInvisibleMaterial(_useInvisibleMaterial);
    }

    protected override void DeinitializeImpl()
    {
      base.DeinitializeImpl();

      ClearMesh();

      if (_hasParserAndGenerator)
      {
        _parser.Dispose();
        _generator.Clear();
      }
    }

    protected override void EnableFeaturesImpl()
    {
      base.EnableFeaturesImpl();

      _prevTargetFrameRate = _targetFrameRate;
      _prevTargetBlockSize = _targetBlockSize;
      RaiseConfigurationChanged();
    }

    protected override void DisableFeaturesImpl()
    {
      base.DisableFeaturesImpl();
      RaiseConfigurationChanged();
    }

    protected override void ListenToSession()
    {
      // TODO (Awareness): Integrate check for if Awareness initialization failed
      if (_hasParserAndGenerator)
        _arSession.MeshUpdated += OnMeshUpdated;
    }

    protected override void StopListeningToSession()
    {
      _arSession.MeshUpdated -= OnMeshUpdated;
    }

    internal override void _ApplyARConfigurationChange(IARConfiguration config)
    {
      if (config is IARWorldTrackingConfiguration worldConfig)
      {
        worldConfig.IsMeshingEnabled = AreFeaturesEnabled;
        worldConfig.MeshingTargetFrameRate = TargetFrameRate;
        worldConfig.MeshingTargetBlockSize = TargetBlockSize;
      }
    }

    /// Convenience method to convert world coordinates in Unity to integer block coordinates.
    public bool GetBlockCoords(Vector3 worldCoords, out Vector3Int blockCoords)
    {
      // Parser dne or has not yet processed the first mesh update
      if (!_hasParserAndGenerator || _parser.MeshVersion == 0)
      {
        blockCoords = Vector3Int.zero;
        return false;
      }

      Vector3 meshCoords = _meshRoot.transform.InverseTransformPoint(worldCoords);

      blockCoords = new Vector3Int
      (
        Mathf.FloorToInt(meshCoords.x / _parser.MeshBlockSize),
        Mathf.FloorToInt(meshCoords.y / _parser.MeshBlockSize),
        Mathf.FloorToInt(meshCoords.z / _parser.MeshBlockSize)
      );

      return true;
    }

    /// Convenience method to get the mesh GameObject at the specified block coordinates.
    /// Returns null if no object exists at those coordinates.
    public GameObject GetBlockGameObject(Vector3Int blockCoords)
    {
      if (!_hasParserAndGenerator)
        return null;

      MeshBlock block;
      if (_parser.Blocks.TryGetValue(blockCoords, out block))
      {
        GameObject blockObject;
        if (_generator.BlockObjects.TryGetValue(blockCoords, out blockObject))
          return blockObject;
      }

      return null;
    }

    /// Updates the MeshRenderers of all GameObjects in _blocks with either the invisible or the
    /// original prefab material. Does nothing if the prefab is null or does not contain a MeshRenderer.
    public void SetUseInvisibleMaterial(bool useInvisible)
    {
      _useInvisibleMaterial = useInvisible;

      if (_hasParserAndGenerator)
        _generator.SetUseInvisibleMaterial(useInvisible);
    }

    /// Clear the mesh, delete all GameObjects under _meshRoot.
    /// Sends a MeshCleared event if there's a listener when it's done.
    public void ClearMesh()
    {
      if (!_hasParserAndGenerator)
        return;

      _parser.Clear();
      _generator.Clear();

      if (MeshBlocksCleared != null)
      {
        MeshBlocksUpdatedArgs args = new MeshBlocksUpdatedArgs
        (
          EmptyReadOnlyCollection<GameObject>.Instance,
          EmptyReadOnlyCollection<GameObject>.Instance
        );

        MeshBlocksCleared(args);
      }
    }

    // Callback on the ARSession.MeshUpdated event.
    // Grabs the block mesh info and calls UpdateMesh() if the provided mesh is more recent
    //  than the current one.
    private void OnMeshUpdated(MeshUpdatedArgs args)
    {
      if (args.Mesh == null || AreBlockUpdatesPaused)
        return; // will try later

      var blocksUpdated = new List<GameObject>();
      var collidersUpdated = new List<GameObject>();

      void BlockObjectUpdated(GameObject obj)
      {
        blocksUpdated.Add(obj);
      }

      void ColliderUpdated(GameObject obj)
      {
        blocksUpdated.Add(obj);
      }

      var handler = MeshBlocksUpdated;
      if (handler != null)
      {
        _generator.BlockObjectUpdated += BlockObjectUpdated;
        _generator.ColliderUpdated += ColliderUpdated;
      }

      _parser.UpdateMeshObjects(args.Mesh);

      if (handler != null)
      {
        _generator.BlockObjectUpdated -= BlockObjectUpdated;
        _generator.ColliderUpdated -= ColliderUpdated;

        if (blocksUpdated.Count > 0 || collidersUpdated.Count > 0)
          handler(new MeshBlocksUpdatedArgs(blocksUpdated, collidersUpdated));
      }
    }

    private void OnValidate()
    {
      if (_prevTargetFrameRate != _targetFrameRate)
      {
        _prevTargetFrameRate = _targetFrameRate;
        RaiseConfigurationChanged();
      }

      if (!Mathf.Approximately(_prevTargetBlockSize, _targetBlockSize))
      {
        _prevTargetBlockSize = _targetBlockSize;
        RaiseConfigurationChanged();
      }
    }
  }
}