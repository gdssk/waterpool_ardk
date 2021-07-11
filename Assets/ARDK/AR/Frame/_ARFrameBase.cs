// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using Niantic.ARDK.AR.Depth;

namespace Niantic.ARDK.AR.Frame
{
  // This class only exists to provide the fields that in the past existed in the ARFrame
  // (not in the Impl classes) as { get; internal set; }.
  // TODO: We should probably refactor so this doesn't exist anymore.
  [Serializable]
  internal abstract class _ARFrameBase
  {
    public IARPointCloud DepthFeaturePoints { get; internal set; }
  }
}
