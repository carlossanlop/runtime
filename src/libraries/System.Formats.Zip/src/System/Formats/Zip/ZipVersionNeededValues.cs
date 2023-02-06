// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Formats.Zip;

internal enum ZipVersionNeededValues : ushort
{
    Default = 10,
    ExplicitDirectory = 20,
    Deflate = 20,
    Deflate64 = 21,
    Zip64 = 45
}
