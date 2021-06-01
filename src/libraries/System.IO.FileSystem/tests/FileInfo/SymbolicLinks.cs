// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace System.IO.Tests
{
    public class FileInfo_SymbolicLinks : BaseSymbolicLinks
    {
        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink()
        {
            string targetPath = Path.Combine(TestDirectory, GetTestFileName());
            File.Create(targetPath).Dispose();

            string linkPath = Path.Combine(TestDirectory, GetTestFileName());
            FileInfo linkInfo = new FileInfo(linkPath);

            linkInfo.CreateAsSymbolicLink(targetPath);

            Assert.True(linkInfo.Exists);
            Assert.True(linkInfo.Attributes.HasFlag(FileAttributes.ReparsePoint));

            var target = linkInfo.ResolveLinkTarget();

            Assert.True(target is FileInfo);
            Assert.True(target.Exists);
            Assert.Equal(linkInfo.LinkTarget, target.FullName);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_NonExistentTarget()
        {
            string nonExistentTargetPath = Path.Combine(TestDirectory, GetTestFileName());

            string linkPath = Path.Combine(TestDirectory, GetTestFileName());
            FileInfo linkInfo = new FileInfo(linkPath);

            linkInfo.CreateAsSymbolicLink(nonExistentTargetPath);

            Assert.True(linkInfo.Exists);
            Assert.True(linkInfo.Attributes.HasFlag(FileAttributes.ReparsePoint));

            var target = linkInfo.ResolveLinkTarget();

            Assert.True(target is FileInfo);
            Assert.False(target.Exists);
            Assert.Equal(linkInfo.LinkTarget, target.FullName);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_WrongTargetType()
        {
            string targetPath = Path.Combine(TestDirectory, GetTestFileName());
            Directory.CreateDirectory(targetPath);

            string linkPath = Path.Combine(TestDirectory, GetTestFileName());
            FileInfo linkInfo = new FileInfo(linkPath);

            Assert.Throws<Exception>(() => linkInfo.CreateAsSymbolicLink(targetPath));
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void GetTargetInfo_NonExistentLink()
        {
            string linkPath = Path.Combine(TestDirectory, GetTestFileName());
            FileInfo linkInfo = new FileInfo(linkPath);
            Assert.Null(linkInfo.ResolveLinkTarget());
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void GetTargetInfo_ReturnFinalTarget()
        {
            // link1 -> link2 -> target

            string targetPath = Path.Combine(TestDirectory, GetTestFileName());
            File.CreateText(targetPath);

            string link2Path = Path.Combine(TestDirectory, GetTestFileName());
            string link1Path = Path.Combine(TestDirectory, GetTestFileName());

            // link to target
            FileInfo link2Info = new FileInfo(link2Path);
            link2Info.CreateAsSymbolicLink(targetPath);

            // link to link2
            FileInfo link1Info = new FileInfo(link1Path);
            link1Info.CreateAsSymbolicLink(link2Path);

            Assert.True(link1Info.Exists);
            Assert.True(link1Info.Attributes.HasFlag(FileAttributes.ReparsePoint));

            // do not follow symlinks
            var directTarget = link1Info.ResolveLinkTarget();

            Assert.True(directTarget is FileInfo);
            Assert.True(directTarget.Exists);
            Assert.Equal(link1Info.LinkTarget, directTarget.FullName);
            Assert.True(directTarget.Attributes.HasFlag(FileAttributes.ReparsePoint));

            // follow symlinks
            var finalTarget = link1Info.ResolveLinkTarget(returnFinalTarget: true);

            Assert.True(finalTarget is FileInfo);
            Assert.True(finalTarget.Exists);
            Assert.Equal(link2Info.LinkTarget, finalTarget.FullName);
            Assert.False(finalTarget.Attributes.HasFlag(FileAttributes.ReparsePoint));
        }
    }
}
