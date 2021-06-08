// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Xunit;

namespace System.IO.Tests
{
    public class File_SymbolicLinks : BaseSymbolicLinks
    {
        [Fact]
        public void LinkDoesNotExist_GetLinkTarget()
        {
            string filePath = GetRandomFilePath();
            Assert.Null(File.ResolveLinkTarget(filePath));
            Assert.Null(File.ResolveLinkTarget(filePath, returnFinalTarget: true));
        }

        [Fact]
        public void NotALink_GetLinkTarget()
        {
            string filePath = GetRandomFilePath();
            File.Create(filePath).Dispose();
            Assert.Null(File.ResolveLinkTarget(filePath));
            Assert.Null(File.ResolveLinkTarget(filePath, returnFinalTarget: true));
        }

        [Fact]
        public void CreateSymbolicLink_NullLinkPath()
        {
            Assert.Throws<ArgumentNullException>(() => File.CreateSymbolicLink(path: null, pathToTarget: GetRandomFileName()));
        }

        [Theory]
        [InlineData("")]
        [InlineData("\0")]
        public void CreateSymbolicLink_InvalidLinkPath(string linkPath)
        {
            Assert.Throws<ArgumentException>(() => File.CreateSymbolicLink(linkPath, pathToTarget: GetRandomFileName()));
        }

        [Fact]
        public void CreateSymbolicLink_RelativeLinkPath()
        {
            Assert.Throws<ArgumentException>(() => File.CreateSymbolicLink(path: GetRandomFileName(), pathToTarget: GetRandomFileName()));
        }

        [Fact]
        public void CreateSymbolicLink_NullPathToTarget()
        {
            Assert.Throws<ArgumentNullException>(() => File.CreateSymbolicLink(path: GetRandomFilePath(), pathToTarget: null));
        }

        [Theory]
        [InlineData("")]
        [InlineData("\0")]
        public void CreateSymbolicLink_InvalidPathToTarget(string pathToTarget)
        {
            Assert.Throws<ArgumentException>(() => File.CreateSymbolicLink(GetRandomFilePath(), pathToTarget));
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_Absolute()
        {
            // /path/to/fileLink -> /path/to/targetFile

            string linkPath = GetRandomLinkPath();
            string filePath = GetRandomFilePath();
            CreateSymbolicLink(
                linkPath: linkPath,
                expectedLinkTarget: filePath,
                filePath: filePath);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_Relative()
        {
            // /path/to/fileLink -> targetFile

            string linkPath = GetRandomLinkPath();
            string filePath = GetRandomFilePath();
            string fileName = Path.GetFileName(filePath);
            CreateSymbolicLink(
                linkPath: linkPath,
                expectedLinkTarget: fileName,
                filePath: filePath);
        }

        private void CreateSymbolicLink(string linkPath, string expectedLinkTarget, string filePath)
        {
            // linkPath -> expectedLinkTarget (created in filePath)

            File.Create(filePath).Dispose();

            var linkInfo = File.CreateSymbolicLink(linkPath, expectedLinkTarget);

            Assert.True(linkInfo is FileInfo);
            Assert.True(linkInfo.Exists);
            Assert.True(linkInfo.Attributes.HasFlag(FileAttributes.ReparsePoint));
            Assert.False(linkInfo.Attributes.HasFlag(FileAttributes.Directory));

            var targetInfo = File.ResolveLinkTarget(linkPath);

            Assert.True(targetInfo is FileInfo);
            Assert.True(targetInfo.Exists);
            Assert.Equal(linkInfo.LinkTarget, expectedLinkTarget);
            Assert.True(Path.IsPathFullyQualified(targetInfo.FullName));
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_NonExistentTarget()
        {
            // fileLink -> ?

            string nonExistentTargetPath = GetRandomFilePath();

            string linkPath = GetRandomLinkPath();
            var linkInfo = File.CreateSymbolicLink(linkPath, nonExistentTargetPath);

            Assert.True(linkInfo is FileInfo);
            Assert.True(linkInfo.Exists); // For file symlinks, we return the exists info from the actual link, not the target
            Assert.True(linkInfo.Attributes.HasFlag(FileAttributes.ReparsePoint));

            var target = File.ResolveLinkTarget(linkPath);

            Assert.True(target is FileInfo);
            Assert.False(target.Exists);
            Assert.Equal(nonExistentTargetPath, target.FullName);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_WrongTargetType()
        {
            // fileLink => directory

            string targetPath = GetRandomFilePath();
            Directory.CreateDirectory(targetPath);

            string linkPath = GetRandomLinkPath();
            Assert.Throws<IOException>(() => File.CreateSymbolicLink(linkPath, targetPath));
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void GetTargetInfo_NonExistentLink()
        {
            // ? -> ?

            string linkPath = GetRandomFilePath();
            Assert.Null(File.ResolveLinkTarget(linkPath));
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void GetTargetInfo_ReturnFinalTarget_Absolute()
        {
            string link1Path = GetRandomLinkPath();
            string link2Path = GetRandomLinkPath();
            string filePath = GetRandomFilePath();
            GetTargetInfo_ReturnFinalTarget(
                link1Path: link1Path,
                expectedLink1Target: link2Path,
                link2Path: link2Path,
                expectedLink2Target: filePath,
                filePath: filePath);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void GetTargetInfo_ReturnFinalTarget_Relative()
        {
            string link1Path = GetRandomLinkPath();
            string link2Path = GetRandomLinkPath();
            string filePath = GetRandomFilePath();
            string link2FileName = Path.GetFileName(link2Path);
            string fileName = Path.GetFileName(filePath);
            GetTargetInfo_ReturnFinalTarget(
                link1Path: link1Path,
                expectedLink1Target: link2FileName,
                link2Path: link2Path,
                expectedLink2Target: fileName,
                filePath: filePath);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void GetTargetInfo_ReturnFinalTarget_Relative_WithRedundantSegments()
        {
            string link1Path = GetRandomLinkPath();
            string link2Path = GetRandomLinkPath();
            string filePath = GetRandomFilePath();
            string link2FileName = Path.GetFileName(link2Path);
            string fileName = Path.GetFileName(filePath);
            string leafDir = Path.GetFileName(TestDirectory);
            GetTargetInfo_ReturnFinalTarget(
                link1Path: link1Path,
                expectedLink1Target: Path.Join(TestDirectory, "..", leafDir, link2FileName),
                link2Path: link2Path,
                expectedLink2Target: Path.Join(TestDirectory, ".", fileName),
                filePath: filePath);
        }

        private void GetTargetInfo_ReturnFinalTarget(string link1Path, string expectedLink1Target, string link2Path, string expectedLink2Target, string filePath)
        {
            // link1Path -> expectedLink1Target (created in link2Path) -> expectedLink2Target (created in filePath)

            File.Create(filePath).Dispose();

            // link to target
            var link2Info = File.CreateSymbolicLink(link2Path, expectedLink2Target);

            // link to link2
            var link1Info = File.CreateSymbolicLink(link1Path, expectedLink1Target);

            Assert.True(link1Info.Exists);
            Assert.True(link1Info.Attributes.HasFlag(FileAttributes.ReparsePoint));
            Assert.False(link1Info.Attributes.HasFlag(FileAttributes.Directory));

            Assert.Equal(link1Info.LinkTarget, expectedLink1Target);
            Assert.Equal(link2Info.LinkTarget, expectedLink2Target);

            // do not follow symlinks
            var link1Target = File.ResolveLinkTarget(link1Path);

            Assert.True(link1Target is FileInfo);
            Assert.True(link1Target.Exists);
            Assert.True(link1Target.Attributes.HasFlag(FileAttributes.ReparsePoint));
            Assert.False(link1Target.Attributes.HasFlag(FileAttributes.Directory));
            Assert.Equal(link1Target.FullName, link2Path);

            // follow symlinks
            var finalTarget = File.ResolveLinkTarget(link1Path, returnFinalTarget: true);

            Assert.True(finalTarget is FileInfo);
            Assert.True(finalTarget.Exists);
            Assert.False(finalTarget.Attributes.HasFlag(FileAttributes.ReparsePoint));
            Assert.False(link1Target.Attributes.HasFlag(FileAttributes.Directory));
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

            File.CreateSymbolicLink(path: link1Path, pathToTarget: link2Path);
            File.CreateSymbolicLink(path: link2Path, pathToTarget: link1Path);

            // Can get targets without following symlinks
            var link1Target = File.ResolveLinkTarget(linkPath: link1Path);
            var link2Target = File.ResolveLinkTarget(linkPath: link2Path);

            // Cannot get target when following symlinks
            Assert.Throws<IndexOutOfRangeException>(() => File.ResolveLinkTarget(linkPath: link1Path, returnFinalTarget: true));
            Assert.Throws<IndexOutOfRangeException>(() => File.ResolveLinkTarget(linkPath: link2Path, returnFinalTarget: true));
        }
    }
}
