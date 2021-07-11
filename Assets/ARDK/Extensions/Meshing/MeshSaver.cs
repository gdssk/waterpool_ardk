// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.IO;

using Niantic.ARDK.AR;
using Niantic.ARDK.AR.ARSessionEventArgs;
using Niantic.ARDK.AR.Mesh;
using Niantic.ARDK.Utilities.Logging;

using UnityEngine;

namespace Niantic.ARDK.Extensions.Meshing
{
  /// This helper can be placed in a scene to save mesh data into a binary file on demand.
  ///
  /// HEADER:
  /// The first 16 bytes are a "magic word" equal to the ASCII string "6DBLOCKMESH" (padded with 0)
  /// The next 16 bytes are 3 Int32 values for block, vertex, and face buffer sizes
  ///  followed by 1 Float value for Mesh Block size
  ///
  /// BUFFERS:
  /// Next is the block buffer (sizeof Int32 * block buffer size, no padding)
  /// Vertex buffer (sizeof Float * vertex buffer size, no padding)
  /// Face buffer (sizeof Int32 * face buffer size, no padding)
  ///
  /// More details on the byte layout can be found in IARMesh.cs.
  /// Mesh files produced by this script can be loaded into the Unity Editor play mode
  ///  with Niantic.ARDK.VirtualStudio.AR.Mock.MockMesh
  public class MeshSaver:
    MonoBehaviour
  {
    private IARMesh _mesh;
    private string _meshPath;
    private IARSession _session;

    private void Start()
    {
      ARSessionFactory.SessionInitialized += OnSessionInitialized;
    }

    private void OnDestroy()
    {
      ARSessionFactory.SessionInitialized -= OnSessionInitialized;
      Teardown();
    }

    private void OnSessionInitialized(AnyARSessionInitializedArgs args)
    {
      if (_session != null)
        return;

      _session = args.Session;
      _meshPath = Application.persistentDataPath +
        "/meshes/" +
        DateTime.Now.ToString("yyyyMMdd-HHmmss");

      _session.Deinitialized += OnDeinitialized;
      _session.MeshUpdated += OnMeshUpdated;
    }

    private void OnDeinitialized(ARSessionDeinitializedArgs args)
    {
      Teardown();
      _meshPath = null;
      _mesh = null;
    }

    private void Teardown()
    {
      if (_session != null)
      {
        _session.Deinitialized -= OnDeinitialized;
        _session.MeshUpdated -= OnMeshUpdated;
        _session = null;
      }
    }

    private void OnMeshUpdated(MeshUpdatedArgs args)
    {
      _mesh = args.Mesh;
    }

    /// Saves the current version of the mesh into a file.
    public void SaveMesh()
    {
      if (_session == null || _meshPath == null || _mesh == null)
        return;

      int blockBufferSize = 0;
      int vertexBufferSize = 0;
      int faceBufferSize = 0;
      int version = _mesh.GetBlockMeshInfo
        (out blockBufferSize, out vertexBufferSize, out faceBufferSize);

      if (blockBufferSize <= 0 ||
        vertexBufferSize <= 0 ||
        faceBufferSize <= 0)
        return;

      byte[] blockArray = new byte[blockBufferSize * sizeof(int)];
      byte[] vertexArray = new byte[vertexBufferSize * sizeof(float)];
      byte[] faceArray = new byte[faceBufferSize * sizeof(int)];

      int fullBlocks = 0;

      unsafe
      {
        fixed (byte* blockBufferPtr = &blockArray[0],
          faceBufferPtr = &faceArray[0],
          vertexBufferPtr = &vertexArray[0])
        {
          fullBlocks = _mesh.GetBlockMesh
          (
            (IntPtr)blockBufferPtr,
            (IntPtr)vertexBufferPtr,
            (IntPtr)faceBufferPtr,
            blockBufferSize,
            vertexBufferSize,
            faceBufferSize
          );
        }
      }

      int blockCount = blockBufferSize / ARMeshConstants.INTS_PER_BLOCK;
      bool gotAllBlocks = fullBlocks == blockCount;

      if (fullBlocks < 0)
      {
        ARLog._Error("MeshSaver: Error calling GetBlockMesh(), will not update the mesh.");
        return;
      }

      if (fullBlocks == 0)
      {
        ARLog._Error("MeshSaver: GetBlockMesh() gave us an empty mesh, will not update.");
        return;
      }

      if (!gotAllBlocks)
      {
        ARLog._Error
        (
          "MeshSaver: GetBlockMesh() returned " +
          fullBlocks +
          " full blocks, expected " +
          blockCount
        );

        return;
      }

      float meshBlockSize = _mesh.MeshBlockSize;

      Directory.CreateDirectory(_meshPath);
      string filename = "mesh_" + version + ".bin";
      byte[] magicWord = FileARMesh.MagicWord;

      using (BinaryWriter writer = new BinaryWriter
        (File.Open(_meshPath + "/" + filename, FileMode.Create)))
      {
        // 16 bytes: signature
        writer.Write(magicWord);
        // 16 bytes: array lengths
        writer.Write(blockBufferSize);
        writer.Write(vertexBufferSize);
        writer.Write(faceBufferSize);
        writer.Write(meshBlockSize);
        // bulk of the data:
        writer.Write(blockArray);
        writer.Write(vertexArray);
        writer.Write(faceArray);
      }

      ARLog._Debug("MeshSaver: successfully written to " + filename);
    }
  }
}
