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
        public void Null_Stream()
            => Assert.Throws<ArgumentNullException>(() => new TarArchive(stream: null, new TarArchiveOptions()));

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
        public void Invalid_TarArchiveMode(TarArchiveMode mode)
            => Assert.Throws<ArgumentOutOfRangeException>(() => new TarArchive(new MemoryStream(), new TarArchiveOptions() { Mode = mode }));

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
