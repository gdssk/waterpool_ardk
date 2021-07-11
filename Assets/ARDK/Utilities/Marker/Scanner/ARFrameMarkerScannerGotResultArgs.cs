// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;

namespace Niantic.ARDK.Utilities.Marker
{
  public struct ARFrameMarkerScannerGotResultArgs:
    IArdkEventArgs
  {
    public readonly IParserResult ParserResult;

    internal ARFrameMarkerScannerGotResultArgs(IParserResult parserResult)
    {
      ParserResult = parserResult;
    }
  }
}