// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.IO.Compression
{
    public class TarFile
    {
        public static TarArchive OpenRead(string archiveFileName) => Open(archiveFileName, TarArchiveMode.Read);

        public static TarArchive Open(string archiveFileName, TarArchiveMode mode)
        {
            if (string.IsNullOrEmpty(archiveFileName))
            {
                throw new ArgumentNullException(nameof(archiveFileName));
            }

            // This check will change when additional modes are supported.
            if (mode is < TarArchiveMode.Read or > TarArchiveMode.Read)
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            FileMode fileMode;
            FileAccess access;
            FileShare fileShare;

            switch (mode)
            {
                case TarArchiveMode.Read:
                    fileMode = FileMode.Open;
                    access = FileAccess.Read;
                    fileShare = FileShare.Read;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }

            FileStream fs = new FileStream(archiveFileName, fileMode, access, fileShare, bufferSize: 0x1000, useAsync: false);

            TarArchiveOptions options = new()
            {
                Mode = mode,
                LeaveOpen = false,
            };

            try
            {
                return new TarArchive(fs, options);
            }
            catch
            {
                fs.Dispose();
                throw;
            }
        }
    }
}