// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using Xunit;

namespace System.IO.Compression.Tests
{
    public partial class TarTests : FileCleanupTestBase
    {
        #region V7 Uncompressed

        [Theory]
        [InlineData("file")]
        [InlineData("folder_file")]
        [InlineData("folder_subfolder_file")]
        public void Read_Uncompressed_V7_NormalFilesAndFolders(string testCaseName) =>
            VerifyTarFileContents(
                CompressionMethod.Uncompressed,
                GetTarFile(CompressionMethod.Uncompressed, TarFormat.V7, testCaseName),
                Path.Join(Directory.GetCurrentDirectory(), GetTestCaseFolderName(testCaseName)));

        // dotnet restore extracts nupkg symlinks and hardlinks as normal files/folders
        [ActiveIssue("https://github.com/NuGet/Home/issues/10734")]
        [Theory]
        [InlineData("file_hardlink")]
        [InlineData("file_symlink")]
        [InlineData("foldersymlink_folder_subfolder_file")]
        public void Read_Uncompressed_V7_Links(string testCaseName) =>
            VerifyTarFileContents(
                CompressionMethod.Uncompressed,
                GetTarFile(CompressionMethod.Uncompressed, TarFormat.V7, testCaseName),
                Path.Join(Directory.GetCurrentDirectory(), GetTestCaseFolderName(testCaseName)));

        // Workaround for 'Read_Uncompressed_V7_Links'
        [Theory]
        [InlineData("file_hardlink")]
        [InlineData("file_symlink")]
        [InlineData("foldersymlink_folder_subfolder_file")]
        public void Read_Uncompressed_V7_Links_ManuallyCreated(string testCaseName)
        {
            //Diagnostics.Debugger.Launch();
            using TempDirectory tmpDir = GenerateExpectedLinkFilesAndFolders(testCaseName);

            VerifyTarFileContents(
                CompressionMethod.Uncompressed,
                GetTarFile(CompressionMethod.Uncompressed, TarFormat.V7, testCaseName),
                tmpDir.Path);
        }

        #endregion

        #region V7 GZip

        [Theory]
        [InlineData("file")]
        [InlineData("folder_file")]
        [InlineData("folder_subfolder_file")]
        public void Read_Gzip_V7_NormalFilesAndFolders(string testCaseName) =>
            VerifyTarFileContents(
                CompressionMethod.GZip,
                GetTarFile(CompressionMethod.GZip, TarFormat.V7, testCaseName),
                Path.Join(Directory.GetCurrentDirectory(), GetTestCaseFolderName(testCaseName)));

        // dotnet restore extracts nupkg symlinks and hardlinks as normal files/folders
        [ActiveIssue("https://github.com/NuGet/Home/issues/10734")]
        [Theory]
        [InlineData("file_hardlink")]
        [InlineData("file_symlink")]
        [InlineData("foldersymlink_folder_subfolder_file")]
        public void Read_Gzip_V7_Links(string testCaseName) =>
            VerifyTarFileContents(
                CompressionMethod.GZip,
                GetTarFile(CompressionMethod.GZip, TarFormat.V7, testCaseName),
                Path.Join(Directory.GetCurrentDirectory(), GetTestCaseFolderName(testCaseName)));

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

        #endregion

        #region Helpers

        private enum CompressionMethod
        {
            Uncompressed,
            GZip,
        }

        private static string GetTarFile(CompressionMethod compressionMethod, TarFormat format, string testCaseName)
        {
            (string compressionMethodFolder, string fileExtension) = compressionMethod switch
            {
                CompressionMethod.Uncompressed => ("tar", ".tar"),
                CompressionMethod.GZip => ("targz", ".tar.gz"),
                _ => throw new NotSupportedException(),
            };

            string formatFolder = format.ToString().ToLowerInvariant();

            return Path.Join(Directory.GetCurrentDirectory(), "TarTestData", compressionMethodFolder, formatFolder, testCaseName + fileExtension);
        }

        private static string GetTestCaseFolderName(string testCaseName) => Path.Join("TarTestData", "unarchived", testCaseName);

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

        // Opens the specified tar file as a stream, decompresses it if necessary, then verifies the contents.
        private void VerifyTarFileContents(CompressionMethod compressionMethod, string tarFilePath, string expectedFilesDir)
        {
            using FileStream fs = File.Open(tarFilePath, FileMode.Open);

            switch (compressionMethod)
            {
                case CompressionMethod.Uncompressed:
                    VerifyUncompressedTarStreamContents(fs, expectedFilesDir);
                    break;

                case CompressionMethod.GZip:
                    using (var decompressor = new GZipStream(fs, CompressionMode.Decompress))
                    {
                        VerifyUncompressedTarStreamContents(decompressor, expectedFilesDir);
                    }
                   break;

                default:
                    throw new NotSupportedException();
            }
        }

        // Reads the contents of a stream wrapping an uncompressed tar archive, then compares
        // the entries with the filesystem entries found in the specified folder path.
        private void VerifyUncompressedTarStreamContents(Stream tarStream, string expectedFilesDir)
        {
            TarOptions options = new() { Mode = TarArchiveMode.Read };
            using var archive = new TarArchive(tarStream, options);
            TarArchiveEntry? entry = null;
            while ((entry = archive.GetNextEntry()) != null)
            {
                string fullPath = Path.Join(expectedFilesDir, entry.Name);
                switch (entry.TypeFlag)
                {
                    case TarArchiveEntryType.OldNormal:
                    case TarArchiveEntryType.Link:
                        Assert.True(File.Exists(fullPath), $"Normal file exists: {entry.Name}");
                        break;
                    case TarArchiveEntryType.Directory:
                        Assert.True(Directory.Exists(fullPath), $"Directory exists: {entry.Name}");
                        break;
                    case TarArchiveEntryType.SymbolicLink:
                        var symLinkInfo = new FileInfo(fullPath);
                        Assert.True(symLinkInfo.Attributes.HasFlag(FileAttributes.ReparsePoint), "Expected file has ReparsePoint flag");
                        if (symLinkInfo.Attributes.HasFlag(FileAttributes.Directory))
                        {
                            Assert.True(Directory.Exists(fullPath), $"Symlink directory exists: {entry.Name}");
                        }
                        else
                        {
                            Assert.True(File.Exists(fullPath), $"Symlink file exists: {entry.Name}");
                        }
                        Assert.True(!string.IsNullOrWhiteSpace(entry.LinkName), "LinkName string is not null or whitespace");
                        break;
                    default:
                        throw new NotSupportedException($"Entry type: {entry.TypeFlag}");
                }
            }
        }

        #endregion
    }
}
