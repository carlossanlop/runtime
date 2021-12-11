// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace System.IO.Compression.Tests
{
    public partial class TarTests : FileCleanupTestBase
    {
        #region Tests that manually create expected files

        // Workaround for 'Read_Uncompressed_V7_Links'
        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Uncompressed_V7_Links_ManuallyCreated(string testCaseName)
        {
            using TempDirectory tmpDir = GenerateExpectedLinkFilesAndFolders(testCaseName);

            CompareTarFileContentsWithDirectoryContents(
                CompressionMethod.Uncompressed,
                GetTarFile(CompressionMethod.Uncompressed, TarFormat.V7, testCaseName),
                tmpDir.Path);
        }

        // Workaround for 'Read_Gzip_V7_Links'
        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Gzip_V7_Links_ManuallyCreated(string testCaseName)
        {
            using TempDirectory tmpDir = GenerateExpectedLinkFilesAndFolders(testCaseName);

            CompareTarFileContentsWithDirectoryContents(
                CompressionMethod.GZip,
                GetTarFile(CompressionMethod.GZip, TarFormat.V7, testCaseName),
                tmpDir.Path);
        }


        // Workaround for 'Read_Uncompressed_Ustar_Links'
        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Uncompressed_Ustar_Links_ManuallyCreated(string testCaseName)
        {
            using TempDirectory tmpDir = GenerateExpectedLinkFilesAndFolders(testCaseName);

            CompareTarFileContentsWithDirectoryContents(
                CompressionMethod.Uncompressed,
                GetTarFile(CompressionMethod.Uncompressed, TarFormat.Ustar, testCaseName),
                tmpDir.Path);
        }

        // Workaround for 'Read_Gzip_Ustar_Links'
        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Gzip_Ustar_Links_ManuallyCreated(string testCaseName)
        {
            using TempDirectory tmpDir = GenerateExpectedLinkFilesAndFolders(testCaseName);

            CompareTarFileContentsWithDirectoryContents(
                CompressionMethod.GZip,
                GetTarFile(CompressionMethod.GZip, TarFormat.Ustar, testCaseName),
                tmpDir.Path);
        }

        #endregion

        #region Helpers

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