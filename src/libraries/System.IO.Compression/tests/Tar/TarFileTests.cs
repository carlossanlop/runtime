// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace System.IO.Compression.Tests
{
    public partial class TarFileTests : TarTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Invalid_Path(string path)
        {
            Assert.Throws<ArgumentNullException>(() => TarFile.OpenRead(archiveFileName: path));
            Assert.Throws<ArgumentNullException>(() => TarFile.Open(archiveFileName: path, TarArchiveMode.Read));
        }

        [Theory]
        [InlineData((TarArchiveMode)(-1))]
        [InlineData((TarArchiveMode)int.MinValue)]
        [InlineData((TarArchiveMode)int.MaxValue)]
        public void Invalid_TarArchiveMode(TarArchiveMode mode) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => TarFile.Open("file.tar", mode));

        [Fact]
        public void NonExistentFile()
        {
            Assert.Throws<FileNotFoundException>(() => TarFile.OpenRead("idonotexist.tar"));
            Assert.Throws<FileNotFoundException>(() => TarFile.Open("idonotexist.tar", TarArchiveMode.Read));
        }


        [Fact]
        public void Not_A_Tar_File()
        {
            string zipFilePath = Path.Combine(Directory.GetCurrentDirectory(), "ZipTestData", "refzipfiles", "normal.zip");

            using var archiveOpenRead = TarFile.OpenRead(zipFilePath);
            Assert.Throws<FormatException>(() => archiveOpenRead.GetNextEntry());

            using var archiveOpen = TarFile.Open(zipFilePath, TarArchiveMode.Read);
            Assert.Throws<FormatException>(() => archiveOpen.GetNextEntry());
        }
    }
}