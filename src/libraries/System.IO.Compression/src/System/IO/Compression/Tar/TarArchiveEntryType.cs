// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.IO.Compression
{
    public enum TarArchiveEntryType
    {
        OldNormal = '\0',
        Normal = '0',
        Link = '1',
        SymbolicLink = '2',
        Character = '3',
        Block = '4',
        Directory = '5',
        Fifo = '6',
        DirectoryEntry = 'D',
        LongLink = 'K',
        LongPath = 'L',

        // PAX entry types that need to be handled internally:
        // - Extended attributes: x
        // - Global extended attributes: g

        // GNU entry types currently not implemented/supported:
        // - Contiguous file: 7 - should be treated as 0, it's extremely rare to handle it as contiguous.
        // - Multi-volume file: M
        // - File to be renamed/symlinked: N - unsafe and already ignored by other tools.
        // - Sparse regular file: S
        // - Tape volume: V
    }
}
