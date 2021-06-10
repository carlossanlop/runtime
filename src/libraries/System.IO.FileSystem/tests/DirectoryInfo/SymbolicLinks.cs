// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Xunit;

namespace System.IO.Tests
{
    public class DirectoryInfo_SymbolicLinks : BaseSymbolicLinks_FileSystemInfo
    {

        protected override FileSystemInfo CreateFileSystemInfo(string path) =>
            new DirectoryInfo(path);

        protected override void CreateFileSystemEntry(string path) =>
            Directory.CreateDirectory(path);

        protected override void DeleteFileSystemEntry(string path) =>
            Directory.Delete(path, recursive: true);

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

        [ConditionalTheory(nameof(CanCreateSymbolicLinks))]
        [InlineData(false)]
        [InlineData(true)]
        public void EnumerateDirectories_LinksWithCycles_ThrowsTooManyLevelsOfSymbolicLinks(bool recurse)
        {
            var options  = new EnumerationOptions() { RecurseSubdirectories = recurse };
            DirectoryInfo testDirectory = CreateDirectoryContainingSelfReferencingSymbolicLink();

            // Windows avoids accessing the cyclic symlink if we do not recurse
            if (OperatingSystem.IsWindows() && !recurse)
            {
                testDirectory.EnumerateDirectories("*", options).Count();
                testDirectory.GetDirectories("*", options).Count();
            }
            else
            {
                // Internally transforms the FileSystemEntry to a DirectoryInfo, which performs a disk hit on the cyclic symlink
                Assert.Throws<IOException>(() => testDirectory.EnumerateDirectories("*", options).Count());
                Assert.Throws<IOException>(() => testDirectory.GetDirectories("*", options).Count());
            }
        }

        [ConditionalTheory(nameof(CanCreateSymbolicLinks))]
        [InlineData(false)]
        [InlineData(true)]
        public void EnumerateFiles_LinksWithCycles_ThrowsTooManyLevelsOfSymbolicLinks(bool recurse)
        {
            var options  = new EnumerationOptions() { RecurseSubdirectories = recurse };
            DirectoryInfo testDirectory = CreateDirectoryContainingSelfReferencingSymbolicLink();

            // Windows avoids accessing the cyclic symlink if we do not recurse
            if (OperatingSystem.IsWindows() && !recurse)
            {
                testDirectory.EnumerateFiles("*", options).Count();
                testDirectory.GetFiles("*", options).Count();
            }
            else
            {
                // Internally transforms the FileSystemEntry to a FileInfo, which performs a disk hit on the cyclic symlink
                Assert.Throws<IOException>(() => testDirectory.EnumerateFiles("*", options).Count());
                Assert.Throws<IOException>(() => testDirectory.GetFiles("*", options).Count());
            }
        }

        [ConditionalTheory(nameof(CanCreateSymbolicLinks))]
        [InlineData(false)]
        [InlineData(true)]
        public void EnumerateFileSystemInfos_LinksWithCycles_ThrowsTooManyLevelsOfSymbolicLinks(bool recurse)
        {
            var options  = new EnumerationOptions() { RecurseSubdirectories = recurse };
            DirectoryInfo testDirectory = CreateDirectoryContainingSelfReferencingSymbolicLink();

            // Windows avoids accessing the cyclic symlink if we do not recurse
            if (OperatingSystem.IsWindows() && !recurse)
            {
                testDirectory.EnumerateFileSystemInfos("*", options).Count();
                testDirectory.GetFileSystemInfos("*", options).Count();
            }
            else
            {
                // Internally transforms the FileSystemEntry to a FileSystemInfo, which performs a disk hit on the cyclic symlink
                Assert.Throws<IOException>(() => testDirectory.EnumerateFileSystemInfos("*", options).Count());
                Assert.Throws<IOException>(() => testDirectory.GetFileSystemInfos("*", options).Count());
            }
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_WrongTargetType()
        {
            // dirLink -> file

            string targetPath = GetRandomFilePath();
            File.Create(targetPath).Dispose();

            string linkPath = GetRandomFilePath();
            var linkInfo = new DirectoryInfo(linkPath);

            Assert.Throws<IOException>(() => linkInfo.CreateAsSymbolicLink(targetPath));
        }

        [Fact]
        public void ResolveLinkTarget_LinkDoesNotExist() =>
            ResolveLinkTarget_LinkDoesNotExist_Internal<DirectoryNotFoundException>();
    }
}
