// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.IO.Compression
{
    public enum TarArchiveEntryType
    {
        OldNormal = '\0',
        Link = '1',
        SymbolicLink = '2',
        Character = '3',
        Block = '4',
        Directory = '5',
        Fifo = '6',
        Contiguous = '7',
    }
}
