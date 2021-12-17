// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.IO.Compression
{
    public class TarArchiveOptions
    {
        public TarArchiveMode Mode { get; set; } = TarArchiveMode.Read;
        public bool LeaveOpen { get; set; }

        public TarArchiveOptions()
        {
        }
    }
}
