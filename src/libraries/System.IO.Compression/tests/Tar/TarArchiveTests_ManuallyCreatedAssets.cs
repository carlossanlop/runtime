// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace System.IO.Compression.Tests
{
    // Tests that manually create expected files.
    // These test methods are a workaround for the nupkg bug preventing the
    // correct extraction of symlink and hardlink files into disk.
    public partial class TarArchiveTests : FileCleanupTestBase
    {
        // NuGet issues:
        // - 'dotnet restore' unexpectedly extracts nupkg symlinks and hardlinks as normal files/folders.
        // - The MSBuild PackTask, when executed on Windows (only platform it currently supports),
        //   is unable to pack relative symlinks that were generated on Unix.
        #region Tests with active issue

        [ActiveIssue("https://github.com/NuGet/Home/issues/10734")]
        [Theory]
        [MemberData(nameof(Links_V7_Data))]
        public void Read_Links_AllFormats(string testCaseName, CompressionMethod compressionMethod)
        {
            foreach (TestTarFormat format in Enum.GetValues<TestTarFormat>())
            {
                VerifyTarFileContents(compressionMethod, format, testCaseName);
            }
        }

        [ActiveIssue("https://github.com/NuGet/Home/issues/10734")]
        [Theory]
        [MemberData(nameof(Links_PaxAndGnu_Data))]
        public void Read_LongLinks_PaxAndGnu(string testCaseName, CompressionMethod compressionMethod)
        {
            foreach (TestTarFormat format in new[] { TestTarFormat.pax, TestTarFormat.pax_gea, TestTarFormat.gnu, TestTarFormat.oldgnu })
            {
                VerifyTarFileContents(compressionMethod, format, testCaseName);
            }
        }

        #endregion

        #region Format specific validation

        [Theory]
        [MemberData(nameof(Links_V7_Data))]
        public void Read_V7_Links_ManuallyCreated(string testCaseName, CompressionMethod compressionMethod) =>
            GenerateExpectedFilesAndCompare(compressionMethod, TestTarFormat.v7, testCaseName);

        [Theory]
        [MemberData(nameof(Links_V7_Data))]
        public void Read_Ustar_Links_ManuallyCreated(string testCaseName, CompressionMethod compressionMethod) =>
            GenerateExpectedFilesAndCompare(compressionMethod, TestTarFormat.ustar, testCaseName);

        [Theory]
        [MemberData(nameof(Links_V7_Data))]
        public void Read_Pax_Links_ManuallyCreated(string testCaseName, CompressionMethod compressionMethod) =>
            GenerateExpectedFilesAndCompare(compressionMethod, TestTarFormat.pax, testCaseName);

        [Theory]
        [MemberData(nameof(Links_V7_Data))]
        public void Read_PaxGEA_Links_ManuallyCreated(string testCaseName, CompressionMethod compressionMethod) =>
            GenerateExpectedFilesAndCompare(compressionMethod, TestTarFormat.pax_gea, testCaseName);

        [Theory]
        [MemberData(nameof(Links_V7_Data))]
        public void Read_Gnu_Links_ManuallyCreated(string testCaseName, CompressionMethod compressionMethod) =>
            GenerateExpectedFilesAndCompare(compressionMethod, TestTarFormat.gnu, testCaseName);

        [Theory]
        [MemberData(nameof(Links_V7_Data))]
        public void Read_OldGnu_Links_ManuallyCreated(string testCaseName, CompressionMethod compressionMethod) =>
            GenerateExpectedFilesAndCompare(compressionMethod, TestTarFormat.oldgnu, testCaseName);

        [Theory]
        [MemberData(nameof(Links_PaxAndGnu_Data))]
        public void Read_Pax_LongLinks_ManuallyCreated(string testCaseName, CompressionMethod compressionMethod) =>
            GenerateExpectedFilesAndCompare(compressionMethod, TestTarFormat.pax, testCaseName);

        [Theory]
        [MemberData(nameof(Links_PaxAndGnu_Data))]
        public void Read_PaxGEA_LongLinks_ManuallyCreated(string testCaseName, CompressionMethod compressionMethod) =>
            GenerateExpectedFilesAndCompare(compressionMethod, TestTarFormat.pax_gea, testCaseName);

        [Theory]
        [MemberData(nameof(Links_PaxAndGnu_Data))]
        public void Read_Gnu_LongLinks_ManuallyCreated(string testCaseName, CompressionMethod compressionMethod) =>
            GenerateExpectedFilesAndCompare(compressionMethod, TestTarFormat.gnu, testCaseName);

        [Theory]
        [MemberData(nameof(Links_PaxAndGnu_Data))]
        public void Read_OldGnu_LongLinks_ManuallyCreated(string testCaseName, CompressionMethod compressionMethod) =>
            GenerateExpectedFilesAndCompare(compressionMethod, TestTarFormat.oldgnu, testCaseName);

        #endregion

        #region Data

        // Can be read by all formats
        public static IEnumerable<object[]> Links_V7_Data() =>
            from string testCase in new[] { TestCaseHardLink, TestCaseSymLink, TestCaseFolderSymlinkFolderSubFolderFile }
            from CompressionMethod compressionMethod in Enum.GetValues<CompressionMethod>()
            select new object[] { testCase, compressionMethod };

        // Can be read by pax, gnu and oldgnu
        public static IEnumerable<object[]> Links_PaxAndGnu_Data() =>
            from CompressionMethod compressionMethod in Enum.GetValues<CompressionMethod>()
            select new object[] { TestCaseLongSymlink, compressionMethod };

        #endregion

        #region Helpers

        private void GenerateExpectedFilesAndCompare(CompressionMethod compressionMethod, TestTarFormat format, string testCaseName)
        {
            using TempDirectory tmpDir = GenerateExpectedLinkFilesAndFolders(testCaseName);
            string tarFilePath = GetTarFile(compressionMethod, format, testCaseName);
            CompareTarFileContentsWithDirectoryContents(compressionMethod, format, tarFilePath, tmpDir.Path, testCaseName);
        }

        private TempDirectory GenerateExpectedLinkFilesAndFolders(string testCaseName)
        {
            var tmpDir = new TempDirectory();
            switch (testCaseName)
            {
                case TestCaseHardLink:
                    GenerateHardLinkTestAssets(tmpDir);
                    break;
                case TestCaseSymLink:
                    GenerateSymlinkTestAssets(tmpDir);
                    break;
                case TestCaseLongSymlink:
                    GenerateLongSymlinkTestAssets(tmpDir);
                    break;
                case TestCaseFolderSymlinkFolderSubFolderFile:
                    GenerateFolderSymlinkTestAssets(tmpDir);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported test case: {testCaseName}");
            }
            return tmpDir;
        }

        // Generates a file and folder structure like the one from the 'file_hardlink' test case.
        private void GenerateHardLinkTestAssets(TempDirectory tmpDir)
        {
            string targetPath = Path.Join(tmpDir.Path, "file.txt");
            GenerateFileAsset(targetPath);

            string linkPath = Path.Join(tmpDir.Path, "hardlink.txt");

            CreateHardLink(linkPath, targetPath);

            Assert.True(File.Exists(linkPath), $"Hard link was not created: {linkPath}");
        }

        // Generates a file and folder structure like the one from the 'file_symlink' test case.
        private void GenerateSymlinkTestAssets(TempDirectory tmpDir)
        {
            string targetPath = Path.Join(tmpDir.Path, "file.txt");
            GenerateFileAsset(targetPath);

            FileInfo linkInfo = new(Path.Join(tmpDir.Path, "link.txt"));
            linkInfo.CreateAsSymbolicLink(targetPath);
            Assert.True(linkInfo.Exists, $"File symbolic link was not created: {linkInfo.FullName}");

            var targetInfo = linkInfo.ResolveLinkTarget(returnFinalTarget: true);
            Assert.NotNull(targetInfo);
            Assert.True(targetInfo.Exists, $"File symbolic link target was not found: {targetInfo.FullName}");
        }


        private void GenerateLongSymlinkTestAssets(TempDirectory tmpDir)
        {
            string linkFileName = "link.txt";

            string dirName = "000000000011111111112222222222333333333344444444445555555555666666666677777777778888888888999999999900000000001111111111222222222233333333334444444444555555555566666666667777777777888888888899999999990000000000111111111122222222223333333333444444444455555";

            string targetFileName = "00000000001111111111222222222233333333334444444444555555555566666666667777777777888888888899999999990000000000111111111122222222223333333333444444444455555555556666666666777777777788888888889999999999000000000011111111112222222222333333333344444444445.txt";

            string dirPath = Path.Join(tmpDir.Path, dirName);
            string targetPath = Path.Join(dirPath, targetFileName);
            string linkPath = Path.Join(tmpDir.Path, linkFileName);

            DirectoryInfo dir1Info = new(dirPath);
            dir1Info.Create();
            Assert.True(dir1Info.Exists, $"Directory was not created: {dirPath}");

            GenerateFileAsset(targetPath);

            FileInfo linkInfo = new(linkPath);
            linkInfo.CreateAsSymbolicLink(targetPath);
            Assert.True(linkInfo.Exists, $"File symbolic link was not created: {linkPath}");

            var targetInfo = linkInfo.ResolveLinkTarget(returnFinalTarget: true);
            Assert.NotNull(targetInfo);
            Assert.True(targetInfo.Exists, $"File symbolic link target was not found: {targetInfo.FullName}");
        }

        // Creates a file in the specified location.
        private void GenerateFileAsset(string filePath)
        {
            using (StreamWriter writer = File.CreateText(filePath))
            {
                writer.Write("Hello world!");
            }
            Assert.True(File.Exists(filePath), $"File was not created: {filePath}");
        }

        // Generates a file and folder structure like the one from the 'foldersymlink_folder_subfolder_file' test case.
        private void GenerateFolderSymlinkTestAssets(TempDirectory tmpDir)
        {
            string parentPath = Path.Join(tmpDir.Path, "parent");
            Directory.CreateDirectory(parentPath);
            Assert.True(Directory.Exists(parentPath), $"Parent directory was not created: {parentPath}");

            string childPath = Path.Join(parentPath, "child");
            Directory.CreateDirectory(childPath);
            Assert.True(Directory.Exists(childPath), $"Child directory was not created: {childPath}");

            string filePath = Path.Join(childPath, "file.txt");
            GenerateFileAsset(filePath);

            DirectoryInfo linkInfo = new(Path.Join(tmpDir.Path, "childlink"));
            linkInfo.CreateAsSymbolicLink(childPath);
            Assert.True(linkInfo.Exists, $"Directory symbolic link was not created: {linkInfo.FullName}");

            var targetInfo = linkInfo.ResolveLinkTarget(returnFinalTarget: true);
            Assert.NotNull(targetInfo);
            Assert.True(targetInfo.Exists, $"Directory symbolic link target was not found: {targetInfo.FullName}");
        }

        #endregion
    }
}