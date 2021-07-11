// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;

using Niantic.ARDK.AR;
using Niantic.ARDK.AR.Mesh;
using Niantic.ARDK.Utilities.Logging;

using UnityEngine;

namespace Niantic.ARDK.VirtualStudio.AR.Mock
{
  /// This script can load individual mesh files saved with Niantic.ARDK.Extensions.Meshing.MeshSaver
  /// into the Unity Editor play mode.
  /// Use MockMeshSequence to load mesh sequences.
  public sealed class MockMesh:
    MockDetectableBase
  {
    /// _meshPath is the path to a single mesh file (mesh_*.bin) in the project.
    [SerializeField]
    private string _meshPath = null;

    internal override void BeDiscovered(_IMockARSession arSession, bool isLocal)
    {
      if (!isLocal)
        return;

      ARLog._Debug("Will load the mesh @ " + _meshPath);

      var loadedMesh = new FileARMesh(_meshPath);

      arSession.UpdateMesh(loadedMesh);
    }

    internal override void OnSessionRanAgain(_IMockARSession arSession)
    {
      if ((arSession.RunOptions & ARSessionRunOptions.RemoveExistingMesh) != 0)
      {
        throw new NotImplementedException("Removing meshes is not yet supported in Virtual Studio");
      }
    }
  }
}
