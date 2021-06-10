// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Xunit;

namespace System.IO.Tests
{
    public abstract class BaseSymbolicLinks_FileSystem : BaseSymbolicLinks
    {
        protected abstract void CreateFileSystemEntry(string path);
        protected abstract void DeleteFileSystemEntry(string path);
        protected abstract void CheckIsDirectory(FileSystemInfo fsi);
        protected abstract void CheckLinkExists(FileSystemInfo link);
        protected abstract void CheckExistsWhenNoTarget(FileSystemInfo link);

        protected void Attach()
        {
            while (!Debugger.IsAttached)
            {
                Console.WriteLine($"Attach to {Environment.ProcessId}");
                Threading.Thread.Sleep(1000);
            }
            Console.WriteLine("Attached!");
            Debugger.Break();
        }
    }
}