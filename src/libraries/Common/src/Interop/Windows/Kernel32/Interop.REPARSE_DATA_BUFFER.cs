// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static partial class Kernel32
    {
        public const int MAXIMUM_REPARSE_DATA_BUFFER_SIZE = 16 * 1024;
        public const uint SYMLINK_FLAG_RELATIVE = 1;

        // https://msdn.microsoft.com/library/windows/hardware/ff552012.aspx
        //[StructLayout(LayoutKind.Explicit)]
        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct REPARSE_DATA_BUFFER
        {
            //[FieldOffset(0)]
            public uint ReparseTag;
            //[FieldOffset(4)]
            public ushort ReparseDataLength;
            //[FieldOffset(6)]
            public ushort Reserved;
            //[FieldOffset(8)]
            public SymbolicLinkReparseBuffer ReparseBufferSymbolicLink;

            // We only need SymbolicLinkReparseBuffer.PathBuffer and its respective offsets and lengths.
            // Commenting out the rest of the definition.

            //[FieldOffset(8)]
            //public MountPointReparseBuffer ReparseBufferMountPoint;
            //[FieldOffset(8)]
            //public GenericReparseBuffer ReparseBufferGeneric;

            [StructLayout(LayoutKind.Sequential)]
            public struct SymbolicLinkReparseBuffer
            {
                public ushort SubstituteNameOffset;
                public ushort SubstituteNameLength;
                public ushort PrintNameOffset;
                public ushort PrintNameLength;
                public uint Flags;
                //private char _PathBuffer;
            }

            //[StructLayout(LayoutKind.Sequential)]
            //public struct MountPointReparseBuffer
            //{
            //    private ushort SubstituteNameOffset;
            //    private ushort SubstituteNameLength;
            //    private ushort PrintNameOffset;
            //    private ushort PrintNameLength;
            //    private char _PathBuffer;
            //    // public ReadOnlySpan<char> SubstituteName => TrailingArray<char>.GetBufferInBytes(in _PathBuffer, SubstituteNameLength, SubstituteNameOffset);
            //    // public ReadOnlySpan<char> PrintName => TrailingArray<char>.GetBufferInBytes(in _PathBuffer, PrintNameLength, PrintNameOffset);
            //}

            //public struct GenericReparseBuffer
            //{
            //    public byte DataBuffer;
            //}
        }
    }
}
