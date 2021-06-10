// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Xunit;

namespace System.IO.Tests
{
    public abstract class BaseSymbolicLinks_FilesAndDirectories : BaseSymbolicLinks_FileSystem
    {
        protected abstract FileSystemInfo CreateSymbolicLink(string path, string pathToTarget);
        protected abstract FileSystemInfo ResolveLinkTarget(string linkPath, bool returnFinalTarget = false);

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_NullPathToTarget()
        {
            Assert.Throws<ArgumentNullException>(() => CreateSymbolicLink(GetRandomFilePath(), pathToTarget: null));
        }

        [ConditionalTheory(nameof(CanCreateSymbolicLinks))]
        [InlineData("")]
        [InlineData("\0")]
        public void CreateSymbolicLink_InvalidPathToTarget(string pathToTarget)
        {
            Assert.Throws<ArgumentException>(() => CreateSymbolicLink(GetRandomFilePath(), pathToTarget));
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_RelativeLinkPath()
        {
            string filePath = GetRandomFilePath();
            string dirPath = Path.GetDirectoryName(filePath);
            string relativePathToTarget = GetRandomFileName();
            CreateFileSystemEntry(Path.Join(dirPath, relativePathToTarget));
            var info = CreateSymbolicLink(filePath, relativePathToTarget);
            Assert.Equal(info.LinkTarget, relativePathToTarget);
            Assert.True(info.Exists);
            var target = ResolveLinkTarget(filePath);
            Assert.NotNull(target);
            CheckIsDirectory(target);
            Assert.True(target.Exists);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_RelativeLinkPath_NonExistentTarget()
        {
            string filePath = GetRandomFilePath();
            string nonExistentTarget = GetRandomFileName();
            var info = CreateSymbolicLink(filePath, nonExistentTarget);
            Assert.Equal(info.LinkTarget, nonExistentTarget);
            CheckExistsWhenNoTarget(info);
            var target = ResolveLinkTarget(filePath);
            Assert.NotNull(target);
            CheckIsDirectory(target);
            Assert.False(target.Exists);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_Absolute()
        {
            // /path/to/link -> /path/to/target

            string linkPath = GetRandomLinkPath();
            string targetPath = GetRandomFilePath();
            CreateSymbolicLink(
                linkPath: linkPath,
                expectedLinkTarget: targetPath,
                targetPath: targetPath);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_Relative()
        {
            // /path/to/link -> target

            string linkPath = GetRandomLinkPath();
            string targetPath = GetRandomFilePath();
            string targetName = Path.GetFileName(targetPath);
            CreateSymbolicLink(
                linkPath: linkPath,
                expectedLinkTarget: targetName,
                targetPath: targetPath);
        }

        private void CreateSymbolicLink(string linkPath, string expectedLinkTarget, string targetPath)
        {
            // linkPath -> expectedLinkTarget (created in targetPath)

            CreateFileSystemEntry(targetPath);

            var link = CreateSymbolicLink(linkPath, expectedLinkTarget);

            Assert.True(link.Exists);
            Assert.True(link.Attributes.HasFlag(FileAttributes.ReparsePoint));

            var target = ResolveLinkTarget(linkPath);
            CheckIsDirectory(target);
            Assert.True(target.Exists);
            Assert.Equal(link.LinkTarget, expectedLinkTarget);
            Assert.True(Path.IsPathFullyQualified(target.FullName));
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_NonExistentTarget()
        {
            // link -> ?

            string linkPath = GetRandomLinkPath();
            string nonExistentTargetPath = GetRandomFilePath();

            var link = CreateSymbolicLink(linkPath, nonExistentTargetPath);
            CheckExistsWhenNoTarget(link);
            Assert.True(link.Attributes.HasFlag(FileAttributes.ReparsePoint));
            var target = ResolveLinkTarget(linkPath); // Should return a FileSystemInfo instance anyway
            Assert.NotNull(target);
            CheckIsDirectory(target);
            Assert.False(target.Exists);
            Assert.Equal(link.LinkTarget, target.FullName);
        }

        protected void ResolveLinkTarget_LinkDoesNotExist_Internal<T>() where T : Exception
        {
            // ? -> ?

            string path = GetRandomFilePath();
            Assert.Throws<T>(() => ResolveLinkTarget(path));
            Assert.Throws<T>(() => ResolveLinkTarget(path, returnFinalTarget: true));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ResolveLinkTarget_FileSystemEntryExistsButIsNotALink(bool returnFinalTarget)
        {
            string path = GetRandomFilePath();
            CreateFileSystemEntry(path); // entry exists as a normal file, not as a link

            var target = ResolveLinkTarget(path, returnFinalTarget);
            Assert.NotNull(target);
            Assert.False(target.Exists);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void ResolveLinkTarget_ReturnFinalTarget_Absolute()
        {
            string link1Path = GetRandomLinkPath();
            string link2Path = GetRandomLinkPath();
            string filePath = GetRandomFilePath();

            ResolveLinkTarget_ReturnFinalTarget(
                link1Path: link1Path,
                expectedLink1Target: link2Path,
                link2Path: link2Path,
                expectedLink2Target: filePath,
                filePath: filePath);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void ResolveLinkTarget_ReturnFinalTarget_Absolute_WithRedundantSegments()
        {
            string link1Path = GetRandomLinkPath();
            string link2Path = GetRandomLinkPath();
            string filePath = GetRandomFilePath();

            string dirPath = Path.GetDirectoryName(filePath);
            string dirName = Path.GetFileName(dirPath);

            string link2FileName = Path.GetFileName(link2Path);
            string fileName = Path.GetFileName(filePath);

            ResolveLinkTarget_ReturnFinalTarget(
                link1Path: link1Path,
                expectedLink1Target: Path.Join(dirPath, "..", dirName, link2FileName),
                link2Path: link2Path,
                expectedLink2Target: Path.Join(dirPath, "..", dirName, fileName),
                filePath: filePath);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void ResolveLinkTarget_ReturnFinalTarget_Relative()
        {
            string link1Path = GetRandomLinkPath();
            string link2Path =  GetRandomLinkPath();
            string filePath = GetRandomFilePath();

            string link2FileName = Path.GetFileName(link2Path);
            string fileName = Path.GetFileName(filePath);

            ResolveLinkTarget_ReturnFinalTarget(
                link1Path: link1Path,
                expectedLink1Target: link2FileName,
                link2Path: link2Path,
                expectedLink2Target: fileName,
                filePath: filePath);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void ResolveLinkTarget_ReturnFinalTarget_Relative_WithRedundantSegments()
        {
            string link1Path = GetRandomLinkPath();
            string link2Path =  GetRandomLinkPath();
            string filePath = GetRandomFilePath();

            string dirPath = Path.GetDirectoryName(filePath);
            string dirName = Path.GetFileName(dirPath);

            string link2FileName = Path.GetFileName(link2Path);
            string fileName = Path.GetFileName(filePath);

            ResolveLinkTarget_ReturnFinalTarget(
                link1Path: link1Path,
                expectedLink1Target: Path.Join("..", dirName, link2FileName),
                link2Path: link2Path,
                expectedLink2Target: Path.Join("..", dirName, fileName),
                filePath: filePath);
        }

        private void ResolveLinkTarget_ReturnFinalTarget(string link1Path, string expectedLink1Target, string link2Path, string expectedLink2Target, string filePath)
        {
            // link1Path -> expectedLink1Target (created in link2Path) -> expectedLink2Target (created in filePath)

            CreateFileSystemEntry(filePath);

            // link2 to target
            var link2 = CreateSymbolicLink(link2Path, expectedLink2Target);
            Assert.True(link2.Exists);
            Assert.True(link2.Attributes.HasFlag(FileAttributes.ReparsePoint));
            CheckIsDirectory(link2);
            Assert.Equal(link2.LinkTarget, expectedLink2Target);

            // link1 to link2
            var link1 = CreateSymbolicLink(link1Path, expectedLink1Target);
            Assert.True(link1.Exists);
            Assert.True(link1.Attributes.HasFlag(FileAttributes.ReparsePoint));
            CheckIsDirectory(link1);
            Assert.Equal(link1.LinkTarget, expectedLink1Target);

            // link1: do not follow symlinks
            var link1Target = ResolveLinkTarget(link1Path);
            Assert.True(link1Target.Exists);
            CheckIsDirectory(link1Target);
            Assert.True(link1Target.Attributes.HasFlag(FileAttributes.ReparsePoint));
            Assert.Equal(link1Target.FullName, link2Path);

            // link2: do not follow symlinks
            var link2Target = ResolveLinkTarget(link2Path);
            Assert.True(link2Target.Exists);
            CheckIsDirectory(link2Target);
            Assert.False(link2Target.Attributes.HasFlag(FileAttributes.ReparsePoint));
            Assert.Equal(link2Target.FullName, filePath);

            // link1: follow symlinks
            var finalTarget = ResolveLinkTarget(link1Path, returnFinalTarget: true);
            Assert.True(finalTarget.Exists);
            CheckIsDirectory(finalTarget);
            Assert.False(finalTarget.Attributes.HasFlag(FileAttributes.ReparsePoint));
            Assert.Equal(finalTarget.FullName, filePath);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void DetectSymbolicLinkCycle()
        {
            // link1 -> link2
            //   ^        /
            //    \______/

            string link2Path = GetRandomFilePath();
            string link1Path = GetRandomFilePath();

            var link1Info = CreateSymbolicLink(link1Path, link2Path);
            var link2Info = CreateSymbolicLink(link2Path, link1Path);

            // Can get targets without following symlinks
            var link1Target = ResolveLinkTarget(link1Path);
            var link2Target = ResolveLinkTarget(link2Path);

            // Cannot get target when following symlinks
            Assert.Throws<IndexOutOfRangeException>(() => ResolveLinkTarget(link1Path, returnFinalTarget: true));
            Assert.Throws<IndexOutOfRangeException>(() => ResolveLinkTarget(link2Path, returnFinalTarget: true));
        }
    }
}