// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace System.IO.Compression.Tests
{
    // Tests that manually create expected files.
    // These test methods are a workaround for the nupkg bug preventing the
    // correct extraction of symlink and hardlink files into disk.
    public partial class TarTests : FileCleanupTestBase
    {
        // 'dotnet restore' unexpectedly extracts nupkg symlinks and hardlinks as normal files/folders
        [ActiveIssue("https://github.com/NuGet/Home/issues/10734")]
        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Links(string testCaseName)
        {
            foreach (CompressionMethod compressionMethod in Enum.GetValues<CompressionMethod>())
            {
                foreach (TestTarFormat format in Enum.GetValues<TestTarFormat>())
                {
                    VerifyTarFileContents(compressionMethod, format, testCaseName);
                }
            }
        }

        #region V7

        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Uncompressed_V7_Links_ManuallyCreated(string testCaseName) =>
            GenerateExpectedFilesAndCompare(CompressionMethod.Uncompressed, TestTarFormat.v7, testCaseName);

        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Gzip_V7_Links_ManuallyCreated(string testCaseName) =>
            GenerateExpectedFilesAndCompare(CompressionMethod.GZip, TestTarFormat.v7, testCaseName);

        #endregion

        #region Ustar

        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Uncompressed_Ustar_Links_ManuallyCreated(string testCaseName) =>
            GenerateExpectedFilesAndCompare(CompressionMethod.Uncompressed, TestTarFormat.ustar, testCaseName);

        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Gzip_Ustar_Links_ManuallyCreated(string testCaseName) =>
            GenerateExpectedFilesAndCompare(CompressionMethod.GZip, TestTarFormat.ustar, testCaseName);

        #endregion

        #region Pax

        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Uncompressed_Pax_Links_ManuallyCreated(string testCaseName) =>
            GenerateExpectedFilesAndCompare(CompressionMethod.Uncompressed, TestTarFormat.pax, testCaseName);

        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Gzip_Pax_Links_ManuallyCreated(string testCaseName) =>
            GenerateExpectedFilesAndCompare(CompressionMethod.GZip, TestTarFormat.pax, testCaseName);

        #endregion

        #region Pax with Global Extended Attributes

        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Uncompressed_PaxGEA_Links_ManuallyCreated(string testCaseName) =>
            GenerateExpectedFilesAndCompare(CompressionMethod.Uncompressed, TestTarFormat.pax_gea, testCaseName);

        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Gzip_Ustar_PaxGEA_Links_ManuallyCreated(string testCaseName) =>
            GenerateExpectedFilesAndCompare(CompressionMethod.GZip, TestTarFormat.pax_gea, testCaseName);

        #endregion

        #region Gnu

        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Uncompressed_Gnu_Links_ManuallyCreated(string testCaseName) =>
            GenerateExpectedFilesAndCompare(CompressionMethod.Uncompressed, TestTarFormat.gnu, testCaseName);

        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Gzip_Gnu_Links_ManuallyCreated(string testCaseName) =>
            GenerateExpectedFilesAndCompare(CompressionMethod.GZip, TestTarFormat.gnu, testCaseName);

        #endregion

        #region Old Gnu

        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Uncompressed_OldGnu_Links_ManuallyCreated(string testCaseName) =>
            GenerateExpectedFilesAndCompare(CompressionMethod.Uncompressed, TestTarFormat.oldgnu, testCaseName);

        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Gzip_OldGnu_Links_ManuallyCreated(string testCaseName) =>
            GenerateExpectedFilesAndCompare(CompressionMethod.GZip, TestTarFormat.oldgnu, testCaseName);

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