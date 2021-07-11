// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Runtime.InteropServices;

using Niantic.ARDK.Internals;

namespace Niantic.ARDK.LocationService
{
  internal sealed class _NativeLocationServiceAdapter:
    IDisposable
  {
    private ILocationService _wrapper;

    // Private handles and code to deal with native callbacks and initialization
    private IntPtr _nativeHandle = IntPtr.Zero;

    public _NativeLocationServiceAdapter
    (
      Guid stageIdentifier,
      ILocationService wrapper
    )
    {
      // Setup native C++ session
      _nativeHandle = _LocationServiceSession_Init(stageIdentifier.ToByteArray());
    }

    public void AssignWrapper(ILocationService wrapper)
    {
      if (_wrapper != null)
      {
        _wrapper.StatusUpdated -= UpdateNativeStatus;
        _wrapper.LocationUpdated -= UpdateNativeLocation;
      }

      _wrapper = wrapper;
      wrapper.StatusUpdated += UpdateNativeStatus;
      wrapper.LocationUpdated += UpdateNativeLocation;
    }

    ~_NativeLocationServiceAdapter()
    {
      Dispose();
    }

    public void Dispose()
    {
      if (_nativeHandle == IntPtr.Zero)
        return;

      _LocationServiceSession_Release(_nativeHandle);
      _nativeHandle = IntPtr.Zero;

      GC.SuppressFinalize(this);
    }

    private void UpdateNativeLocation(LocationUpdatedArgs args)
    {
      if (_nativeHandle != IntPtr.Zero)
      {
        _LocationServiceSession_LocationUpdate
        (
          _nativeHandle,
          args.Altitude,
          args.HorizontalAccuracy,
          args.Latitude,
          args.Longitude,
          args.Timestamp,
          args.VerticalAccuracy
        );
      }
    }

    private void UpdateNativeStatus(LocationStatusUpdatedArgs args)
    {
      if (_nativeHandle != IntPtr.Zero)
        _LocationServiceSession_StatusUpdate(_nativeHandle, (UInt64) args.Status);
    }

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern IntPtr _LocationServiceSession_Init(byte[] stageIdentifier);

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern void _LocationServiceSession_StatusUpdate
      (IntPtr nativeHandle, UInt64 status);

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern void _LocationServiceSession_LocationUpdate
    (
      IntPtr nativeHandle,
      float altitude,
      float horizontalAccuracy,
      float latitude,
      float longitude,
      double timestamp,
      float verticalAccuracy
    );

    [DllImport(_ARDKLibrary.libraryName)]
    private static extern void _LocationServiceSession_Release(IntPtr nativeHandle);
  }
}
