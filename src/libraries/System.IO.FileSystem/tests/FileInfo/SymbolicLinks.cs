// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace System.IO.Tests
{
    public class FileInfo_SymbolicLinks : BaseSymbolicLinks
    {
        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink()
        {
            string targetPath = Path.Combine(TestDirectory, GetTestFileName());
            File.Create(targetPath).Dispose();

            string linkPath = Path.Combine(TestDirectory, GetTestFileName());
            FileInfo linkInfo = new FileInfo(linkPath);

            linkInfo.CreateAsSymbolicLink(targetPath);

            Assert.True(linkInfo.Exists);
            Assert.True(linkInfo.Attributes.HasFlag(FileAttributes.ReparsePoint));

            var target = linkInfo.ResolveLinkTarget();

            Assert.True(target is FileInfo);
            Assert.True(target.Exists);
            Assert.Equal(targetPath, target.FullName);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_NonExistentTarget()
        {
            string nonExistentTargetPath = Path.Combine(TestDirectory, GetTestFileName());

            string linkPath = Path.Combine(TestDirectory, GetTestFileName());
            FileInfo linkInfo = new FileInfo(linkPath);

            linkInfo.CreateAsSymbolicLink(nonExistentTargetPath);

            Assert.True(linkInfo.Exists);
            Assert.True(linkInfo.Attributes.HasFlag(FileAttributes.ReparsePoint));

            var target = linkInfo.ResolveLinkTarget();

            Assert.True(target is FileInfo);
            Assert.False(target.Exists);
            Assert.Equal(nonExistentTargetPath, target.FullName);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void CreateSymbolicLink_WrongTargetType()
        {
            string targetPath = Path.Combine(TestDirectory, GetTestFileName());
            Directory.CreateDirectory(targetPath);

            string linkPath = Path.Combine(TestDirectory, GetTestFileName());
            FileInfo linkInfo = new FileInfo(linkPath);

            Assert.Throws<Exception>(() => linkInfo.CreateAsSymbolicLink(targetPath));
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void GetTargetInfo_NonExistentLink()
        {
            string linkPath = Path.Combine(TestDirectory, GetTestFileName());
            FileInfo linkInfo = new FileInfo(linkPath);
            Assert.Null(linkInfo.ResolveLinkTarget());
        }

        //[ConditionalFact(nameof(CanCreateSymbolicLinks))]
        //public void Test()
        //{
        //    while (!System.Diagnostics.Debugger.IsAttached)
        //    {
        //        Console.WriteLine($"Attach to {System.Environment.ProcessId}");
        //        System.Threading.Thread.Sleep(1000);
        //    }
        //    Console.WriteLine("Attached!");
        //    System.Diagnostics.Debugger.Break();

        //    var root = new DirectoryInfo(@"D:\symlinks");
        //    foreach (FileSystemInfo item in root.EnumerateFileSystemInfos())
        //    {
        //        Console.WriteLine($"{item.FullName} {item.ResolveLinkTarget()?.LinkTarget}");
        //    }
        //}
    }
}
