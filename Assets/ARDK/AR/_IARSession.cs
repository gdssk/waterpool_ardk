// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;

using Niantic.ARDK.AR;
using Niantic.ARDK.AR.Configuration;

namespace Niantic.ARDK.AR
{
  internal interface _IARSession : IARSession
  {
    _ARConfigChangesCollector _ARConfigChangesCollector { get; }

    /// Gets how this session will transition the AR state when re-run.
    ARSessionRunOptions RunOptions { get; }
  }
}
