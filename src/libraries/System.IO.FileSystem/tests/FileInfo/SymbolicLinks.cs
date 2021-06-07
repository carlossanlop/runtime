// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace System.IO.Tests
{
    public class FileInfo_SymbolicLinks : BaseSymbolicLinks
    {
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
            var linkInfo = new FileInfo(linkPath);

            linkInfo.CreateAsSymbolicLink(expectedLinkTarget);

            Assert.True(linkInfo.Exists);
            Assert.True(linkInfo.Attributes.HasFlag(FileAttributes.ReparsePoint));

            var targetInfo = linkInfo.ResolveLinkTarget();

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
            var linkInfo = new FileInfo(linkPath);

            linkInfo.CreateAsSymbolicLink(nonExistentTargetPath);
            Assert.True(linkInfo.Exists); // For file symlinks, we return the exists info from the actual link, not the target
            Assert.True(linkInfo.Attributes.HasFlag(FileAttributes.ReparsePoint));

            var target = linkInfo.ResolveLinkTarget();

            Assert.True(target is FileInfo);
            Assert.False(target.Exists);
            Assert.Equal(linkInfo.LinkTarget, target.FullName);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_WrongTargetType()
        {
            // fileLink => directory

            string targetPath = GetRandomFilePath();
            Directory.CreateDirectory(targetPath);

            string linkPath = GetRandomLinkPath();
            var linkInfo = new FileInfo(linkPath);

            Assert.Throws<IOException>(() => linkInfo.CreateAsSymbolicLink(targetPath));
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void GetTargetInfo_NonExistentLink()
        {
            // ? -> ?

            string linkPath = GetRandomLinkPath();
            var linkInfo = new FileInfo(linkPath);
            Assert.Null(linkInfo.ResolveLinkTarget());
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
            //Attach();
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
            var link2Info = new FileInfo(link2Path);
            link2Info.CreateAsSymbolicLink(expectedLink2Target);

            // link to link2
            var link1Info = new FileInfo(link1Path);
            link1Info.CreateAsSymbolicLink(expectedLink1Target);

            Assert.True(link1Info.Exists);
            Assert.True(link1Info.Attributes.HasFlag(FileAttributes.ReparsePoint));

            Assert.Equal(link1Info.LinkTarget, expectedLink1Target);
            Assert.Equal(link2Info.LinkTarget, expectedLink2Target);

            // do not follow symlinks
            var link1Target = link1Info.ResolveLinkTarget();

            Assert.True(link1Target is FileInfo);
            Assert.True(link1Target.Exists);
            Assert.True(link1Target.Attributes.HasFlag(FileAttributes.ReparsePoint));
            Assert.Equal(link1Target.FullName, link2Path);

            // follow symlinks
            var finalTarget = link1Info.ResolveLinkTarget(returnFinalTarget: true);

            Assert.True(finalTarget is FileInfo);
            Assert.True(finalTarget.Exists);
            if (finalTarget.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                Console.WriteLine($"Final target path: {finalTarget.FullName},\nexpected file path: {filePath}\nexpected2: {expectedLink2Target}\nreparse: {finalTarget.Attributes.HasFlag(FileAttributes.ReparsePoint)}");
            }
            Assert.False(finalTarget.Attributes.HasFlag(FileAttributes.ReparsePoint));
            Assert.Equal(finalTarget.FullName, filePath);
        }
        
        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void DetectSymbolicLinkCycle()
        {
            // fileLink1 -> fileLink2
            //   ^        /
            //    \______/

            string link2Path = GetRandomLinkPath();
            string link1Path = GetRandomLinkPath();

            var link1Info = new FileInfo(link1Path);
            link1Info.CreateAsSymbolicLink(link2Path);

            var link2Info = new FileInfo(link2Path);
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
