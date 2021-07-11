// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Niantic.ARDK.AR;
using Niantic.ARDK.AR.Mesh;
using Niantic.ARDK.Utilities.Logging;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using UnityEngine;

namespace Niantic.ARDK.Extensions.Meshing
{
  public class MeshBlock
  {
    public int ColliderVersion = -1;
    public Mesh Mesh;
    public int MeshVersion = -1;
    public int Version = -1;
  }

  internal class _MeshDataParser: IDisposable
  {
    /// Convenience property to get the size of a block in meters.
    public float MeshBlockSize { get; private set; }

    /// Convenience property to get the version of the mesh.
    public int MeshVersion { get; private set; }

    /// Convenience property to get the number of blocks in the mesh. Each block is represented
    ///  by a separate GameObject, which is an instance of _meshPrefab.
    public int MeshBlockCount { get; private set; }

    /// Convenience property to get the number of vertices in the mesh.
    public int MeshVertexCount { get; private set; }

    /// Convenience property to get the number of faces (polygons) in the mesh.
    public int MeshFaceCount { get; private set; }

    // buffers
    private NativeArray<int> _blockArray;
    private NativeArray<int> _faceArray;
    private NativeArray<float> _vertexArray;

    private Dictionary<Vector3Int, MeshBlock> _blocks = new Dictionary<Vector3Int, MeshBlock>();

    public event Action<Vector3Int> MeshBlockUpdated;
    public event Action<Vector3Int> MeshBlockObsoleted;
    public event Action MeshCleared;

    public IReadOnlyDictionary<Vector3Int, MeshBlock> Blocks { get; }

    public _MeshDataParser()
    {
      const int initialCount = 100;

      // Allocate for 100 blocks
      const int initialBlockBufferSize = ARMeshConstants.INTS_PER_BLOCK * initialCount;
      _blockArray = new NativeArray<int>(initialBlockBufferSize, Allocator.Persistent);

      // Allocate for 100 vertices for each block
      const int initialVertexBufferSize =
        ARMeshConstants.FLOATS_PER_VERTEX * initialCount * initialCount;
      _vertexArray = new NativeArray<float>(initialVertexBufferSize, Allocator.Persistent);

      // Allocate for 100 polygons for each block
      const int initialFaceBufferSize = ARMeshConstants.INTS_PER_FACE * initialCount * initialCount;
      _faceArray = new NativeArray<int>(initialFaceBufferSize, Allocator.Persistent);

      Blocks = new ReadOnlyDictionary<Vector3Int, MeshBlock>(_blocks);
    }

    public void UpdateMeshObjects(IARMesh mesh)
    {
      var version =
        mesh.GetBlockMeshInfo
        (
          out int blockBufferSize,
          out int vertexBufferSize,
          out int faceBufferSize
        );

      var needsUpdate =
        version > MeshVersion &&
        blockBufferSize > 0 &&
        vertexBufferSize > 0 &&
        faceBufferSize > 0;

      if (needsUpdate)
      {
        // Update mesh info
        MeshBlockSize = mesh.MeshBlockSize;
        MeshVersion = version;
        MeshBlockCount = blockBufferSize / ARMeshConstants.INTS_PER_BLOCK;
        MeshVertexCount = vertexBufferSize / ARMeshConstants.FLOATS_PER_VERTEX;
        MeshFaceCount = faceBufferSize / ARMeshConstants.INTS_PER_FACE;

        // Recreate native buffers if the new mesh contains more blocks
        if (blockBufferSize > _blockArray.Length)
        {
          _blockArray.Dispose();
          _blockArray = new NativeArray<int>(blockBufferSize * 2, Allocator.Persistent);
        }

        if (vertexBufferSize > _vertexArray.Length)
        {
          _vertexArray.Dispose();
          _vertexArray = new NativeArray<float>(vertexBufferSize * 2, Allocator.Persistent);
        }

        if (faceBufferSize > _faceArray.Length)
        {
          _faceArray.Dispose();
          _faceArray = new NativeArray<int>(faceBufferSize * 2, Allocator.Persistent);
        }

        UpdateMeshBlocks(mesh, blockBufferSize, vertexBufferSize, faceBufferSize);
      }
      else if (version == 0 && MeshVersion > 0 && blockBufferSize == 0)
      {
        // Mesh was reset after loading a new map
        Clear();
      }
    }

    // Obtains the mesh buffers and parses them, updating individual mesh blocks if their
    // version number is newer than the current one.
    // Returns true if mesh objects were successfully updated
    private void UpdateMeshBlocks
    (
      IARMesh mesh,
      int blockBufferSize,
      int vertexBufferSize,
      int faceBufferSize
    )
    {
      // In the unlikely event that a race condition occured and the buffers are not the right size,
      // just skip this update and wait for the next one.
      if (!GetBlocksAndValidateSize(mesh, blockBufferSize, vertexBufferSize, faceBufferSize))
        return;

      int firstVertex = 0;
      int firstTriangle = 0;
      int normalsOffset =
        vertexBufferSize / 2; // normals start halfway through the buffer (See IARMesh.cs)

      // Update all the full blocks returned by the API
      for (int b = 0; b < blockBufferSize; b += ARMeshConstants.INTS_PER_BLOCK)
      {
        var currentBlock =
          GetOrCreateBlockWithInfo
          (
            b,
            out Vector3Int blockCoords,
            out int vertexCount,
            out int triangleCount,
            out int blockVersion
          );

        currentBlock.MeshVersion = MeshVersion;

        // Update block if it is outdated
        if (currentBlock.Version < blockVersion)
        {
          UpdateBlockGeometry
          (
            currentBlock,
            vertexCount,
            firstVertex,
            normalsOffset,
            triangleCount,
            firstTriangle
          );

          currentBlock.Version = blockVersion;
          MeshBlockUpdated?.Invoke(blockCoords);
        }

        firstVertex += vertexCount;
        firstTriangle += triangleCount;
      }

      // Clean up obsolete blocks
      RemoveObsoleteBlocks();
    }

    private bool GetBlocksAndValidateSize
    (
      IARMesh mesh,
      int blockBufferSize,
      int vertexBufferSize,
      int faceBufferSize
    )
    {
      // Get all the blocks and validate the counts are correct
      int fullBlocksCount;
      unsafe
      {
        var blockBufferPtr = _blockArray.GetUnsafePtr();
        var vertexBufferPtr = _vertexArray.GetUnsafePtr();
        var faceBufferPtr = _faceArray.GetUnsafePtr();

        fullBlocksCount = mesh.GetBlockMesh
        (
          (IntPtr)blockBufferPtr,
          (IntPtr)vertexBufferPtr,
          (IntPtr)faceBufferPtr,
          blockBufferSize,
          vertexBufferSize,
          faceBufferSize
        );
      }

      if (fullBlocksCount < 0)
      {
        ARLog._Error("Error calling IARMesh.GetBlockMesh(), will not update the mesh.");
        return false;
      }

      if (fullBlocksCount == 0)
      {
        ARLog._Error("IARMesh.GetBlockMesh() gave us an empty mesh, will not update.");
        return false;
      }

      var gotAllBlocks = fullBlocksCount == MeshBlockCount;
      if (!gotAllBlocks)
      {
        ARLog._ErrorFormat
        (
          "IARMesh.GetBlockMesh() returned {0} full blocks, expected {1}.",
          fullBlocksCount,
          MeshBlockCount
        );

        return false;
      }

      return true;
    }

    private MeshBlock GetOrCreateBlockWithInfo
    (
      int startIndex,
      out Vector3Int blockCoords,
      out int vertexCount,
      out int triangleCount,
      out int blockVersion
    )
    {
      blockCoords =
        new Vector3Int
        (
          _blockArray[startIndex],
          _blockArray[startIndex + 1],
          _blockArray[startIndex + 2]
        );

      vertexCount = _blockArray[startIndex + 3];
      triangleCount = _blockArray[startIndex + 4];
      blockVersion = _blockArray[startIndex + 5];

      if (!_blocks.TryGetValue(blockCoords, out MeshBlock block))
      {
        block = new MeshBlock();

        block.Mesh = new Mesh();
        block.Mesh.MarkDynamic();

        _blocks[blockCoords] = block;
      }

      return block;
    }

    private void UpdateBlockGeometry
    (
      MeshBlock block,
      int vertexCount,
      int firstVertex,
      int normalsOffset,
      int triangleCount,
      int firstTriangle
    )
    {
      // copy vertices
      var vertices = new Vector3[vertexCount];
      var normals = new Vector3[vertexCount];

      for (int va = 0; va < vertexCount; va++)
      {
        int pos = (firstVertex + va) * 3; // 3 floats per vector value
        vertices[va] =
          new Vector3
          (
            _vertexArray[pos],
            _vertexArray[pos + 1],
            _vertexArray[pos + 2]
          );

        int norm = normalsOffset + pos;
        normals[va] =
          new Vector3
          (
            _vertexArray[norm],
            _vertexArray[norm + 1],
            _vertexArray[norm + 2]
          );
      }

      // copy faces
      int firstTriangleIndex = firstTriangle * ARMeshConstants.INTS_PER_FACE;
      int maxTriangleIndex = triangleCount * ARMeshConstants.INTS_PER_FACE;
      int[] triangles = _faceArray.Slice(firstTriangleIndex, maxTriangleIndex).ToArray();

      for (int t = 0; t < maxTriangleIndex; t++)
        triangles[t] -= firstVertex;

      Mesh workMesh = block.Mesh;

      workMesh.Clear();
      workMesh.vertices = vertices;
      workMesh.normals = normals;
      workMesh.triangles = triangles;
    }

    private void RemoveObsoleteBlocks()
    {
      var obsoleteBlocks = new List<Vector3Int>();
      foreach (Vector3Int blockCoords in _blocks.Keys)
      {
        var block = _blocks[blockCoords];
        if (block.MeshVersion != MeshVersion)
        {
          MeshBlockObsoleted?.Invoke(blockCoords);
          DestroyMesh(block.Mesh);
          obsoleteBlocks.Add(blockCoords);
        }
      }

      foreach (Vector3Int blockCoords in obsoleteBlocks)
        _blocks.Remove(blockCoords);
    }

    public void Clear()
    {
      MeshVersion = 0;
      MeshBlockCount = 0;
      MeshVertexCount = 0;
      MeshFaceCount = 0;

      _blocks.Clear();

      if (_blockArray.IsCreated)
        _blockArray.Dispose();

      if (_faceArray.IsCreated)
        _faceArray.Dispose();

      if (_vertexArray.IsCreated)
        _vertexArray.Dispose();

      MeshCleared?.Invoke();
    }

    public void Dispose()
    {
      Clear();
    }

    internal static bool _destroyImmediateForTesting = false;

    private void DestroyMesh(Mesh mesh)
    {
      if (_destroyImmediateForTesting)
        GameObject.DestroyImmediate(mesh);
      else
        GameObject.Destroy(mesh);
    }
  }
}
