// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;

using Niantic.ARDK.AR;
using Niantic.ARDK.AR.SLAM;
using UnityEngine;

namespace Niantic.ARDK.VirtualStudio.AR.Mock
{
  public sealed class MockMap:
    MockDetectableBase
  {
    private Guid _identifier = Guid.NewGuid();

    internal sealed override void BeDiscovered(_IMockARSession arSession, bool isLocal)
    {
      var serialMap =
        new _SerializableARMap
        (
          _identifier,
          1.0f,
          Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one)
        );

      arSession.AddMap(serialMap);
    }

    internal override void OnSessionRanAgain(_IMockARSession arSession)
    {
      _identifier = Guid.NewGuid();
    }
  }
}