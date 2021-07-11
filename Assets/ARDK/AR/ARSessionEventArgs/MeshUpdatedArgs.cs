// Copyright 2021 Niantic, Inc. All Rights Reserved.

using Niantic.ARDK.AR.Mesh;
using Niantic.ARDK.Utilities;

namespace Niantic.ARDK.AR.ARSessionEventArgs
{
  public struct MeshUpdatedArgs:
    IArdkEventArgs
  {
    internal MeshUpdatedArgs(IARMesh mesh)
    {
      Mesh = mesh;
    }

    public IARMesh Mesh { get; private set; }
  }
}
