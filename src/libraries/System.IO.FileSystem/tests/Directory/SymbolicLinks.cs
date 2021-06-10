// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Xunit;

namespace System.IO.Tests
{
    public class Directory_SymbolicLinks : BaseSymbolicLinks_FilesAndDirectories
    {
        protected override void CreateFileSystemEntry(string path) =>
            Directory.CreateDirectory(path);

        protected override void DeleteFileSystemEntry(string path) =>
            Directory.Delete(path, recursive: true);

        protected override FileSystemInfo CreateSymbolicLink(string path, string pathToTarget) =>
            Directory.CreateSymbolicLink(path, pathToTarget);

        protected override FileSystemInfo ResolveLinkTarget(string linkPath, bool returnFinalTarget = false) =>
            Directory.ResolveLinkTarget(linkPath, returnFinalTarget);

        protected override void CheckIsDirectory(FileSystemInfo fsi)
        {
            if (fsi.Exists)
            {
                Assert.True(fsi.Attributes.HasFlag(FileAttributes.Directory));
            }
            Assert.True(fsi is DirectoryInfo);
        }

        protected override void CheckLinkExists(FileSystemInfo link) =>
            Assert.False(link.Exists); // For directory symlinks, we return the exists info from the target

        // When the directory target does not exist FileStatus.GetExists returns false because:
        // - We check _exists (which whould be true because the link itself exists).
        // - We check InitiallyDirectory, which is the initial expected object type (which would be true).
        // - We check _directory (false because the target directory does not exist)
        protected override void CheckExistsWhenNoTarget(FileSystemInfo link) =>
            Assert.False(link.Exists);

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void EnumerateDirectories_LinksWithCycles_ShouldNotThrow()
        {
            DirectoryInfo testDirectory = CreateDirectoryContainingSelfReferencingSymbolicLink();

            // Windows differentiates between dir symlinks and file symlinks
            int expected = OperatingSystem.IsWindows() ? 1 : 0;
            Assert.Equal(expected, Directory.EnumerateDirectories(testDirectory.FullName).Count());
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void EnumerateFiles_LinksWithCycles_ShouldNotThrow()
        {
            DirectoryInfo testDirectory = CreateDirectoryContainingSelfReferencingSymbolicLink();

            // Windows differentiates between dir symlinks and file symlinks
            int expected = OperatingSystem.IsWindows() ? 0 : 1;
            Assert.Equal(expected, Directory.EnumerateFiles(testDirectory.FullName).Count());
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void EnumerateFileSystemEntries_LinksWithCycles_ShouldNotThrow()
        {
            DirectoryInfo testDirectory = CreateDirectoryContainingSelfReferencingSymbolicLink();
            Assert.Single(Directory.EnumerateFileSystemEntries(testDirectory.FullName));
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_WrongTargetType()
        {
            // dirLink -> file

            string targetPath = GetRandomFilePath();
            File.Create(targetPath).Dispose(); // The underlying file system entry needs to be a file
            Assert.Throws<IOException>(() => CreateSymbolicLink(GetRandomFilePath(), targetPath));
        }

        [Fact]
        public void ResolveLinkTarget_LinkDoesNotExist() =>
            ResolveLinkTarget_LinkDoesNotExist_Internal<DirectoryNotFoundException>();
    }
}
