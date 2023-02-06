// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Formats.Zip;

internal enum CompressionMethodValues : ushort
{
    Stored = 0x0,
    Deflate = 0x8,
    Deflate64 = 0x9,
    BZip2 = 0xC,
    LZMA = 0xE
}
