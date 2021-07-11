// Copyright 2021 Niantic, Inc. All Rights Reserved.

namespace Niantic.ARDK.Utilities.VersionUtilities
{
  public static class ARDKGlobalVersion
  {
    private static _IARDKVersion _impl;

    private static _IARDKVersion _Impl
    {
      get
      {
        if (_impl == null)
        {
          _impl = new _NativeARDKVersion();
        }

        return _impl;
      }
    }

    public static string getARDKVersion()
    {
      return _Impl.getARDKVersion();
    }

    // This returns an empty string if not connected to ARBE. Call after OnNetworkConnected
    // to get valid ARBE version info
    public static string getARBEVersion()
    {
      return _Impl.getARBEVersion();
    }
  }
}
