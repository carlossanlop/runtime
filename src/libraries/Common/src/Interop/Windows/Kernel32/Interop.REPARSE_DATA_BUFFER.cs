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
        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct REPARSE_DATA_BUFFER
        {
            public uint ReparseTag;
            public ushort ReparseDataLength;
            public ushort Reserved;
            public SymbolicLinkReparseBuffer ReparseBufferSymbolicLink;

            // We don't need all the fields; commenting out the rest.

            //public MountPointReparseBuffer ReparseBufferMountPoint;
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
            //}

            //public struct GenericReparseBuffer
            //{
            //    public byte DataBuffer;
            //}
        }
    }
}
