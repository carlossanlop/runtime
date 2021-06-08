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
        public void CreateSymbolicLink()
        {
            string targetPath = GetRandomFilePath();
            File.Create(targetPath).Dispose();

            string linkPath = GetRandomLinkPath();
            var linkInfo = File.CreateSymbolicLink(linkPath, targetPath);

            Assert.True(linkInfo is FileInfo);
            Assert.True(linkInfo.Exists);
            Assert.True(linkInfo.Attributes.HasFlag(FileAttributes.ReparsePoint));

            var target = File.ResolveLinkTarget(linkPath);

            Assert.True(target is FileInfo);
            Assert.True(target.Exists);
            Assert.Equal(targetPath, target.FullName);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_NonExistentTarget()
        {
            string nonExistentTargetPath = Path.Join(TestDirectory, GetTestFileName());

            string linkPath = Path.Join(TestDirectory, GetTestFileName());
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
            string targetPath = Path.Join(TestDirectory, GetTestFileName());
            Directory.CreateDirectory(targetPath);

            string linkPath = Path.Join(TestDirectory, GetTestFileName());
            Assert.Throws<IOException>(() => File.CreateSymbolicLink(linkPath, targetPath));
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void GetTargetInfo_NonExistentLink()
        {
            string linkPath = Path.Join(TestDirectory, GetTestFileName());
            Assert.Null(File.ResolveLinkTarget(linkPath));
        }
        
        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void DetectSymbolicLinkCycle()
        {
            // link1 -> link2
            //   ^        /
            //    \______/

            string link2Path = Path.Join(TestDirectory, GetTestFileName());
            string link1Path = Path.Join(TestDirectory, GetTestFileName());

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
