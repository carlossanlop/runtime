// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO.Enumeration;
using System.Linq;
using Xunit;

namespace System.IO.Compression.Tests
{
    public partial class TarArchiveTests : TarTests
    {
        #region Basic validation

        [Fact]
        public void Null_Stream() =>
            Assert.Throws<ArgumentNullException>(() => new TarArchive(stream: null, new TarArchiveOptions()));

        [Fact]
        public void Null_Options()
        {
            using var archive = new TarArchive(new MemoryStream(), options: null);
            Assert.NotNull(archive.Options);
            Assert.False(archive.Options.LeaveOpen);
            Assert.Equal(TarArchiveMode.Read, archive.Options.Mode);
        }

        [Theory]
        [InlineData((TarArchiveMode)int.MinValue)]
        [InlineData((TarArchiveMode)(-1))]
        [InlineData((TarArchiveMode)int.MaxValue)]
        public void Invalid_TarArchiveMode(TarArchiveMode mode) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new TarArchive(new MemoryStream(), new TarArchiveOptions() { Mode = mode }));

        [Fact]
        public void Not_A_Tar_File()
        {
            string zipFilePath = Path.Combine(Directory.GetCurrentDirectory(), "ZipTestData", "refzipfiles", "normal.zip");

            using FileStream stream = File.Open(zipFilePath, FileMode.Open);

            TarArchiveOptions options = new() { Mode = TarArchiveMode.Read };
            using var archive = new TarArchive(stream, options);

            Assert.Throws<FormatException>(() => archive.GetNextEntry());

            /*
            TODO: This exception depends on the format of the file, and it's in different locations depending on what fails first.
            We probably have to throw consistent exception if the file does not have a valid tar format.
            If this behavior is changed, the TarFile.Open and TarFile.OpenRead methods need to be adjusted as well.
            Example: For this zip file, we throw when attempting to parse an octal.

            System.FormatException : Could not find any recognizable digits.
            Stack Trace:
            D:\runtime\src\libraries\System.Private.CoreLib\src\System\ParseNumbers.cs(182,0): at System.ParseNumbers.StringToInt(ReadOnlySpan`1 s, Int32 radix, Int32 flags, Int32& currPos)
            D:\runtime\src\libraries\System.Private.CoreLib\src\System\ParseNumbers.cs(120,0): at System.ParseNumbers.StringToInt(ReadOnlySpan`1 s, Int32 radix, Int32 flags)
            D:\runtime\src\libraries\System.Private.CoreLib\src\System\Convert.cs(2220,0): at System.Convert.ToInt32(String value, Int32 fromBase)
            D:\runtime\src\libraries\System.IO.Compression\src\System\IO\Compression\Tar\TarHeader.cs(615,0): at System.IO.Compression.Tar.TarHeader.GetTenBaseNumberFromOctalAsciiChars(Span`1 buffer)
            */
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Verify_LeaveOpen(bool leaveOpen)
        {
            var stream = new MemoryStream();
            using (var archive = new TarArchive(stream, new TarArchiveOptions() { LeaveOpen = leaveOpen })) { }

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
            var archive = new TarArchive(new MemoryStream(), new TarArchiveOptions() { LeaveOpen = false });

            archive.Dispose();
            archive.Dispose(); // A second dispose call should be a no-op

            Assert.Throws<ObjectDisposedException>(() => archive.GetNextEntry());
        }

        #endregion

        #region Format specific validation

        [Theory]
        [MemberData(nameof(V7_Data))]
        public void Read_V7_NormalFilesAndFolders(string testCaseName, CompressionMethod compressionMethod) =>
            VerifyTarFileContents(compressionMethod, TestTarFormat.v7, testCaseName);

        [Theory]
        [MemberData(nameof(Ustar_Data))]
        public void Read_Ustar_NormalFilesAndFolders(string testCaseName, CompressionMethod compressionMethod) =>
            VerifyTarFileContents(compressionMethod, TestTarFormat.ustar, testCaseName);

        [Theory]
        [MemberData(nameof(PaxAndGnu_Data))]
        public void Read_Pax_NormalFilesAndFolders(string testCaseName, CompressionMethod compressionMethod) =>
            VerifyTarFileContents(compressionMethod, TestTarFormat.pax, testCaseName);

        [Theory]
        [MemberData(nameof(PaxAndGnu_Data))]
        public void Read_PaxGEA_NormalFilesAndFolders(string testCaseName, CompressionMethod compressionMethod) =>
            VerifyTarFileContents(compressionMethod, TestTarFormat.pax_gea, testCaseName);

        [Theory]
        [MemberData(nameof(PaxAndGnu_Data))]
        public void Read_Gnu_NormalFilesAndFolders(string testCaseName, CompressionMethod compressionMethod) =>
            VerifyTarFileContents(compressionMethod, TestTarFormat.gnu, testCaseName);

        [Theory]
        [MemberData(nameof(PaxAndGnu_Data))]
        public void Read_OldGnu_NormalFilesAndFolders(string testCaseName, CompressionMethod compressionMethod) =>
            VerifyTarFileContents(compressionMethod, TestTarFormat.oldgnu, testCaseName);

        #endregion
    }
}
