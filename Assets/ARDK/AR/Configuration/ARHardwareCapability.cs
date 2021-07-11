// Copyright 2021 Niantic, Inc. All Rights Reserved.

namespace Niantic.ARDK.AR.Configuration
{
  /// @brief Possible states of capability for the current hardware device regarding a particular 
  /// configuration.
  public enum ARHardwareCapability
  {
    /// Hardware device is not capable for the particular configuration.
    NotCapable = 0,

    /// Hardware device capability check failed.
    CheckFailedWithError = 1,

    /// Hardware device capability check timed out.
    CheckFailedWithTimeout = 2,

    /// Hardware device is capable of the particular configuration.
    Capable = 4,
  }
}
