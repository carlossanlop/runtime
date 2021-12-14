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

        private const string TestGlobalExtendedAttributeKey = "globexthdr.MyGlobalExtendedAttribute";
        private const string TestGlobalExtendedAttributeValue = "hello";

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

        #region V7

        [Theory]
        [MemberData(nameof(Normal_FilesAndFolders_V7_Data))]
        public void Read_Uncompressed_V7_NormalFilesAndFolders(string testCaseName) =>
            VerifyTarFileContents(CompressionMethod.Uncompressed, TestTarFormat.v7, testCaseName);

        [Theory]
        [MemberData(nameof(Normal_FilesAndFolders_V7_Data))]
        public void Read_Gzip_V7_NormalFilesAndFolders(string testCaseName) =>
            VerifyTarFileContents(CompressionMethod.GZip, TestTarFormat.v7, testCaseName);

        #endregion

        #region Ustar

        [Theory]
        [MemberData(nameof(Normal_FilesAndFolders_Ustar_Data))]
        public void Read_Uncompressed_Ustar_NormalFilesAndFolders(string testCaseName) =>
            VerifyTarFileContents(CompressionMethod.Uncompressed, TestTarFormat.ustar, testCaseName);

        [Theory]
        [MemberData(nameof(Normal_FilesAndFolders_Ustar_Data))]
        public void Read_Gzip_Ustar_NormalFilesAndFolders(string testCaseName) =>
            VerifyTarFileContents(CompressionMethod.GZip, TestTarFormat.ustar, testCaseName);

        #endregion

        #region Pax

        [Theory]
        [MemberData(nameof(Normal_FilesAndFolders_PaxAndGnu_Data))]
        public void Read_Uncompressed_Pax_NormalFilesAndFolders(string testCaseName) =>
            VerifyTarFileContents(CompressionMethod.Uncompressed, TestTarFormat.pax, testCaseName);

        [Theory]
        [MemberData(nameof(Normal_FilesAndFolders_PaxAndGnu_Data))]
        public void Read_Gzip_Pax_NormalFilesAndFolders(string testCaseName) =>
            VerifyTarFileContents(CompressionMethod.GZip, TestTarFormat.pax, testCaseName);

        #endregion

        #region Pax with Global Extended Attributes

        [Theory]
        [MemberData(nameof(Normal_FilesAndFolders_PaxAndGnu_Data))]
        public void Read_Uncompressed_PaxGEA_NormalFilesAndFolders(string testCaseName) =>
            VerifyTarFileContents(CompressionMethod.Uncompressed, TestTarFormat.pax_gea, testCaseName);

        [Theory]
        [MemberData(nameof(Normal_FilesAndFolders_PaxAndGnu_Data))]
        public void Read_Gzip_PaxGEA_NormalFilesAndFolders(string testCaseName) =>
            VerifyTarFileContents(CompressionMethod.GZip, TestTarFormat.pax_gea, testCaseName);

        #endregion

        #region Gnu

        [Theory]
        [MemberData(nameof(Normal_FilesAndFolders_PaxAndGnu_Data))]
        public void Read_Uncompressed_Gnu_NormalFilesAndFolders(string testCaseName) =>
            VerifyTarFileContents(CompressionMethod.Uncompressed, TestTarFormat.gnu, testCaseName);

        [Theory]
        [MemberData(nameof(Normal_FilesAndFolders_PaxAndGnu_Data))]
        public void Read_Gzip_Gnu_NormalFilesAndFolders(string testCaseName) =>
            VerifyTarFileContents(CompressionMethod.GZip, TestTarFormat.gnu, testCaseName);

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

        protected enum TestTarFormat
        {
            v7,
            ustar,
            pax,
            pax_gea,
            gnu
        }

        protected static string GetTarFile(CompressionMethod compressionMethod, TestTarFormat format, string testCaseName)
        {
            (string compressionMethodFolder, string fileExtension) = compressionMethod switch
            {
                CompressionMethod.Uncompressed => ("tar", ".tar"),
                CompressionMethod.GZip => ("targz", ".tar.gz"),
                _ => throw new NotSupportedException(),
            };

            return Path.Join(Directory.GetCurrentDirectory(), "TarTestData", compressionMethodFolder, format.ToString(), testCaseName + fileExtension);
        }

        private static string GetTestCaseFolderPath(string testCaseName) =>
            Path.Join(Directory.GetCurrentDirectory(), "TarTestData", "unarchived", testCaseName);

        protected void CompareTarFileContentsWithDirectoryContents(CompressionMethod compressionMethod, TestTarFormat format, string tarFilePath, string expectedFilesDir)
        {
            using FileStream fs = File.Open(tarFilePath, FileMode.Open);

            switch (compressionMethod)
            {
                case CompressionMethod.Uncompressed:
                    VerifyUncompressedTarStreamContents(fs, format, expectedFilesDir);
                    break;

                case CompressionMethod.GZip:
                    using (var decompressor = new GZipStream(fs, CompressionMode.Decompress))
                    {
                        VerifyUncompressedTarStreamContents(decompressor, format, expectedFilesDir);
                    }
                    break;

                default:
                    throw new NotSupportedException();
            }
        }

        private void VerifyTarFileContents(CompressionMethod compressionMethod, TestTarFormat format, string testCaseName)
        {
            string tarFilePath = GetTarFile(compressionMethod, format, testCaseName);
            string expectedFilesDir = GetTestCaseFolderPath(testCaseName);

            CompareTarFileContentsWithDirectoryContents(compressionMethod, format, tarFilePath, expectedFilesDir);
        }

        // Reads the contents of a stream wrapping an uncompressed tar archive, then compares
        // the entries with the filesystem entries found in the specified folder path.
        private void VerifyUncompressedTarStreamContents(Stream tarStream, TestTarFormat format, string expectedFilesDir)
        {
            TarOptions options = new() { Mode = TarArchiveMode.Read };
            using var archive = new TarArchive(tarStream, options);

            var extractedEntries = new List<TarArchiveEntry>();
            TarArchiveEntry? entry = null;
            while ((entry = archive.GetNextEntry()) != null)
            {
                extractedEntries.Add(entry);
            }

            foreach (TarArchiveEntry extractedEntry in extractedEntries)
            {
                VerifyEntry(extractedEntry, format, expectedFilesDir);
            }

            // The 'special files' test case does not have any files in its 'unarchived' folder
            // because character devices, block devices and fifo files cannot be merged to git.
            // Hence why for this test case the number is hardcoded: we expect one of each special file.
            int expectedEntriesCount = Path.GetFileName(expectedFilesDir) == TestCaseSpecialFiles ? 3 : GetExpectedEntriesCount(expectedFilesDir);
            Assert.Equal(expectedEntriesCount, extractedEntries.Count());
        }

        private void VerifyEntry(TarArchiveEntry entry, TestTarFormat format, string expectedFilesDir)
        {
            Assert.NotEmpty(entry.Name);
            string fullPath = Path.GetFullPath(Path.Join(expectedFilesDir, entry.Name));
            string? linkTargetFullPath = !string.IsNullOrEmpty(entry.LinkName) ? Path.GetFullPath(Path.Join(expectedFilesDir, entry.LinkName)) : null;

            if (format == TestTarFormat.pax_gea)
            {
                VerifyPaxGlobalExtendedAttributes(entry);
            }

            VerifyEntryOwnershipAndPermissions(format, entry);

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

                default:
                    throw new NotSupportedException($"Unexpected entry type: {entry.TypeFlag}");
            }
        }

        private void VerifyPaxGlobalExtendedAttributes(TarArchiveEntry entry)
        {
            Assert.NotNull(entry.ExtendedAttributes);
            Assert.True(entry.ExtendedAttributes.ContainsKey(TestGlobalExtendedAttributeKey));
            Assert.Equal(TestGlobalExtendedAttributeValue, entry.ExtendedAttributes[TestGlobalExtendedAttributeKey]);
        }

        private void VerifyEntryOwnershipAndPermissions(TestTarFormat format, TarArchiveEntry entry)
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
                case TestTarFormat.v7:
                    // GName and Uname aren't supported in this format.
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
