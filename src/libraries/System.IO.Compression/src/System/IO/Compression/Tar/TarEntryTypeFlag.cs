// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.IO.Compression.Tar
{
    // Enumerates the type flags found in a tar archive entry header.
    internal enum TarEntryTypeFlag
    {
        OldNormal = '\0',
        Normal = '0',
        Link = '1',
        SymbolicLink = '2',
        Character = '3',
        Block = '4',
        Directory = '5',
        Fifo = '6',
        Contiguous = '7',
        ExtendedAttributes = 'x',
        GlobalExtendedAttributes = 'g',
        DirectoryEntry = 'D',
        LongLink = 'K',
        LongPath = 'L',
        MultiVolume = 'M',
        RenamedOrSymlinked = 'N',
        Sparse = 'S',
        TapeVolume = 'V',


        // GNU entry types currently not implemented/supported:
        // - Contiguous file: 7 - should be treated as 0, it's extremely rare to handle it as contiguous.
        // - Multi-volume file: M
        // - File to be renamed/symlinked: N - unsafe and already ignored by other tools.
        // - Sparse regular file: S
        // - Tape volume: V
    }
}
