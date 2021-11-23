// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace System.IO.Compression.Tests
{
    // TODO: Make this class run in many platforms, not just Windows
    public partial class TarTests_ManuallyCreatedAssets : TarTests
    {
        // Workaround for 'Read_Uncompressed_V7_Links'
        [Theory]
        [InlineData("file_hardlink")]
        [InlineData("file_symlink")]
        [InlineData("foldersymlink_folder_subfolder_file")]
        public void Read_Uncompressed_V7_Links_ManuallyCreated(string testCaseName)
        {
            using TempDirectory tmpDir = GenerateExpectedLinkFilesAndFolders(testCaseName);

            VerifyTarFileContents(
                CompressionMethod.Uncompressed,
                GetTarFile(CompressionMethod.Uncompressed, TarFormat.V7, testCaseName),
                tmpDir.Path);
        }

        // Workaround for 'Read_Gzip_V7_Links'
        [Theory]
        [InlineData("file_hardlink")]
        [InlineData("file_symlink")]
        [InlineData("foldersymlink_folder_subfolder_file")]
        public void Read_Gzip_V7_Links_ManuallyCreated(string testCaseName)
        {
            using TempDirectory tmpDir = GenerateExpectedLinkFilesAndFolders(testCaseName);

            VerifyTarFileContents(
                CompressionMethod.GZip,
                GetTarFile(CompressionMethod.GZip, TarFormat.V7, testCaseName),
                tmpDir.Path);
        }


        // Workaround for 'Read_Uncompressed_Ustar_Links'
        [Theory]
        [InlineData("file_hardlink")]
        [InlineData("file_symlink")]
        [InlineData("foldersymlink_folder_subfolder_file")]
        public void Read_Uncompressed_Ustar_Links_ManuallyCreated(string testCaseName)
        {
            using TempDirectory tmpDir = GenerateExpectedLinkFilesAndFolders(testCaseName);

            VerifyTarFileContents(
                CompressionMethod.Uncompressed,
                GetTarFile(CompressionMethod.Uncompressed, TarFormat.Ustar, testCaseName),
                tmpDir.Path);
        }

        // Workaround for 'Read_Gzip_Ustar_Links'
        [Theory]
        [InlineData("file_hardlink")]
        [InlineData("file_symlink")]
        [InlineData("foldersymlink_folder_subfolder_file")]
        public void Read_Gzip_Ustar_Links_ManuallyCreated(string testCaseName)
        {
            using TempDirectory tmpDir = GenerateExpectedLinkFilesAndFolders(testCaseName);

            VerifyTarFileContents(
                CompressionMethod.GZip,
                GetTarFile(CompressionMethod.GZip, TarFormat.Ustar, testCaseName),
                tmpDir.Path);
        }

        private TempDirectory GenerateExpectedLinkFilesAndFolders(string testCaseName)
        {
            var tmpDir = new TempDirectory();
            switch (testCaseName)
            {
                case "file_hardlink":
                    GenerateHardLinkTestAssets(tmpDir);
                    break;
                case "file_symlink":
                    GenerateSymlinkTestAssets(tmpDir);
                    break;
                case "foldersymlink_folder_subfolder_file":
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
        }

        // Generates a file and folder structure like the one from the 'file_symlink' test case.
        private void GenerateSymlinkTestAssets(TempDirectory tmpDir)
        {
            string targetPath = Path.Join(tmpDir.Path, "file.txt");
            GenerateFileAsset(targetPath);

            string linkPath = Path.Join(tmpDir.Path, "link.txt");
            File.CreateSymbolicLink(linkPath, targetPath);
        }

        // Creates a file in the specified location.
        private void GenerateFileAsset(string filePath)
        {
            using (StreamWriter writer = File.CreateText(filePath))
            {
                writer.Write("Hello world!");
            }
        }

        // Generates a file and folder structure like the one from the 'foldersymlink_folder_subfolder_file' test case.
        private void GenerateFolderSymlinkTestAssets(TempDirectory tmpDir)
        {
            string parentPath = Path.Join(tmpDir.Path, "parent");
            Directory.CreateDirectory(parentPath);

            string childPath = Path.Join(parentPath, "child");
            Directory.CreateDirectory(childPath);

            string filePath = Path.Join(childPath, "file.txt");
            GenerateFileAsset(filePath);

            string linkPath = Path.Join(tmpDir.Path, "childlink");
            Directory.CreateSymbolicLink(linkPath, childPath);
        }

        private void CreateHardLink(string linkPath, string targetPath) => Interop.Kernel32.CreateHardLink(linkPath, targetPath);
    }
}
