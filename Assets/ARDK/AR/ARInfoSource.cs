// Copyright 2021 Niantic, Inc. All Rights Reserved.

namespace Niantic.ARDK.AR
{
  /// An enum of all possible sources of "reality" for the ARDK framework.
  /// This enum is probably going to get more items in future versions of the ARDK.
  public enum ARInfoSource
  {
    Default,

    /// Reality data is "live" (that is, an actual camera or similar is being used).
    LiveDevice,

    /// Reality data is coming from a remote source.
    Remote,

    /// The "reality" seen by the framework is completely code based. This is the most useful for
    /// tests.
    Mock

    // TODO: Playback, if added in this enum, should probably be named Recorded, as the source
    // of a playback is a "recorded data set".
  }
}
