// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.IO.Compression.Tests
{
    public partial class TarTests : FileCleanupTestBase
    {
        private void CreateHardLink(string linkPath, string targetPath) => Interop.Kernel32.CreateHardLink(linkPath, targetPath);
    }
}
