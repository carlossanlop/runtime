// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Xunit;

namespace System.IO.Tests
{
    public class Directory_SymbolicLinks : BaseSymbolicLinks
    {
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

        [Fact]
        public void LinkDoesNotExist_GetLinkTarget()
        {
            string dirPath = GetRandomFilePath();
            Assert.Null(Directory.ResolveLinkTarget(dirPath));
            Assert.Null(Directory.ResolveLinkTarget(dirPath, returnFinalTarget: true));
        }

        [Fact]
        public void NotALink_GetLinkTarget()
        {
            string dirPath = GetRandomFilePath();
            Directory.CreateDirectory(dirPath);
            Assert.Null(Directory.ResolveLinkTarget(dirPath));
            Assert.Null(Directory.ResolveLinkTarget(dirPath, returnFinalTarget: true));
        }

        [Fact]
        public void CreateSymbolicLink_NullLinkPath()
        {
            Assert.Throws<ArgumentNullException>(() => Directory.CreateSymbolicLink(path: null, pathToTarget: GetRandomFileName()));
        }

        [Theory]
        [InlineData("")]
        [InlineData("\0")]
        public void CreateSymbolicLink_InvalidLinkPath(string linkPath)
        {
            Assert.Throws<ArgumentException>(() => Directory.CreateSymbolicLink(linkPath, pathToTarget: GetRandomFileName()));
        }

        [Fact]
        public void CreateSymbolicLink_RelativeLinkPath()
        {
            Assert.Throws<ArgumentException>(() => Directory.CreateSymbolicLink(path: GetRandomFileName(), pathToTarget: GetRandomFileName()));
        }

        [Fact]
        public void CreateSymbolicLink_NullPathToTarget()
        {
            Assert.Throws<ArgumentNullException>(() => Directory.CreateSymbolicLink(path: GetRandomFilePath(), pathToTarget: null));
        }

        [Theory]
        [InlineData("")]
        [InlineData("\0")]
        public void CreateSymbolicLink_InvalidPathToTarget(string pathToTarget)
        {
            Assert.Throws<ArgumentException>(() => Directory.CreateSymbolicLink(GetRandomFilePath(), pathToTarget));
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_Absolute()
        {
            // /path/to/fileLink -> /path/to/targetFile

            string linkPath = GetRandomLinkPath();
            string dirPath = GetRandomFilePath();
            CreateSymbolicLink(
                linkPath: linkPath,
                expectedLinkTarget: dirPath,
                dirPath: dirPath);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_Relative()
        {
            // /path/to/fileLink -> targetFile

            string linkPath = GetRandomLinkPath();
            string dirPath = GetRandomFilePath();
            string dirName = Path.GetFileName(dirPath);
            CreateSymbolicLink(
                linkPath: linkPath,
                expectedLinkTarget: dirName,
                dirPath: dirPath);
        }

        private void CreateSymbolicLink(string linkPath, string expectedLinkTarget, string dirPath)
        {
            // linkPath -> expectedLinkTarget (created in dirPath)

            Directory.CreateDirectory(dirPath);

            var linkInfo = Directory.CreateSymbolicLink(linkPath, expectedLinkTarget);

            Assert.True(linkInfo is DirectoryInfo);
            Assert.True(linkInfo.Exists);
            Assert.True(linkInfo.Attributes.HasFlag(FileAttributes.ReparsePoint));
            Assert.True(linkInfo.Attributes.HasFlag(FileAttributes.Directory));

            var targetInfo = Directory.ResolveLinkTarget(linkPath);

            Assert.True(targetInfo is DirectoryInfo);
            Assert.True(targetInfo.Exists);
            Assert.Equal(linkInfo.LinkTarget, expectedLinkTarget);
            Assert.True(Path.IsPathFullyQualified(targetInfo.FullName));
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_NonExistentTarget()
        {
            // dirLink -> ?

            string nonExistentTargetPath = Path.Join(TestDirectory, GetTestFileName());

            string linkPath = Path.Join(TestDirectory, GetTestFileName());
            var linkInfo = Directory.CreateSymbolicLink(linkPath, nonExistentTargetPath);

            Assert.True(linkInfo is DirectoryInfo);
            Assert.False(linkInfo.Exists); // For directory symlinks, we return the exists info from the target
            Assert.True(linkInfo.Attributes.HasFlag(FileAttributes.ReparsePoint));

            var target = Directory.ResolveLinkTarget(linkPath);

            Assert.True(target is DirectoryInfo);
            Assert.False(target.Exists);
            Assert.Equal(nonExistentTargetPath, target.FullName);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_WrongTargetType()
        {
            // dirLink => file

            string targetPath = Path.Join(TestDirectory, GetTestFileName());
            File.Create(targetPath).Dispose();

            string linkPath = Path.Join(TestDirectory, GetTestFileName());
            Assert.Throws<IOException>(() => Directory.CreateSymbolicLink(linkPath, targetPath));
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void GetTargetInfo_NonExistentLink()
        {
            // ? -> ?

            string linkPath = Path.Join(TestDirectory, GetTestFileName());
            Assert.Null(Directory.ResolveLinkTarget(linkPath));
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void GetTargetInfo_ReturnFinalTarget_Absolute()
        {
            string link1Path = GetRandomLinkPath();
            string link2Path = GetRandomLinkPath();
            string dirPath = GetRandomFilePath();
            GetTargetInfo_ReturnFinalTarget(
                link1Path: link1Path,
                expectedLink1Target: link2Path,
                link2Path: link2Path,
                expectedLink2Target: dirPath,
                dirPath: dirPath);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void GetTargetInfo_ReturnFinalTarget_Relative()
        {
            string link1Path = GetRandomLinkPath();
            string link2Path = GetRandomLinkPath();
            string dirPath = GetRandomFilePath();
            string link2FileName = Path.GetFileName(link2Path);
            string dirName = Path.GetFileName(dirPath);
            GetTargetInfo_ReturnFinalTarget(
                link1Path: link1Path,
                expectedLink1Target: link2FileName,
                link2Path: link2Path,
                expectedLink2Target: dirName,
                dirPath: dirPath);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void GetTargetInfo_ReturnFinalTarget_Relative_WithRedundantSegments()
        {
            string link1Path = GetRandomLinkPath();
            string link2Path = GetRandomLinkPath();
            string dirPath = GetRandomFilePath();
            string link2FileName = Path.GetFileName(link2Path);
            string dirName = Path.GetFileName(dirPath);
            string leafDir = Path.GetFileName(TestDirectory);
            GetTargetInfo_ReturnFinalTarget(
                link1Path: link1Path,
                expectedLink1Target: Path.Join(TestDirectory, "..", leafDir, link2FileName),
                link2Path: link2Path,
                expectedLink2Target: Path.Join(TestDirectory, ".", dirName),
                dirPath: dirPath);
        }

        private void GetTargetInfo_ReturnFinalTarget(string link1Path, string expectedLink1Target, string link2Path, string expectedLink2Target, string dirPath)
        {
            // link1Path -> expectedLink1Target (created in link2Path) -> expectedLink2Target (created in dirPath)

            Directory.CreateDirectory(dirPath);

            // link to target
            var link2Info = Directory.CreateSymbolicLink(link2Path, expectedLink2Target);

            // link to link2
            var link1Info = Directory.CreateSymbolicLink(link1Path, expectedLink1Target);

            Assert.True(link1Info.Exists);
            Assert.True(link1Info.Attributes.HasFlag(FileAttributes.ReparsePoint));
            Assert.True(link1Info.Attributes.HasFlag(FileAttributes.Directory));

            Assert.Equal(link1Info.LinkTarget, expectedLink1Target);
            Assert.Equal(link2Info.LinkTarget, expectedLink2Target);

            // do not follow symlinks
            var link1Target = Directory.ResolveLinkTarget(link1Path);

            Assert.True(link1Target is DirectoryInfo);
            Assert.True(link1Target.Exists);
            Assert.True(link1Target.Attributes.HasFlag(FileAttributes.ReparsePoint));
            Assert.True(link1Target.Attributes.HasFlag(FileAttributes.Directory));
            Assert.Equal(link1Target.FullName, link2Path);

            // follow symlinks
            var finalTarget = Directory.ResolveLinkTarget(link1Path, returnFinalTarget: true);

            Assert.True(finalTarget is DirectoryInfo);
            Assert.True(finalTarget.Exists);
            Assert.False(finalTarget.Attributes.HasFlag(FileAttributes.ReparsePoint));
            Assert.True(finalTarget.Attributes.HasFlag(FileAttributes.Directory));
            Assert.Equal(finalTarget.FullName, dirPath);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void DetectSymbolicLinkCycle()
        {
            // link1 -> link2
            //   ^        /
            //    \______/

            string link2Path = Path.Join(TestDirectory, GetTestFileName());
            string link1Path = Path.Join(TestDirectory, GetTestFileName());

            Directory.CreateSymbolicLink(path: link1Path, pathToTarget: link2Path);
            Directory.CreateSymbolicLink(path: link2Path, pathToTarget: link1Path);

            // Can get targets without following symlinks
            var link1Target = Directory.ResolveLinkTarget(linkPath: link1Path);
            var link2Target = Directory.ResolveLinkTarget(linkPath: link2Path);

            // Cannot get target when following symlinks
            Assert.Throws<IndexOutOfRangeException>(() => Directory.ResolveLinkTarget(linkPath: link1Path, returnFinalTarget: true));
            Assert.Throws<IndexOutOfRangeException>(() => Directory.ResolveLinkTarget(linkPath: link2Path, returnFinalTarget: true));
        }
    }
}
