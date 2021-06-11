// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace System.IO.Tests
{
    public class FileInfo_SymbolicLinks : BaseSymbolicLinks_FileSystemInfo
    {
        protected override FileSystemInfo GetFileSystemInfo(string path) =>
            new FileInfo(path);

        protected override void CreateFileOrDirectory(string path) =>
            File.Create(path).Dispose();

        protected override void DeleteFileOrDirectory(string path) =>
            File.Delete(path);

        protected override void AssertIsDirectory(FileSystemInfo fsi)
        {
            if (fsi.Exists)
            {
                Assert.False(fsi.Attributes.HasFlag(FileAttributes.Directory));
            }
            Assert.True(fsi is FileInfo);
        }

        protected override void AssertLinkExists(FileSystemInfo link) =>
            Assert.True(link.Exists); // For file symlinks, we return the exists info from the actual link, not the target

        protected override void AssertExistsWhenNoTarget(FileSystemInfo link) =>
            Assert.True(link.Exists);

        [Fact]
        public void CreateSymbolicLink_WrongTargetType()
        {
            // fileLink => directory

            string targetPath = GetRandomFilePath();
            Directory.CreateDirectory(targetPath); // Creating directory when the link is a FileInfo

            string linkPath = GetRandomLinkPath();
            var link = new FileInfo(linkPath);

            Assert.Throws<IOException>(() => link.CreateAsSymbolicLink(targetPath));
        }

        [Fact]
        public void ResolveLinkTarget_LinkDoesNotExist() =>
            ResolveLinkTarget_LinkDoesNotExist_Internal<FileNotFoundException>();
    }
}
