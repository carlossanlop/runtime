using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Xunit;

namespace MyBenchmarks;

/*
File: filename.txt
| X/Y                   | NoCompression | Fastest | Optimal | SmallestSize |
|-----------------------|---------------|---------|---------|--------------|
| Windows 64 zlib       |               |         |         |              |
| Windows 64 zlib-ng    |               |         |         |              |
| Linux 64 zlib         |               |         |         |              |
| Linux 64 zlib-ng      |               |         |         |              |
| Mac 64 zlib           |               |         |         |              |
| Mac 64 zlib-ng        |               |         |         |              |
| Windows arm64 zlib    |               |         |         |              |
| Windows arm64 zlib-ng |               |         |         |              |
| Linux arm64 zlib      |               |         |         |              |
| Linux arm64 zlib-ng   |               |         |         |              |
| Mac arm64 zlib        |               |         |         |              |
| Mac arm64 zlib-ng     |               |         |         |              |
*/

public class MyClass
{
    [Fact]
    public void MyTest()
    {
        // CHANGE THESE IN EACH MACHINE
        string os = "Windows 11";
        string arch = "x64";
        string version = "zlib";
        string repoPath = "D:/zlib-ng-corpora/";
        // dotnet build -c Release /t:test .\System.IO.Compression.Tests.csproj /p:XUnitMethodName=MyBenchmarks.MyClass.MyTest > "C:\Users\calope\OneDrive - Microsoft\Attachments\win-x64-zlib.md"

        CompressionLevel[] levels = [CompressionLevel.NoCompression, CompressionLevel.Fastest, CompressionLevel.Optimal, CompressionLevel.SmallestSize];

        Console.WriteLine();
        Console.Write("| OS | Arch | Version | FileName");
        foreach (CompressionLevel level in levels)
        {
            Console.Write($" | {level} (len/ms)");
        }
        Console.WriteLine(" |");

        Console.WriteLine("|-|-|-|-|-|-|-|-|");

        foreach (string filePath in GetFilesToCompress(repoPath))
        {
            string folderName = Path.GetFileName(Path.GetDirectoryName(filePath));
            string fileName = Path.GetFileName(filePath);

            Console.Write( $"| {os}");
            Console.Write($" | {arch}");
            Console.Write($" | {version}");
            Console.Write($" | {folderName}/{fileName}");

            foreach (CompressionLevel level in levels)
            {
                (long length, double ms) = GetZipLengthAndMilliseconds(filePath, level);
                Console.Write($" | {length} bytes / {ms} ms");
            }
            Console.WriteLine(" |");
        }
        Console.WriteLine();
    }

    private (long, double) GetZipLengthAndMilliseconds(string filePath, CompressionLevel level)
    {
        string sourceDirectoryName = GetDirectoryToCompress(filePath);
        string destinationArchiveFileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()  + ".zip");
        FileInfo zipFileInfo = new FileInfo(destinationArchiveFileName);

        // BENCHMARK START
        DateTime start = DateTime.Now;
        ZipFile.CreateFromDirectory(sourceDirectoryName, destinationArchiveFileName, level, true);
        DateTime end = DateTime.Now;
        // BENCHMARK END

        long length = zipFileInfo.Length;
        double ms = (end - start).TotalMilliseconds;

        // CLEANUP
        Directory.Delete(sourceDirectoryName, true);
        File.Delete(destinationArchiveFileName);

        return (length, ms);
    }

    private string GetDirectoryToCompress(string filePath)
    {
        string tempRootDir = Path.GetTempPath();
        string tempDir = Path.Combine(tempRootDir, Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        string destFileName = Path.Combine(tempDir, Path.GetFileName(filePath));
        File.Copy(filePath, destFileName);
        return tempDir;
    }

    private IEnumerable<string> GetFilesToCompress(string repoPath)
    {
        foreach (string dirName in Directory.GetDirectories(repoPath))
        {
            if (dirName.Contains(".git"))
            {
                continue;
            }

            foreach (string fileName in Directory.GetFiles(dirName))
            {
                yield return fileName;
            }
        }
    }
}
