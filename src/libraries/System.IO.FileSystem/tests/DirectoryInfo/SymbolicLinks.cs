// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Xunit;

namespace System.IO.Tests
{
    public class DirectoryInfo_SymbolicLinks : BaseSymbolicLinks
    {
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

        [Fact]
        public void LinkDoesNotExist_GetLinkTarget()
        {
            var info = new DirectoryInfo(GetRandomFilePath());
            Assert.Null(info.LinkTarget);
            Assert.Null(info.ResolveLinkTarget());
            Assert.Null(info.ResolveLinkTarget(returnFinalTarget: true));
        }

        [Fact]
        public void NotALink_GetLinkTarget()
        {
            string dirPath = GetRandomFilePath();
            Directory.CreateDirectory(dirPath);
            var info = new DirectoryInfo(dirPath);
            Assert.Null(info.LinkTarget);
            Assert.Null(info.ResolveLinkTarget());
            Assert.Null(info.ResolveLinkTarget(returnFinalTarget: true));
        }

        [Fact]
        public void CreateSymbolicLink_RelativeLinkPath()
        {
            var info = new DirectoryInfo(GetRandomFileName());
            Assert.Throws<ArgumentException>(() => info.CreateAsSymbolicLink(pathToTarget: GetRandomFileName()));
        }

        [Fact]
        public void CreateSymbolicLink_NullPathToTarget()
        {
            var info = new DirectoryInfo(GetRandomFilePath());
            Assert.Throws<ArgumentNullException>(() => info.CreateAsSymbolicLink(pathToTarget: null));
        }

        [Theory]
        [InlineData("")]
        [InlineData("\0")]
        public void CreateSymbolicLink_InvalidPathToTarget(string pathToTarget)
        {
            var info = new DirectoryInfo(GetRandomFilePath());
            Assert.Throws<ArgumentException>(() => info.CreateAsSymbolicLink(pathToTarget));
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_Absolute()
        {
            // /path/to/dirLink -> /path/to/targetDir

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
            // /path/to/dirLink -> targetDir

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
            var linkInfo = new DirectoryInfo(linkPath);

            linkInfo.CreateAsSymbolicLink(expectedLinkTarget);

            Assert.True(linkInfo.Exists);
            Assert.True(linkInfo.Attributes.HasFlag(FileAttributes.ReparsePoint));
            Assert.True(linkInfo.Attributes.HasFlag(FileAttributes.Directory));

            var targetInfo = linkInfo.ResolveLinkTarget();

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
            var linkInfo = new DirectoryInfo(linkPath);

            linkInfo.CreateAsSymbolicLink(nonExistentTargetPath);
            Assert.False(linkInfo.Exists); // For directory symlinks, we return the exists info from the target
            Assert.True(linkInfo.Attributes.HasFlag(FileAttributes.ReparsePoint));

            var target = linkInfo.ResolveLinkTarget();

            Assert.NotNull(target);
            Assert.True(target is DirectoryInfo);
            Assert.False(target.Exists);
            Assert.Equal(linkInfo.LinkTarget, target.FullName);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_WrongTargetType()
        {
            // dirLink -> file

            string targetPath = Path.Join(TestDirectory, GetTestFileName());
            File.Create(targetPath).Dispose();

            string linkPath = Path.Join(TestDirectory, GetTestFileName());
            var linkInfo = new DirectoryInfo(linkPath);

            Assert.Throws<IOException>(() => linkInfo.CreateAsSymbolicLink(targetPath));
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void GetTargetInfo_NonExistentLink()
        {
            // ? -> ?

            string linkPath = Path.Join(TestDirectory, GetTestFileName());
            var linkInfo = new DirectoryInfo(linkPath);
            Assert.Null(linkInfo.ResolveLinkTarget());
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
            string link2DirName = Path.GetFileName(link2Path);
            string dirName = Path.GetFileName(dirPath);
            GetTargetInfo_ReturnFinalTarget(
                link1Path: link1Path,
                expectedLink1Target: link2DirName,
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
            string link2DirName = Path.GetFileName(link2Path);
            string dirName = Path.GetFileName(dirPath);
            string leafDir = Path.GetFileName(TestDirectory);
            GetTargetInfo_ReturnFinalTarget(
                link1Path: link1Path,
                expectedLink1Target: Path.Join(TestDirectory, "..", leafDir, link2DirName),
                link2Path: link2Path,
                expectedLink2Target: Path.Join(TestDirectory, ".", dirName),
                dirPath: dirPath);
        }

        private void GetTargetInfo_ReturnFinalTarget(string link1Path, string expectedLink1Target, string link2Path, string expectedLink2Target, string dirPath)
        {
            // link1Path -> expectedLink1Target (created in link2Path) -> expectedLink2Target (created in dirPath)

            Directory.CreateDirectory(dirPath);

            // link to target
            var link2Info = new DirectoryInfo(link2Path);
            link2Info.CreateAsSymbolicLink(expectedLink2Target);

            // link to link2
            var link1Info = new DirectoryInfo(link1Path);
            link1Info.CreateAsSymbolicLink(expectedLink1Target);

            Assert.True(link1Info.Exists);
            Assert.True(link1Info.Attributes.HasFlag(FileAttributes.ReparsePoint));
            Assert.True(link1Info.Attributes.HasFlag(FileAttributes.Directory));

            Assert.Equal(link1Info.LinkTarget, expectedLink1Target);
            Assert.Equal(link2Info.LinkTarget, expectedLink2Target);

            // do not follow symlinks
            var link1Target = link1Info.ResolveLinkTarget();

            Assert.True(link1Target is DirectoryInfo);
            Assert.True(link1Target.Exists);
            Assert.True(link1Target.Attributes.HasFlag(FileAttributes.ReparsePoint));
            Assert.True(link1Target.Attributes.HasFlag(FileAttributes.Directory));
            Assert.Equal(link1Target.FullName, link2Path);

            // follow symlinks
            var finalTarget = link1Info.ResolveLinkTarget(returnFinalTarget: true);

            Assert.True(finalTarget is DirectoryInfo);
            Assert.True(finalTarget.Exists);
            Assert.False(finalTarget.Attributes.HasFlag(FileAttributes.ReparsePoint));
            Assert.True(link1Target.Attributes.HasFlag(FileAttributes.Directory));
            Assert.Equal(finalTarget.FullName, dirPath);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void DetectSymbolicLinkCycle()
        {
            // dirLink1 -> dirLink2
            //   ^        /
            //    \______/

            string link2Path = Path.Join(TestDirectory, GetTestFileName());
            string link1Path = Path.Join(TestDirectory, GetTestFileName());

            var link1Info = new DirectoryInfo(link1Path);
            link1Info.CreateAsSymbolicLink(link2Path);

            var link2Info = new DirectoryInfo(link2Path);
            link2Info.CreateAsSymbolicLink(link1Path);

            // Can get targets without following symlinks
            var link1Target = link1Info.ResolveLinkTarget();
            var link2Target = link2Info.ResolveLinkTarget();

            // Cannot get target when following symlinks
            Assert.Throws<IndexOutOfRangeException>(() => link1Info.ResolveLinkTarget(returnFinalTarget: true));
            Assert.Throws<IndexOutOfRangeException>(() => link2Info.ResolveLinkTarget(returnFinalTarget: true));
        }
    }
}
