// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO.Enumeration;
using System.Linq;
using Xunit;

namespace System.IO.Compression.Tests
{
    public partial class TarTests : FileCleanupTestBase
    {
        #region Basic validation

        [Fact]
        public void Null_Stream()
            => Assert.Throws<ArgumentNullException>(() => new TarArchive(stream: null, new TarOptions()));

        [Fact]
        public void Null_Options()
        {
            var archive = new TarArchive(new MemoryStream(), options: null);
            Assert.NotNull(archive.Options);
            Assert.False(archive.Options.LeaveOpen);
            Assert.Equal(TarArchiveMode.Read, archive.Options.Mode);
        }

        [Theory]
        [InlineData((TarArchiveMode)int.MinValue)]
        [InlineData((TarArchiveMode)(-1))]
        [InlineData((TarArchiveMode)int.MaxValue)]
        public void Invalid_TarArchiveMode(TarArchiveMode mode)
            => Assert.Throws<ArgumentOutOfRangeException>(() => new TarArchive(new MemoryStream(), new TarOptions() { Mode = mode }));

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Verify_LeaveOpen(bool leaveOpen)
        {
            var stream = new MemoryStream();
            using (var archive = new TarArchive(stream, new TarOptions() { LeaveOpen = leaveOpen })) { }

            if (leaveOpen)
            {
                stream.WriteByte(0);
                stream.Dispose();
            }
            else
            {
                Assert.Throws<ObjectDisposedException>(() => stream.WriteByte(0));
            }
        }

        [Fact]
        public void Verify_TarArchive_Disposed()
        {
            var archive = new TarArchive(new MemoryStream(), new TarOptions() { LeaveOpen = false });

            archive.Dispose();
            archive.Dispose(); // A second dispose call should be a no-op

            Assert.Throws<ObjectDisposedException>(() => archive.GetNextEntry());
        }

        #endregion

        #region V7 Uncompressed

        [Theory]
        [MemberData(nameof(Normal_FilesAndFolders_Data))]
        public void Read_Uncompressed_V7_NormalFilesAndFolders(string testCaseName) =>
            VerifyTarFileContents(
                CompressionMethod.Uncompressed,
                GetTarFile(CompressionMethod.Uncompressed, TarFormat.V7, testCaseName),
                Path.Join(Directory.GetCurrentDirectory(), GetTestCaseFolderName(testCaseName)));

        // dotnet restore extracts nupkg symlinks and hardlinks as normal files/folders
        [ActiveIssue("https://github.com/NuGet/Home/issues/10734")]
        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Uncompressed_V7_Links(string testCaseName) =>
            VerifyTarFileContents(
                CompressionMethod.Uncompressed,
                GetTarFile(CompressionMethod.Uncompressed, TarFormat.V7, testCaseName),
                Path.Join(Directory.GetCurrentDirectory(), GetTestCaseFolderName(testCaseName)));

        #endregion

        #region V7 GZip

        [Theory]
        [MemberData(nameof(Normal_FilesAndFolders_Data))]
        public void Read_Gzip_V7_NormalFilesAndFolders(string testCaseName) =>
            VerifyTarFileContents(
                CompressionMethod.GZip,
                GetTarFile(CompressionMethod.GZip, TarFormat.V7, testCaseName),
                Path.Join(Directory.GetCurrentDirectory(), GetTestCaseFolderName(testCaseName)));

        // dotnet restore extracts nupkg symlinks and hardlinks as normal files/folders
        [ActiveIssue("https://github.com/NuGet/Home/issues/10734")]
        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Gzip_V7_Links(string testCaseName) =>
            VerifyTarFileContents(
                CompressionMethod.GZip,
                GetTarFile(CompressionMethod.GZip, TarFormat.V7, testCaseName),
                Path.Join(Directory.GetCurrentDirectory(), GetTestCaseFolderName(testCaseName)));

        #endregion

        #region Ustar Uncompressed

        [Theory]
        [MemberData(nameof(Normal_FilesAndFolders_Data))]
        public void Read_Uncompressed_Ustar_NormalFilesAndFolders(string testCaseName) =>
            VerifyTarFileContents(
                CompressionMethod.Uncompressed,
                GetTarFile(CompressionMethod.Uncompressed, TarFormat.Ustar, testCaseName),
                Path.Join(Directory.GetCurrentDirectory(), GetTestCaseFolderName(testCaseName)));

        // dotnet restore extracts nupkg symlinks and hardlinks as normal files/folders
        [ActiveIssue("https://github.com/NuGet/Home/issues/10734")]
        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Uncompressed_Ustar_Links(string testCaseName) =>
            VerifyTarFileContents(
                CompressionMethod.Uncompressed,
                GetTarFile(CompressionMethod.Uncompressed, TarFormat.Ustar, testCaseName),
                Path.Join(Directory.GetCurrentDirectory(), GetTestCaseFolderName(testCaseName)));

        #endregion

        #region Ustar GZip

        [Theory]
        [MemberData(nameof(Normal_FilesAndFolders_Data))]
        public void Read_Gzip_Ustar_NormalFilesAndFolders(string testCaseName) =>
            VerifyTarFileContents(
                CompressionMethod.GZip,
                GetTarFile(CompressionMethod.GZip, TarFormat.Ustar, testCaseName),
                Path.Join(Directory.GetCurrentDirectory(), GetTestCaseFolderName(testCaseName)));

        // dotnet restore extracts nupkg symlinks and hardlinks as normal files/folders
        [ActiveIssue("https://github.com/NuGet/Home/issues/10734")]
        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Gzip_Ustar_Links(string testCaseName) =>
            VerifyTarFileContents(
                CompressionMethod.GZip,
                GetTarFile(CompressionMethod.GZip, TarFormat.Ustar, testCaseName),
                Path.Join(Directory.GetCurrentDirectory(), GetTestCaseFolderName(testCaseName)));

        #endregion

        #region Helpers

        public static IEnumerable<object[]> Normal_FilesAndFolders_Data()
        {
            yield return new object[] { "file" };
            yield return new object[] { "folder_file" };
            yield return new object[] { "folder_file_utf8" };
            yield return new object[] { "folder_subfolder_file" };
            // TODO: Test separately when gnu/oldgnu are implemented.
            // Only gnu and oldgnu fully support a very long path.
            // The rest of the formats are unable to archive all the folders and the file in the long path correctly
            // yield return new object[] { "longpath" };
        }

        public static IEnumerable<object[]> Links_Data()
        {
            yield return new object[] { "file_hardlink" };
            yield return new object[] { "file_symlink" };
            yield return new object[] { "foldersymlink_folder_subfolder_file" };
        }

        protected enum CompressionMethod
        {
            Uncompressed,
            GZip,
        }

        protected static string GetTarFile(CompressionMethod compressionMethod, TarFormat format, string testCaseName)
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

        // Opens the specified tar file as a stream, decompresses it if necessary, then verifies the contents.
        protected void VerifyTarFileContents(CompressionMethod compressionMethod, string tarFilePath, string expectedFilesDir)
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

            int entryCount = 0;
            while ((entry = archive.GetNextEntry()) != null)
            {
                entryCount++;
                string fullPath = Path.Join(expectedFilesDir, entry.Name);
                switch (entry.TypeFlag)
                {
                    case TarArchiveEntryType.OldNormal:
                    case TarArchiveEntryType.Normal:
                    case TarArchiveEntryType.Link:
                        Assert.True(File.Exists(fullPath), $"Normal file exists: {entry.Name}");
                        // TODO: Hardlink should have the link to the real file in Prefix, check it
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
            Assert.Equal(GetExpectedEntriesCount(expectedFilesDir), entryCount);
        }

        private int GetExpectedEntriesCount(string expectedFilesDir)
        {
            var entries = new FileSystemEnumerable<string>(
                directory: expectedFilesDir,
                transform: (ref FileSystemEntry entry) => entry.ToFullPath(),
                options: new EnumerationOptions() { RecurseSubdirectories = true })
            {
                // Avoid recursing symlinks to directories, otherwise entries show up twice in the
                // enumeration (once as the original file, and once as the file inside the symlinked directory)
                ShouldRecursePredicate = (ref FileSystemEntry entry) =>
                    entry.IsDirectory && !entry.Attributes.HasFlag(FileAttributes.ReparsePoint)
            };
            return entries.Count();
        }

        #endregion
    }
}
