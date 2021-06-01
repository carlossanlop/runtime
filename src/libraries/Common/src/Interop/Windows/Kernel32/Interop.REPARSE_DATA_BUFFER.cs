// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static partial class Kernel32
    {
        // https://msdn.microsoft.com/en-us/library/windows/hardware/ff552012.aspx
        [StructLayout(LayoutKind.Sequential)]
        public struct REPARSE_DATA_BUFFER
        {
            public ulong ReparseTag;
            public ushort ReparseDataLength;
            public ushort Reserved;
            public SymbolicLinkReparseBuffer ReparseBufferSymbolicLink;//SymbolicLinkData;
            public MountPointReparseBuffer ReparseBufferMountPoint;//MountPointData;
            public GenericReparseBuffer ReparseBufferGeneric;//GenericData;

            [StructLayout(LayoutKind.Sequential)]
            public struct SymbolicLinkReparseBuffer
            {
                private readonly ushort SubstituteNameOffset;
                private readonly ushort SubstituteNameLength;
                private readonly ushort PrintNameOffset;
                private readonly ushort PrintNameLength;
                public uint Flags;
                private char _PathBuffer;
                // public ReadOnlySpan<char> SubstituteName => TrailingArray<char>.GetBufferInBytes(in _PathBuffer, SubstituteNameLength, SubstituteNameOffset);
                // public ReadOnlySpan<char> PrintName => TrailingArray<char>.GetBufferInBytes(in _PathBuffer, PrintNameLength, PrintNameOffset);
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MountPointReparseBuffer
            {
                private readonly ushort SubstituteNameOffset;
                private readonly ushort SubstituteNameLength;
                private readonly ushort PrintNameOffset;
                private readonly ushort PrintNameLength;
                private char _PathBuffer;
                // public ReadOnlySpan<char> SubstituteName => TrailingArray<char>.GetBufferInBytes(in _PathBuffer, SubstituteNameLength, SubstituteNameOffset);
                // public ReadOnlySpan<char> PrintName => TrailingArray<char>.GetBufferInBytes(in _PathBuffer, PrintNameLength, PrintNameOffset);
            }

            public struct GenericReparseBuffer
            {
                public byte DataBuffer;
            }
        }
    }
}
