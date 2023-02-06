// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Formats.Zip;

[Flags]
internal enum BitFlagValues : ushort
{
    IsEncrypted = 0x1,
    DataDescriptor = 0x8,
    UnicodeFileNameAndComment = 0x800
}
