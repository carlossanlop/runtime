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
            string targetPath = Path.Combine(TestDirectory, GetTestFileName());
            Directory.CreateDirectory(targetPath);

            string linkPath = Path.Combine(TestDirectory, GetTestFileName());
            DirectoryInfo linkInfo = new DirectoryInfo(linkPath);

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
            string targetPath = Path.Combine(TestDirectory, GetTestFileName());
            Directory.CreateDirectory(targetPath);

            string linkPath = Path.Combine(TestDirectory, GetTestFileName());
            DirectoryInfo linkInfo = new DirectoryInfo(linkPath);

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
            string nonExistentTargetPath = Path.Combine(TestDirectory, GetTestFileName());

            string linkPath = Path.Combine(TestDirectory, GetTestFileName());
            DirectoryInfo linkInfo = new DirectoryInfo(linkPath);

            linkInfo.CreateAsSymbolicLink(nonExistentTargetPath);

            Assert.True(linkInfo.Exists);
            Assert.True(linkInfo.Attributes.HasFlag(FileAttributes.ReparsePoint));

            var target = linkInfo.ResolveLinkTarget();

            Assert.True(target is DirectoryInfo);
            Assert.False(target.Exists);
            Assert.Equal(linkInfo.LinkTarget, target.FullName);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_WrongTargetType()
        {
            string targetPath = Path.Combine(TestDirectory, GetTestFileName());
            File.Create(targetPath).Dispose();

            string linkPath = Path.Combine(TestDirectory, GetTestFileName());
            DirectoryInfo linkInfo = new DirectoryInfo(linkPath);

            Assert.Throws<Exception>(() => linkInfo.CreateAsSymbolicLink(targetPath));
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void GetTargetInfo_NonExistentLink()
        {
            string linkPath = Path.Combine(TestDirectory, GetTestFileName());
            DirectoryInfo linkInfo = new DirectoryInfo(linkPath);
            Assert.Null(linkInfo.ResolveLinkTarget());
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void GetTargetInfo_ReturnFinalTarget()
        {
            // link1 -> link2 -> target

            string targetPath = Path.Combine(TestDirectory, GetTestFileName());
            Directory.CreateDirectory(targetPath);

            string link2Path = Path.Combine(TestDirectory, GetTestFileName());
            string link1Path = Path.Combine(TestDirectory, GetTestFileName());

            // link to target
            DirectoryInfo link2Info = new DirectoryInfo(link2Path);
            link2Info.CreateAsSymbolicLink(targetPath);

            // link to link2
            DirectoryInfo link1Info = new DirectoryInfo(link1Path);
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

            string link2Path = Path.Combine(TestDirectory, GetTestFileName());
            string link1Path = Path.Combine(TestDirectory, GetTestFileName());

            DirectoryInfo link1Info = new DirectoryInfo(link1Path);
            link1Info.CreateAsSymbolicLink(link1Path);

            Assert.True(link1Info is DirectoryInfo);
            Assert.True(link1Info.Exists);
            Assert.True(link1Info.Attributes.HasFlag(FileAttributes.ReparsePoint));

            DirectoryInfo link2Info = new DirectoryInfo(link2Path);
            link2Info.CreateAsSymbolicLink(link1Path);

            Assert.True(link2Info is DirectoryInfo);
            Assert.True(link2Info.Exists);
            Assert.True(link2Info.Attributes.HasFlag(FileAttributes.ReparsePoint));

            // Can get target without following symlinks
            var link1Target = link1Info.ResolveLinkTarget();
            Assert.True(link1Target is DirectoryInfo);
            Assert.True(link1Target.Exists);
            Assert.True(link1Target.Attributes.HasFlag(FileAttributes.ReparsePoint));

            var link2Target = link2Info.ResolveLinkTarget();
            Assert.True(link2Target is DirectoryInfo);
            Assert.True(link2Target.Exists);
            Assert.True(link2Target.Attributes.HasFlag(FileAttributes.ReparsePoint));

            // Cannot get target when following symlinks
            Assert.Throws<Exception>(() => link1Info.ResolveLinkTarget(returnFinalTarget: true));
            Assert.Throws<Exception>(() => link2Info.ResolveLinkTarget(returnFinalTarget: true));
        }
    }
}
