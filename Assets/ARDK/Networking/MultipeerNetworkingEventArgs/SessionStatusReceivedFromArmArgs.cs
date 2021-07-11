// Copyright 2021 Niantic, Inc. All Rights Reserved.

using Niantic.ARDK.Utilities;

namespace Niantic.ARDK.Networking.MultipeerNetworkingEventArgs
{
  public struct SessionStatusReceivedFromArmArgs:
    IArdkEventArgs
  {
    internal SessionStatusReceivedFromArmArgs(uint status):
      this()
    {
      Status = status;
    }

    public uint Status { get; private set; }
  }
}
