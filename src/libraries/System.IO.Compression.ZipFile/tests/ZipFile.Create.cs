// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace System.IO.Compression.Tests
{
    public class ZipFile_Create : ZipFileTestBase
    {
        [Fact]
        public async Task Async()
        {
            string folderName = zfolder("normal");
            string noBaseDir = GetTestFilePath();
            await ZipFile.CreateFromDirectoryAsync(folderName, noBaseDir, CancellationToken.None);

            await IsZipSameAsDirAsyncAsync(noBaseDir, folderName, ZipArchiveMode.Read, requireExplicit: false, checkTimes: false, CancellationToken.None);
        }

        [Fact]
        public async Task CreateFromDirectoryNormal()
        {
            string folderName = zfolder("normal");
            string noBaseDir = GetTestFilePath();
            ZipFile.CreateFromDirectory(folderName, noBaseDir);

            await IsZipSameAsDirAsync(noBaseDir, folderName, ZipArchiveMode.Read, requireExplicit: false, checkTimes: false);
        }

        [Fact]
        public void CreateFromDirectory_IncludeBaseDirectory()
        {
            string folderName = zfolder("normal");
            string withBaseDir = GetTestFilePath();
            ZipFile.CreateFromDirectory(folderName, withBaseDir, CompressionLevel.Optimal, true);

            IEnumerable<string> expected = Directory.EnumerateFiles(zfolder("normal"), "*", SearchOption.AllDirectories);
            using (ZipArchive actual_withbasedir = ZipFile.Open(withBaseDir, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry actualEntry in actual_withbasedir.Entries)
                {
                    string expectedFile = expected.Single(i => Path.GetFileName(i).Equals(actualEntry.Name));
                    Assert.StartsWith("normal", actualEntry.FullName);
                    Assert.Equal(new FileInfo(expectedFile).Length, actualEntry.Length);
                    using (Stream expectedStream = File.OpenRead(expectedFile))
                    using (Stream actualStream = actualEntry.Open())
                    {
                        StreamsEqual(expectedStream, actualStream);
                    }
                }
            }
        }

        [Fact]
        public async Task CreateFromDirectory_IncludeBaseDirectoryAsync()
        {
            string folderName = zfolder("normal");
            string withBaseDir = GetTestFilePath();
            ZipFile.CreateFromDirectory(folderName, withBaseDir, CompressionLevel.Optimal, true);

            IEnumerable<string> expected = Directory.EnumerateFiles(zfolder("normal"), "*", SearchOption.AllDirectories);
            using (ZipArchive actual_withbasedir = ZipFile.Open(withBaseDir, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry actualEntry in actual_withbasedir.Entries)
                {
                    string expectedFile = expected.Single(i => Path.GetFileName(i).Equals(actualEntry.Name));
                    Assert.StartsWith("normal", actualEntry.FullName);
                    Assert.Equal(new FileInfo(expectedFile).Length, actualEntry.Length);
                    using (Stream expectedStream = File.OpenRead(expectedFile))
                    using (Stream actualStream = actualEntry.Open())
                    {
                        await StreamsEqualAsync(expectedStream, actualStream);
                    }
                }
            }
        }

        [Fact]
        public void CreateFromDirectoryUnicode()
        {
            string folderName = zfolder("unicode");
            string noBaseDir = GetTestFilePath();
            ZipFile.CreateFromDirectory(folderName, noBaseDir);

            using (ZipArchive archive = ZipFile.OpenRead(noBaseDir))
            {
                IEnumerable<string> actual = archive.Entries.Select(entry => entry.Name);
                IEnumerable<string> expected = Directory.EnumerateFileSystemEntries(zfolder("unicode"), "*", SearchOption.AllDirectories).ToList();
                Assert.True(Enumerable.SequenceEqual(expected.Select(i => Path.GetFileName(i)), actual.Select(i => i)));
            }
        }

        [Fact]
        public void CreatedEmptyDirectoriesRoundtrip()
        {
            using (var tempFolder = new TempDirectory(GetTestFilePath()))
            {
                DirectoryInfo rootDir = new DirectoryInfo(tempFolder.Path);
                rootDir.CreateSubdirectory("empty1");

                string archivePath = GetTestFilePath();
                ZipFile.CreateFromDirectory(
                    rootDir.FullName, archivePath,
                    CompressionLevel.Optimal, false, Encoding.UTF8);

                using (ZipArchive archive = ZipFile.OpenRead(archivePath))
                {
                    Assert.Equal(1, archive.Entries.Count);
                    Assert.StartsWith("empty1", archive.Entries[0].FullName);
                }
            }
        }

        [Fact]
        public void CreatedEmptyUtf32DirectoriesRoundtrip()
        {
            using (var tempFolder = new TempDirectory(GetTestFilePath()))
            {
                Encoding entryEncoding = Encoding.UTF32;
                DirectoryInfo rootDir = new DirectoryInfo(tempFolder.Path);
                rootDir.CreateSubdirectory("empty1");

                string archivePath = GetTestFilePath();
                ZipFile.CreateFromDirectory(
                    rootDir.FullName, archivePath,
                    CompressionLevel.Optimal, false, entryEncoding);

                using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Read, entryEncoding))
                {
                    Assert.Equal(1, archive.Entries.Count);
                    Assert.StartsWith("empty1", archive.Entries[0].FullName);
                }
            }
        }

        [Fact]
        public void CreatedEmptyRootDirectoryRoundtrips()
        {
            using (var tempFolder = new TempDirectory(GetTestFilePath()))
            {
                DirectoryInfo emptyRoot = new DirectoryInfo(tempFolder.Path);
                string archivePath = GetTestFilePath();
                ZipFile.CreateFromDirectory(
                    emptyRoot.FullName, archivePath,
                    CompressionLevel.Optimal, true);

                using (ZipArchive archive = ZipFile.OpenRead(archivePath))
                {
                    Assert.Equal(1, archive.Entries.Count);
                }
            }
        }

        [Fact]
        public void CreateSetsExternalAttributesCorrectly()
        {
            string folderName = zfolder("normal");
            string filepath = GetTestFilePath();
            ZipFile.CreateFromDirectory(folderName, filepath);

            using (ZipArchive archive = ZipFile.Open(filepath, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (OperatingSystem.IsWindows())
                    {
                        Assert.Equal(0, entry.ExternalAttributes);
                    }
                    else
                    {
                        Assert.NotEqual(0, entry.ExternalAttributes);
                    }
                }
            }
        }
    }

    public class ZipFile_Create_Async : ZipFileTestBase
    {
        [Fact]
        public async Task CreateFromDirectoryNormal()
        {
            string folderName = zfolder("normal");
            string directory = GetTestFilePath();
            CancellationToken ct = CancellationToken.None;
            await ZipFile.CreateFromDirectoryAsync(folderName, directory, ct);
            var mode = ZipArchiveMode.Read;
            bool checkTimes = false;
            bool requireExplicit = false;

            var archiveFile = await StreamHelpers.CreateTempCopyStream(folderName);
            int count = 0;

            await using (ZipArchive archive = new ZipArchive(archiveFile, mode))
            {
                List<FileData> files = FileData.InPath(directory);
                Assert.All<FileData>(files, async (file) => {
                    count++;
                    string entryName = file.FullName;
                    if (file.IsFolder)
                        entryName += Path.DirectorySeparatorChar;
                    ZipArchiveEntry entry = await archive.GetEntryAsync(entryName, ct);
                    if (entry == null)
                    {
                        entryName = FlipSlashes(entryName);
                        entry = await archive.GetEntryAsync(entryName, ct);
                    }
                    if (file.IsFile)
                    {
                        Assert.NotNull(entry);
                        long givenLength = entry.Length;

                        var buffer = new byte[entry.Length];
                        await using (Stream entrystream = await entry.OpenAsync(ct))
                        {
                            ReadAllBytes(entrystream, buffer, 0, buffer.Length);
#if NET
                            uint zipcrc = entry.Crc32;
                            Assert.Equal(CRC.CalculateCRC(buffer), zipcrc);
#endif

                            if (file.Length != givenLength)
                            {
                                buffer = NormalizeLineEndings(buffer);
                            }

                            Assert.Equal(file.Length, buffer.Length);
                            ulong crc = CRC.CalculateCRC(buffer);
                            Assert.Equal(file.CRC, crc.ToString());
                        }

                        if (checkTimes)
                        {
                            const int zipTimestampResolution = 2; // Zip follows the FAT timestamp resolution of two seconds for file records
                            DateTime lower = file.LastModifiedDate.AddSeconds(-zipTimestampResolution);
                            DateTime upper = file.LastModifiedDate.AddSeconds(zipTimestampResolution);
                            Assert.InRange(entry.LastWriteTime.Ticks, lower.Ticks, upper.Ticks);
                        }

                        Assert.Equal(file.Name, entry.Name);
                        Assert.Equal(entryName, entry.FullName);
                        Assert.Equal(entryName, entry.ToString());
                        Assert.Equal(archive, entry.Archive);
                    }
                    else if (file.IsFolder)
                    {
                        if (entry == null) //entry not found
                        {
                            string entryNameOtherSlash = FlipSlashes(entryName);
                            bool isEmpty = !files.Any(
                                f => f.IsFile &&
                                     (f.FullName.StartsWith(entryName, StringComparison.OrdinalIgnoreCase) ||
                                      f.FullName.StartsWith(entryNameOtherSlash, StringComparison.OrdinalIgnoreCase)));
                            if (requireExplicit || isEmpty)
                            {
                                Assert.Contains("emptydir", entryName);
                            }

                            if ((!requireExplicit && !isEmpty) || entryName.Contains("emptydir"))
                                count--; //discount this entry
                        }
                        else
                        {
                            await using (Stream es = await entry.OpenAsync(ct))
                            {
                                try
                                {
                                    Assert.Equal(0, es.Length);
                                }
                                catch (NotSupportedException)
                                {
                                    try
                                    {
                                        Assert.Equal(-1, es.ReadByte());
                                    }
                                    catch (Exception)
                                    {
                                        Console.WriteLine("Didn't return EOF");
                                        throw;
                                    }
                                }
                            }
                        }
                    }
                });
                Assert.Equal(count, archive.Entries.Count);
            }
        }
    }
}
