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
        #region Constants

        // These constants were used by the runtime-assets
        // script to generate all the tar files.
        private const string TestUser = "dotnet";
        private const string TestGroup = "devdiv";

        private const int TestMode = 484; // 744 in octal

        private const int TestUid = 7913;
        private const int TestGid = 3579;

        private const string CharDevName = "chardev";
        private const int CharDevMajor = 49;
        private const int CharDevMinor = 86;

        private const string BlockDevName = "blockdev";
        private const int BlockDevMajor = 71;
        private const int BlockDevMinor = 53;

        private const string FifoName = "fifofile";

        private const string TestCaseHardLink = "file_hardlink";
        private const string TestCaseSymLink = "file_symlink";
        private const string TestCaseFolderSymlinkFolderSubFolderFile = "foldersymlink_folder_subfolder_file";
        private const string TestCaseSpecialFiles = "specialfiles";

        #endregion

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
        [MemberData(nameof(Normal_FilesAndFolders_V7_Data))]
        public void Read_Uncompressed_V7_NormalFilesAndFolders(string testCaseName) =>
            VerifyTarFileContents(CompressionMethod.Uncompressed, TarFormat.V7, testCaseName);

        // dotnet restore extracts nupkg symlinks and hardlinks as normal files/folders
        [ActiveIssue("https://github.com/NuGet/Home/issues/10734")]
        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Uncompressed_V7_Links(string testCaseName) =>
            VerifyTarFileContents(CompressionMethod.Uncompressed, TarFormat.V7, testCaseName);

        #endregion

        #region V7 GZip

        [Theory]
        [MemberData(nameof(Normal_FilesAndFolders_V7_Data))]
        public void Read_Gzip_V7_NormalFilesAndFolders(string testCaseName) =>
            VerifyTarFileContents(CompressionMethod.GZip, TarFormat.V7, testCaseName);

        // dotnet restore extracts nupkg symlinks and hardlinks as normal files/folders
        [ActiveIssue("https://github.com/NuGet/Home/issues/10734")]
        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Gzip_V7_Links(string testCaseName) =>
            VerifyTarFileContents(CompressionMethod.GZip, TarFormat.V7, testCaseName);

        #endregion

        #region Ustar Uncompressed

        [Theory]
        [MemberData(nameof(Normal_FilesAndFolders_Ustar_Data))]
        public void Read_Uncompressed_Ustar_NormalFilesAndFolders(string testCaseName) =>
            VerifyTarFileContents(CompressionMethod.Uncompressed, TarFormat.Ustar, testCaseName);

        // dotnet restore extracts nupkg symlinks and hardlinks as normal files/folders
        [ActiveIssue("https://github.com/NuGet/Home/issues/10734")]
        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Uncompressed_Ustar_Links(string testCaseName) =>
            VerifyTarFileContents(CompressionMethod.Uncompressed, TarFormat.Ustar, testCaseName);

        #endregion

        #region Ustar GZip

        [Theory]
        [MemberData(nameof(Normal_FilesAndFolders_Ustar_Data))]
        public void Read_Gzip_Ustar_NormalFilesAndFolders(string testCaseName) =>
            VerifyTarFileContents(CompressionMethod.GZip, TarFormat.Ustar, testCaseName);

        // dotnet restore extracts nupkg symlinks and hardlinks as normal files/folders
        [ActiveIssue("https://github.com/NuGet/Home/issues/10734")]
        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Gzip_Ustar_Links(string testCaseName) =>
            VerifyTarFileContents(CompressionMethod.GZip, TarFormat.Ustar, testCaseName);

        #endregion

        #region Pax Uncompressed

        [Theory]
        [MemberData(nameof(Normal_FilesAndFolders_PaxAndGnu_Data))]
        public void Read_Uncompressed_Pax_NormalFilesAndFolders(string testCaseName) =>
            VerifyTarFileContents(CompressionMethod.Uncompressed, TarFormat.Pax, testCaseName);

        // dotnet restore extracts nupkg symlinks and hardlinks as normal files/folders
        [ActiveIssue("https://github.com/NuGet/Home/issues/10734")]
        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Uncompressed_Pax_Links(string testCaseName) =>
            VerifyTarFileContents(CompressionMethod.Uncompressed, TarFormat.Pax, testCaseName);

        #endregion

        #region Pax GZip

        [Theory]
        [MemberData(nameof(Normal_FilesAndFolders_PaxAndGnu_Data))]
        public void Read_Gzip_Pax_NormalFilesAndFolders(string testCaseName) =>
            VerifyTarFileContents(CompressionMethod.GZip, TarFormat.Pax, testCaseName);

        // dotnet restore extracts nupkg symlinks and hardlinks as normal files/folders
        [ActiveIssue("https://github.com/NuGet/Home/issues/10734")]
        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Gzip_Pax_Links(string testCaseName) =>
            VerifyTarFileContents(CompressionMethod.GZip, TarFormat.Pax, testCaseName);

        #endregion

        #region Gnu Uncompressed

        [Theory]
        [MemberData(nameof(Normal_FilesAndFolders_PaxAndGnu_Data))]
        public void Read_Uncompressed_Gnu_NormalFilesAndFolders(string testCaseName) =>
            VerifyTarFileContents(CompressionMethod.Uncompressed, TarFormat.Gnu, testCaseName);

        // dotnet restore extracts nupkg symlinks and hardlinks as normal files/folders
        [ActiveIssue("https://github.com/NuGet/Home/issues/10734")]
        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Uncompressed_Gnu_Links(string testCaseName) =>
            VerifyTarFileContents(CompressionMethod.Uncompressed, TarFormat.Gnu, testCaseName);

        #endregion

        #region Gnu GZip

        [Theory]
        [MemberData(nameof(Normal_FilesAndFolders_PaxAndGnu_Data))]
        public void Read_Gzip_Gnu_NormalFilesAndFolders(string testCaseName) =>
            VerifyTarFileContents(CompressionMethod.GZip, TarFormat.Gnu, testCaseName);

        // dotnet restore extracts nupkg symlinks and hardlinks as normal files/folders
        [ActiveIssue("https://github.com/NuGet/Home/issues/10734")]
        [Theory]
        [MemberData(nameof(Links_Data))]
        public void Read_Gzip_Gnu_Links(string testCaseName) =>
            VerifyTarFileContents(CompressionMethod.GZip, TarFormat.Gnu, testCaseName);

        #endregion

        #region Data

        public static IEnumerable<object[]> Normal_FilesAndFolders_V7_Data()
        {
            yield return new object[] { "file" };
            yield return new object[] { "folder_file" };
            yield return new object[] { "folder_file_utf8" };
            yield return new object[] { "folder_subfolder_file" };
        }

        public static IEnumerable<object[]> Normal_FilesAndFolders_Ustar_Data()
        {
            foreach (var item in Normal_FilesAndFolders_V7_Data())
            {
                yield return item;
            }
            yield return new object[] { TestCaseSpecialFiles };
            yield return new object[] { "longpath_splitable_under255" };
        }

        public static IEnumerable<object[]> Normal_FilesAndFolders_PaxAndGnu_Data()
        {
            foreach (var item in Normal_FilesAndFolders_Ustar_Data())
            {
                yield return item;
            }
            yield return new object[] { "longfilename_over100_under255" };
            yield return new object[] { "longpath_over255" };
        }

        public static IEnumerable<object[]> Links_Data()
        {
            yield return new object[] { TestCaseHardLink };
            yield return new object[] { TestCaseSymLink };
            yield return new object[] { TestCaseFolderSymlinkFolderSubFolderFile };
        }

        #endregion

        #region Helpers

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

        private static string GetTestCaseFolderPath(string testCaseName) =>
            Path.Join(Directory.GetCurrentDirectory(), "TarTestData", "unarchived", testCaseName);

        protected void CompareTarFileContentsWithDirectoryContents(CompressionMethod compressionMethod, string tarFilePath, string expectedFilesDir)
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

        private void VerifyTarFileContents(CompressionMethod compressionMethod, TarFormat format, string testCaseName)
        {
            string tarFilePath = GetTarFile(compressionMethod, format, testCaseName);
            string expectedFilesDir = GetTestCaseFolderPath(testCaseName);

            CompareTarFileContentsWithDirectoryContents(compressionMethod, tarFilePath, expectedFilesDir);
        }

        // Reads the contents of a stream wrapping an uncompressed tar archive, then compares
        // the entries with the filesystem entries found in the specified folder path.
        private void VerifyUncompressedTarStreamContents(Stream tarStream, string expectedFilesDir)
        {
            TarOptions options = new() { Mode = TarArchiveMode.Read };
            using var archive = new TarArchive(tarStream, options);

            var extractedEntries = new List<TarArchiveEntry>();
            TarArchiveEntry? entry = null;
            while ((entry = archive.GetNextEntry()) != null)
            {
                extractedEntries.Add(entry);
            }

            Assert.NotEqual(TarFormat.Unknown, archive.Format);

            foreach (TarArchiveEntry extractedEntry in extractedEntries)
            {
                VerifyEntry(extractedEntry, archive.Format, expectedFilesDir);
            }

            // The 'devices' test case does not have any files in its 'unarchived' folder
            //  because character and block device files cannot be merged to git.
            if (Path.GetFileName(expectedFilesDir) != TestCaseSpecialFiles)
            {
                int expectedEntriesCount = GetExpectedEntriesCount(expectedFilesDir);
                Assert.Equal(expectedEntriesCount, extractedEntries.Count());
            }
            // TODO: Add verification of total number of entries for 'devices' (there are more in pax)
        }

        private void VerifyEntry(TarArchiveEntry entry, TarFormat format, string expectedFilesDir)
        {
            Assert.NotEmpty(entry.Name);
            string fullPath = Path.GetFullPath(Path.Join(expectedFilesDir, entry.Name));
            string? linkTargetFullPath = !string.IsNullOrEmpty(entry.LinkName) ? Path.GetFullPath(Path.Join(expectedFilesDir, entry.LinkName)) : null;

            VerifyEntryPermissions(format, entry);

            switch (entry.TypeFlag)
            {
                case TarArchiveEntryType.OldNormal:
                case TarArchiveEntryType.Normal:
                    Assert.True(File.Exists(fullPath), $"File does not exist: {fullPath}");
                    break;

                case TarArchiveEntryType.Link:
                    VerifyHardLinkEntry(entry, fullPath, linkTargetFullPath);
                    break;

                case TarArchiveEntryType.Directory:
                    Assert.True(Directory.Exists(fullPath), $"Directory does not exist: {fullPath}");
                    break;

                case TarArchiveEntryType.SymbolicLink:
                    Assert.NotEmpty(entry.LinkName);
                    VerifySymbolicLinkEntry(link: fullPath, target: linkTargetFullPath);
                    break;

                case TarArchiveEntryType.Block:
                    Assert.Equal(BlockDevName, entry.Name);
                    Assert.Equal(BlockDevMajor, entry.DevMajor);
                    Assert.Equal(BlockDevMinor, entry.DevMinor);
                    break;

                case TarArchiveEntryType.Character:
                    Assert.Equal(CharDevName, entry.Name);
                    Assert.Equal(CharDevMajor, entry.DevMajor);
                    Assert.Equal(CharDevMinor, entry.DevMinor);
                    break;

                case TarArchiveEntryType.Fifo:
                    Assert.Equal(FifoName, entry.Name);
                    break;

                // Extended attributes entries should not reach the user
                case (TarArchiveEntryType)'x':
                // Global extended attribute entries are extremely rare, the 'tar' command does not generate them
                case (TarArchiveEntryType)'g':
                default:
                    throw new NotSupportedException($"Unexpected entry type: {entry.TypeFlag}");
            }
        }

        private void VerifyEntryPermissions(TarFormat format, TarArchiveEntry entry)
        {
            // Skip checking mode of a symbolic link. From 'man chmod':
            // "chmod never changes the permissions of symbolic links; the chmod system call cannot change their
            // permissions. This is not a problem since the permissions of symbolic links are never used. However,
            // for each symbolic link listed on the command line, chmod changes the permissions of the pointed-to
            // file. In contrast, chmod ignores symbolic links encountered during recursive directory traversals."
            if (entry.TypeFlag is not TarArchiveEntryType.SymbolicLink)
            {
                Assert.Equal(TestMode, entry.Mode);
            }

            Assert.Equal(TestUid, entry.Uid);
            Assert.Equal(TestGid, entry.Gid);

            switch (format)
            {
                case TarFormat.V7:
                    // Fields aren't supported in this format
                    Assert.Null(entry.UName);
                    Assert.Null(entry.GName);
                    break;

                default:
                    Assert.Equal(TestUser, entry.UName);
                    Assert.Equal(TestGroup, entry.GName);
                    break;
            }
        }

        private void VerifyHardLinkEntry(TarArchiveEntry entry, string link, string target)
        {
            Assert.NotNull(entry.LinkName);
            Assert.NotEmpty(entry.LinkName);

            Assert.True(File.Exists(link), $"File hardlink does not exist: {link}");
            Assert.True(File.Exists(target), $"File hardlink target does not exist: {target}");
        }

        private void VerifySymbolicLinkEntry(string link, string target)
        {
            FileAttributes attributes = File.GetAttributes(link);
            Assert.True(attributes.HasFlag(FileAttributes.ReparsePoint), $"File is not a symlink: {link}");

            FileSystemInfo linkInfo;
            if (attributes.HasFlag(FileAttributes.Directory))
            {
                Assert.True(Directory.Exists(link), $"Directory symlink does not exist: {link}");
                linkInfo = new DirectoryInfo(link);
            }
            else
            {
                Assert.True(File.Exists(link), $"File symlink does not exist: {link}");
                linkInfo = new FileInfo(link);
            }

            Assert.True(linkInfo.Exists, $"Symlink does not exist: {linkInfo.FullName}");
            var targetInfo = linkInfo.ResolveLinkTarget(returnFinalTarget: true);
            Assert.NotNull(targetInfo);
            Assert.True(targetInfo.Exists, $"Symlink target does not exist: {targetInfo.FullName}");
            Assert.Equal(targetInfo.FullName, Path.GetFullPath(target));
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
