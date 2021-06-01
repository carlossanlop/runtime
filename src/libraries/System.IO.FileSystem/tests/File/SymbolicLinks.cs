// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace System.IO.Tests
{
    public class File_SymbolicLinks : BaseSymbolicLinks
    {
        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink()
        {
            string targetPath = Path.Combine(TestDirectory, GetTestFileName());
            File.Create(targetPath).Dispose();

            string linkPath = Path.Combine(TestDirectory, GetTestFileName());
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
            string nonExistentTargetPath = Path.Combine(TestDirectory, GetTestFileName());

            string linkPath = Path.Combine(TestDirectory, GetTestFileName());
            var linkInfo = File.CreateSymbolicLink(linkPath, nonExistentTargetPath);

            Assert.True(linkInfo is FileInfo);
            Assert.True(linkInfo.Exists);
            Assert.True(linkInfo.Attributes.HasFlag(FileAttributes.ReparsePoint));

            var target = File.ResolveLinkTarget(linkPath);

            Assert.True(target is FileInfo);
            Assert.False(target.Exists);
            Assert.Equal(nonExistentTargetPath, target.FullName);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_WrongTargetType()
        {
            string targetPath = Path.Combine(TestDirectory, GetTestFileName());
            Directory.CreateDirectory(targetPath);

            string linkPath = Path.Combine(TestDirectory, GetTestFileName());
            Assert.Throws<Exception>(() => File.CreateSymbolicLink(linkPath, targetPath));
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void GetTargetInfo_NonExistentLink()
        {
            string linkPath = Path.Combine(TestDirectory, GetTestFileName());
            Assert.Null(File.ResolveLinkTarget(linkPath));
        }
    }
}
