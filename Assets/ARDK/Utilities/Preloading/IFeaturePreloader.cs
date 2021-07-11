// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;

namespace Niantic.ARDK.Utilities.Preloading
{
  /// Interface for ARDK class that pre-downloads all necessary assets for certain ARDK features
  /// to function. If the files are not preloaded, they will take time to download when an
  /// ARSession configured to use those features is run.
  public interface IFeaturePreloader:
    IDisposable
  {
    /// @param feature
    /// @returns
    ///   A value in the range of [0.0, 1.0] representing how much progress has been made
    ///   downloading the specified feature.
    float GetProgress(Feature feature);

    /// @param feature
    /// @returns
    ///   The current preload state of the specified feature. If this feature was cleared from cache
    ///   after it was completely downloaded, it will still return the last known download state.
    PreloadedFeatureState GetStatus(Feature feature);

    /// @param feature
    /// @returns True if the specified feature was found in the application's cache.
    bool ExistsInCache(Feature feature);

    /// Begin the download of all added features. Calling this after an ARSession has already run
    /// is undefined behaviour.
    void Download(Feature[] features);

    /// Clears this feature from the application's cache. Calling this while a download is in process,
    /// or while downloaded features are being used in an ARSession, is invalid and will result in
    /// undefined behaviour.
    void ClearCache(Feature feature);
  }
}