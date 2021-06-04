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

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateAbsoluteSymbolicLink()
        {
            string targetPath = Path.Join(TestDirectory, GetTestFileName());
            Directory.CreateDirectory(targetPath);

            string linkPath = Path.Join(TestDirectory, GetTestFileName());
            var linkInfo = new DirectoryInfo(linkPath);

            linkInfo.CreateAsSymbolicLink(targetPath);

            Assert.True(linkInfo.Exists);
            Assert.True(linkInfo.Attributes.HasFlag(FileAttributes.ReparsePoint));

            var target = linkInfo.ResolveLinkTarget();

            Assert.True(target is DirectoryInfo);
            Assert.True(target.Exists);
            Assert.Equal(linkInfo.LinkTarget, target.FullName);
        }


        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateRelativeSymbolicLink()
        {
            string targetPath = Path.Join(TestDirectory, GetTestFileName());
            Directory.CreateDirectory(targetPath);

            string linkPath = Path.Join(TestDirectory, GetTestFileName());
            var linkInfo = new DirectoryInfo(linkPath);

            linkInfo.CreateAsSymbolicLink(targetPath);

            Assert.True(linkInfo.Exists);
            Assert.True(linkInfo.Attributes.HasFlag(FileAttributes.ReparsePoint));

            var target = linkInfo.ResolveLinkTarget();

            Assert.True(target is DirectoryInfo);
            Assert.True(target.Exists);
            Assert.Equal(linkInfo.LinkTarget, target.FullName);
        }


        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_NonExistentTarget()
        {
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
            string targetPath = Path.Join(TestDirectory, GetTestFileName());
            File.Create(targetPath).Dispose();

            string linkPath = Path.Join(TestDirectory, GetTestFileName());
            var linkInfo = new DirectoryInfo(linkPath);

            Assert.Throws<IOException>(() => linkInfo.CreateAsSymbolicLink(targetPath));
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void GetTargetInfo_NonExistentLink()
        {
            string linkPath = Path.Join(TestDirectory, GetTestFileName());
            var linkInfo = new DirectoryInfo(linkPath);
            Assert.Null(linkInfo.ResolveLinkTarget());
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void GetTargetInfo_ReturnFinalTarget()
        {
            // link1 -> link2 -> target

            string targetPath = Path.Join(TestDirectory, GetTestFileName());
            Directory.CreateDirectory(targetPath);

            string link2Path = Path.Join(TestDirectory, GetTestFileName());
            string link1Path = Path.Join(TestDirectory, GetTestFileName());

            // link to target
            var link2Info = new DirectoryInfo(link2Path);
            link2Info.CreateAsSymbolicLink(targetPath);

            // link to link2
            var link1Info = new DirectoryInfo(link1Path);
            link1Info.CreateAsSymbolicLink(link2Path);

            Assert.True(link1Info.Exists);
            Assert.True(link1Info.Attributes.HasFlag(FileAttributes.ReparsePoint));

            // do not follow symlinks
            var directTarget = link1Info.ResolveLinkTarget();

            Assert.True(directTarget is DirectoryInfo);
            Assert.True(directTarget.Exists);
            Assert.Equal(link1Info.LinkTarget, directTarget.FullName);
            Assert.True(directTarget.Attributes.HasFlag(FileAttributes.ReparsePoint));

            // follow symlinks
            var finalTarget = link1Info.ResolveLinkTarget(returnFinalTarget: true);

            Assert.True(finalTarget is DirectoryInfo);
            Assert.True(finalTarget.Exists);
            Assert.Equal(link2Info.LinkTarget, finalTarget.FullName);
            Assert.False(finalTarget.Attributes.HasFlag(FileAttributes.ReparsePoint));
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void DetectSymbolicLinkCycle()
        {
            // link1 -> link2
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
