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

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink()
        {
            string targetPath = Path.Join(TestDirectory, GetTestFileName());
            Directory.CreateDirectory(targetPath);

            string linkPath = Path.Join(TestDirectory, GetTestFileName());
            var linkInfo = Directory.CreateSymbolicLink(linkPath, targetPath);

            Assert.True(linkInfo is DirectoryInfo);
            Assert.True(linkInfo.Exists);
            Assert.True(linkInfo.Attributes.HasFlag(FileAttributes.ReparsePoint));

            var target = Directory.ResolveLinkTarget(linkPath);

            Assert.True(target is DirectoryInfo);
            Assert.True(target.Exists);
            Assert.Equal(targetPath, target.FullName);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_NonExistentTarget()
        {
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
            string targetPath = Path.Join(TestDirectory, GetTestFileName());
            File.Create(targetPath).Dispose();

            string linkPath = Path.Join(TestDirectory, GetTestFileName());
            Assert.Throws<IOException>(() => Directory.CreateSymbolicLink(linkPath, targetPath));
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void GetTargetInfo_NonExistentLink()
        {
            string linkPath = Path.Join(TestDirectory, GetTestFileName());
            Assert.Null(Directory.ResolveLinkTarget(linkPath));
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
