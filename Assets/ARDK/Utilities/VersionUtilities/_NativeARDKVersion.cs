// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Runtime.InteropServices;

using Niantic.ARDK.AR;
using Niantic.ARDK.Utilities;
using UnityEngine;
using Niantic.ARDK.Internals;

namespace Niantic.ARDK.Utilities.VersionUtilities
{
    internal sealed class _NativeARDKVersion:
        _IARDKVersion
      {
        private string _ARDKVersion;

        public string getARDKVersion()
        {
            if (NativeAccess.Mode == NativeAccess.ModeType.Native)
            {
                var ptr = _NAR_VersionInfo_GetARDKVersion();
                return ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) : null;
            }
    #pragma warning disable 0162
            return "Editor";
    #pragma warning restore 0162

        }

        public string getARBEVersion()
        {
            if (NativeAccess.Mode == NativeAccess.ModeType.Native)
            {
                var ptr = _NAR_VersionInfo_GetARBEVersion();
                return ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) : null;
            }
    #pragma warning disable 0162
            return "Editor";
    #pragma warning restore 0162

        }

        [DllImport(_ARDKLibrary.libraryName)]
        private static extern IntPtr _NAR_VersionInfo_GetARDKVersion();

        [DllImport(_ARDKLibrary.libraryName)]
        private static extern IntPtr _NAR_VersionInfo_GetARBEVersion();
    }
}
