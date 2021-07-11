// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;

using Niantic.ARDK.AR.Depth.Generators;

namespace Niantic.ARDK.AR.Configuration
{
  public static class ARWorldTrackingConfigurationFactory
  {
    /// Perform an asynchronous check as to whether the hardware and software are capable of and
    /// support the ARWorldTrackingConfiguration.
    /// @note Returns ARHardwareCapability.Capable and ARSoftwareSupport.Supported when run
    /// in the editor by default.
    public static void CheckCapabilityAndSupport
    (
      Action<ARHardwareCapability, ARSoftwareSupport> callback
    )
    {
      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
        _NativeARWorldTrackingConfiguration._CheckCapabilityAndSupport(callback);
      #pragma warning disable 0162
      else
        callback(ARHardwareCapability.Capable, ARSoftwareSupport.Supported);
      #pragma warning restore 0162
    }

    /// Check whether the device supports depth estimation.
    /// @note Returns true when run in the Unity Editor.
    public static bool CheckDepthEstimationSupport()
    {
      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
        return _NativeARWorldTrackingConfiguration._CheckDepthEstimationSupport();

      #pragma warning disable 0162
      return true;
      #pragma warning restore 0162
    }

    /// Check whether the device supports semantic segmentation.
    /// @note Returns true when run in the Unity Editor.
    public static bool CheckSemanticSegmentationSupport()
    {
      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
        return _NativeARWorldTrackingConfiguration._CheckSemanticSegmentationSupport();

      #pragma warning disable 0162
      return true;
      #pragma warning restore 0162
    }

    /// Check whether the device supports meshing.
    /// @note Returns true when run in the Unity Editor.
    public static bool CheckMeshingSupport()
    {
      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
        return _NativeARWorldTrackingConfiguration._CheckMeshingSupport();

      #pragma warning disable 0162
      return true;
      #pragma warning restore 0162
    }

    /// Initializes a new instance of the ARWorldTrackingConfiguration class.
    public static IARWorldTrackingConfiguration Create()
    {
      if (NativeAccess.Mode == NativeAccess.ModeType.Native)
        return new _NativeARWorldTrackingConfiguration();

      #pragma warning disable 0162
      return new _SerializableARWorldTrackingConfiguration();
      #pragma warning restore 0162
    }
  }
}
