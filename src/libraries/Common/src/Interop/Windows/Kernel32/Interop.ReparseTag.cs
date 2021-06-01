// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

internal static partial class Interop
{
    internal static partial class Kernel32
    {
        /// <summary>
        /// Represents all the Reparse Tag values as described in the File System Control Codes [MS-FSCC] spec document:
        /// https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-fscc/c8e77b37-3909-4fe6-a4ea-2b9d423b1ee4
        /// </summary>
        internal enum ReparseTag : uint
        {
            /// <summary>
            /// Reserved reparse tag value.
            /// </summary>
            IO_REPARSE_TAG_RESERVED_ZERO = 0x00000000,
            /// <summary>
            /// Reserved reparse tag value.
            /// </summary>
            IO_REPARSE_TAG_RESERVED_ONE = 0x00000001,
            /// <summary>
            /// Reserved reparse tag value.
            /// </summary>
            IO_REPARSE_TAG_RESERVED_TWO = 0x00000002,
            /// <summary>
            /// Used for mount point support, specified in section 2.1.2.5.
            /// This reparse tag is otherwise known as Junction.
            /// </summary>
            IO_REPARSE_TAG_MOUNT_POINT = 0xA0000003,
            /// <summary>
            /// Obsolete. Used by legacy Hierarchical Storage Manager Product.
            /// </summary>
            IO_REPARSE_TAG_HSM = 0xC0000004,
            /// <summary>
            /// Home server drive extender.
            /// The Windows Home Server Drive Extender is part of the Windows Home Server product.
            /// </summary>
            IO_REPARSE_TAG_DRIVE_EXTENDER = 0x80000005,
            /// <summary>
            /// Obsolete. Used by legacy Hierarchical Storage Manager Product.
            /// </summary>
            IO_REPARSE_TAG_HSM2 = 0x80000006,
            /// <summary>
            /// Used by single-instance storage (SIS) filter driver. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_SIS = 0x80000007,
            /// <summary>
            /// Used by the WIM Mount filter. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_WIM = 0x80000008,
            /// <summary>
            /// Obsolete. Used by Clustered Shared Volumes (CSV) version 1 in Windows Server 2008 R2 operating system. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_CSV = 0x80000009,
            /// <summary>
            /// Used by the DFS filter. The DFS is described in the Distributed File System (DFS): Referral Protocol Specification [MS-DFSC]. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_DFS = 0x8000000A,
            /// <summary>
            /// Used by filter manager test harness.
            /// The filter manager test harness is not shipped with Windows.
            /// </summary>
            IO_REPARSE_TAG_FILTER_MANAGER = 0x8000000B,
            /// <summary>
            /// Used for symbolic link support.
            /// </summary>
            IO_REPARSE_TAG_SYMLINK = 0xA000000C,
            /// <summary>
            /// Used by Microsoft Internet Information Services (IIS) caching. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_IIS_CACHE = 0xA0000010,
            /// <summary>
            /// Used by the DFS filter. The DFS is described in [MS-DFSC]. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_DFSR = 0x80000012,
            /// <summary>
            /// Used by the Data Deduplication (Dedup) filter. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_DEDUP = 0x80000013,
            /// <summary>
            /// Not used.
            /// </summary>
            IO_REPARSE_TAG_APPXSTRM = 0xC0000014,
            /// <summary>
            /// Used by the Network File System (NFS) component. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_NFS = 0x80000014,
            /// <summary>
            /// Obsolete. Used by Windows Shell for legacy placeholder files in Windows 8.1. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_FILE_PLACEHOLDER = 0x80000015,
            /// <summary>
            /// Used by the Dynamic File filter. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_DFM = 0x80000016,
            /// <summary>
            /// Used by the Windows Overlay filter, for either WIMBoot or single-file compression. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_WOF = 0x80000017,
            /// <summary>
            /// Used by the Windows Container Isolation filter. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_WCI = 0x80000018,
            /// <summary>
            /// Used by the Windows Container Isolation filter. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_WCI_1 = 0x90001018,
            /// <summary>
            /// Used by NPFS to indicate a named pipe symbolic link from a server silo into the host silo. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_GLOBAL_REPARSE = 0xA0000019,
            /// <summary>
            /// Used by the Cloud Files filter, for files managed by a sync engine such as Microsoft OneDrive. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_CLOUD = 0x9000001A,
            /// <summary>
            /// Used by the Cloud Files filter, for files managed by a sync engine such as OneDrive. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_CLOUD_1 = 0x9000101A,
            /// <summary>
            /// Used by the Cloud Files filter, for files managed by a sync engine such as OneDrive. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_CLOUD_2 = 0x9000201A,
            /// <summary>
            /// Used by the Cloud Files filter, for files managed by a sync engine such as OneDrive. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_CLOUD_3 = 0x9000301A,
            /// <summary>
            /// Used by the Cloud Files filter, for files managed by a sync engine such as OneDrive. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_CLOUD_4 = 0x9000401A,
            /// <summary>
            /// Used by the Cloud Files filter, for files managed by a sync engine such as OneDrive. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_CLOUD_5 = 0x9000501A,
            /// <summary>
            /// Used by the Cloud Files filter, for files managed by a sync engine such as OneDrive. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_CLOUD_6 = 0x9000601A,
            /// <summary>
            /// Used by the Cloud Files filter, for files managed by a sync engine such as OneDrive. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_CLOUD_7 = 0x9000701A,
            /// <summary>
            /// Used by the Cloud Files filter, for files managed by a sync engine such as OneDrive. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_CLOUD_8 = 0x9000801A,
            /// <summary>
            /// Used by the Cloud Files filter, for files managed by a sync engine such as OneDrive. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_CLOUD_9 = 0x9000901A,
            /// <summary>
            /// Used by the Cloud Files filter, for files managed by a sync engine such as OneDrive. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_CLOUD_A = 0x9000A01A,
            /// <summary>
            /// Used by the Cloud Files filter, for files managed by a sync engine such as OneDrive. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_CLOUD_B = 0x9000B01A,
            /// <summary>
            /// Used by the Cloud Files filter, for files managed by a sync engine such as OneDrive. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_CLOUD_C = 0x9000C01A,
            /// <summary>
            /// Used by the Cloud Files filter, for files managed by a sync engine such as OneDrive. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_CLOUD_D = 0x9000D01A,
            /// <summary>
            /// Used by the Cloud Files filter, for files managed by a sync engine such as OneDrive. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_CLOUD_E = 0x9000E01A,
            /// <summary>
            /// Used by the Cloud Files filter, for files managed by a sync engine such as OneDrive. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_CLOUD_F = 0x9000F01A,
            /// <summary>
            /// Used by Universal Windows Platform (UWP) packages to encode information that allows the application to be launched by CreateProcess. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_APPEXECLINK = 0x8000001B,
            /// <summary>
            /// Used by the Windows Projected File System filter, for files managed by a user mode provider such as VFS for Git. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_PROJFS = 0x9000001C,
            /// <summary>
            /// Used by the Windows Subsystem for Linux (WSL) to represent a UNIX symbolic link. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_LX_SYMLINK = 0xA000001D,
            /// <summary>
            /// Used by the Azure File Sync (AFS) filter. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_STORAGE_SYNC = 0x8000001E,
            /// <summary>
            /// Used by the Windows Container Isolation filter. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_WCI_TOMBSTONE = 0xA000001F,
            /// <summary>
            /// Used by the Windows Container Isolation filter. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_UNHANDLED = 0x80000020,
            /// <summary>
            /// Not used.
            /// </summary>
            IO_REPARSE_TAG_ONEDRIVE = 0x80000021,
            /// <summary>
            /// Used by the Windows Projected File System filter, for files managed by a user mode provider such as VFS for Git. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_PROJFS_TOMBSTONE = 0xA0000022,
            /// <summary>
            /// Used by the Windows Subsystem for Linux (WSL) to represent a UNIX domain socket. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_AF_UNIX = 0x80000023,
            /// <summary>
            /// Used by the Windows Subsystem for Linux (WSL) to represent a UNIX FIFO (named pipe). Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_LX_FIFO = 0x80000024,
            /// <summary>
            /// Used by the Windows Subsystem for Linux (WSL) to represent a UNIX character special file. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_LX_CHR = 0x80000025,
            /// <summary>
            /// Used by the Windows Subsystem for Linux (WSL) to represent a UNIX block special file. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_LX_BLK = 0x80000026,
            /// <summary>
            /// Used by the Windows Container Isolation filter. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_WCI_LINK = 0xA0000027,
            /// <summary>
            /// Used by the Windows Container Isolation filter. Server-side interpretation only, not meaningful over the wire.
            /// </summary>
            IO_REPARSE_TAG_WCI_LINK_1 = 0xA0001027
        };
    }
}
