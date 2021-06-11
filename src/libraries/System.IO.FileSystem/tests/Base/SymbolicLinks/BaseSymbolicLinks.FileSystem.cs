// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Xunit;

namespace System.IO.Tests
{
    public abstract class BaseSymbolicLinks_FileSystem : BaseSymbolicLinks
    {
        protected abstract void CreateFileOrDirectory(string path);
        protected abstract void DeleteFileOrDirectory(string path);
        protected abstract void AssertIsDirectory(FileSystemInfo fsi);
        protected abstract void AssertLinkExists(FileSystemInfo link);
        protected abstract void AssertExistsWhenNoTarget(FileSystemInfo link);
    }
}