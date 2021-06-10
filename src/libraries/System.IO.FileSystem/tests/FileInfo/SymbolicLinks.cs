// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace System.IO.Tests
{
    public class FileInfo_SymbolicLinks : BaseSymbolicLinks_FileSystemInfo
    {
        protected override FileSystemInfo CreateFileSystemInfo(string path) =>
            new FileInfo(path);

        protected override void CreateFileSystemEntry(string path) =>
            File.Create(path).Dispose();

        protected override void DeleteFileSystemEntry(string path) =>
            File.Delete(path);

        protected override void CheckIsDirectory(FileSystemInfo fsi)
        {
            if (fsi.Exists)
            {
                Assert.False(fsi.Attributes.HasFlag(FileAttributes.Directory));
            }
            Assert.True(fsi is FileInfo);
        }

        protected override void CheckLinkExists(FileSystemInfo link) =>
            Assert.True(link.Exists); // For file symlinks, we return the exists info from the actual link, not the target

        protected override void CheckExistsWhenNoTarget(FileSystemInfo link) =>
            Assert.True(link.Exists);

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_WrongTargetType()
        {
            // fileLink => directory

            string targetPath = GetRandomFilePath();
            Directory.CreateDirectory(targetPath);

            string linkPath = GetRandomLinkPath();
            var linkInfo = new FileInfo(linkPath);

            Assert.Throws<IOException>(() => linkInfo.CreateAsSymbolicLink(targetPath));
        }

        [Fact]
        public void ResolveLinkTarget_LinkDoesNotExist() =>
            ResolveLinkTarget_LinkDoesNotExist_Internal<FileNotFoundException>();
    }
}
