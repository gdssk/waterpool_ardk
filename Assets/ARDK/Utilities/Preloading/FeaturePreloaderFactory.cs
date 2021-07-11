// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;

using Niantic.ARDK.AR;
using Niantic.ARDK.Utilities.Logging;

namespace Niantic.ARDK.Utilities.Preloading
{
  public static class FeaturePreloaderFactory
  {
    public static IFeaturePreloader Create()
    {
      return _Create();
    }

    public static IFeaturePreloader Create(ARInfoSource source)
    {
      if (source == ARInfoSource.Remote)
      {
        ARLog._Warn
        (
          "Preloading is not yet supported over Remote. Added features will be download to the" +
          " desktop and required features will be downloaded when the ARSession is run on device."
        );
      }

      return new _NativeFeaturePreloader();
    }

    private static readonly ARInfoSource[] _bestMatches =
      new ARInfoSource[] { ARInfoSource.LiveDevice, ARInfoSource.Remote};

    internal static IFeaturePreloader _Create
    (
      IEnumerable<ARInfoSource> sources = null
    )
    {
      bool triedAtLeast1 = false;

      if (sources != null)
      {
        foreach (var source in sources)
        {
          var possibleResult = Create(source);
          if (possibleResult != null)
            return possibleResult;

          triedAtLeast1 = true;
        }
      }

      if (!triedAtLeast1)
        return _Create(_bestMatches);

      throw new NotSupportedException("None of the provided sources are supported by this build.");
    }
  }
}
