// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace System.IO.Tests
{
    public class Directory_Changed_Tests : FileSystemWatcherTest
    {
        [Fact]
        public void FileSystemWatcher_Directory_Changed_LastWrite()
        {
            using var testDirectory = new TempDirectory(GetTestFilePath());
            using var dir = new TempDirectory(Path.Combine(testDirectory.Path, "dir"));
            using var watcher = new FileSystemWatcher(testDirectory.Path, Path.GetFileName(dir.Path));
            Action action = () => Directory.SetLastWriteTime(dir.Path, DateTime.Now + TimeSpan.FromSeconds(10));

            WatcherChangeTypes expected = WatcherChangeTypes.Changed;
            ExpectEvent(watcher, expected, action, expectedPath: dir.Path);
        }

        [Fact]
        public void FileSystemWatcher_Directory_Changed_WatchedFolder()
        {
            using var testDirectory = new TempDirectory(GetTestFilePath());
            using var dir = new TempDirectory(Path.Combine(testDirectory.Path, "dir"));
            using var watcher = new FileSystemWatcher(dir.Path, "*");
            Action action = () => Directory.SetLastWriteTime(dir.Path, DateTime.Now + TimeSpan.FromSeconds(10));

            ExpectEvent(watcher, 0, action, expectedPath: dir.Path);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FileSystemWatcher_Directory_Changed_Nested(bool includeSubdirectories)
        {
            using var dir = new TempDirectory(GetTestFilePath());
            using var firstDir = new TempDirectory(Path.Combine(dir.Path, "dir1"));
            using var nestedDir = new TempDirectory(Path.Combine(firstDir.Path, "nested"));
            using var watcher = new FileSystemWatcher(dir.Path, "*")
            {
                IncludeSubdirectories = includeSubdirectories,
                NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.Attributes
            };

            var attributes = File.GetAttributes(nestedDir.Path);
            Action action = () => File.SetAttributes(nestedDir.Path, attributes | FileAttributes.ReadOnly);
            Action cleanup = () => File.SetAttributes(nestedDir.Path, attributes);

            WatcherChangeTypes expected = includeSubdirectories ? WatcherChangeTypes.Changed : 0;
            ExpectEvent(watcher, expected, action, cleanup, nestedDir.Path);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void FileSystemWatcher_Directory_Changed_SymLink()
        {
            using var testDirectory = new TempDirectory(GetTestFilePath());
            using var dir = new TempDirectory(Path.Combine(testDirectory.Path, "dir"));
            using var tempDir = new TempDirectory(Path.Combine(testDirectory.Path, "tempDir"));
            using var file = new TempFile(Path.Combine(tempDir.Path, "test"));
            using var watcher = new FileSystemWatcher(dir.Path, "*")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size,
                IncludeSubdirectories = true
            };
            Assert.True(CreateSymLink(tempDir.Path, Path.Combine(dir.Path, "link"), true));

            Action action = () => File.AppendAllText(file.Path, "longtext");
            Action cleanup = () => File.AppendAllText(file.Path, "short");

            ExpectEvent(watcher, 0, action, cleanup, dir.Path);
        }

        [ConditionalFact(nameof(CanCreateSymbolicLinks))]
        public void FileSystemWatcher_DirectorySymLink_Changed()
        {
            using var root = new TempDirectory(GetTestFilePath());
            using var dir = new TempDirectory(Path.Combine(root.Path, "dir"));

            string filePath = Path.Combine(dir.Path, "testFile.txt");
            File.AppendAllText(filePath, "Hello world");

            string dirSymLinkPath = Path.Combine(root.Path, "dirSymLink");
            Assert.True(CreateSymLink(dir.Path, dirSymLinkPath, true));

            using var watcher = new FileSystemWatcher(dirSymLinkPath, "*");
            watcher.NotifyFilter |= NotifyFilters.FollowSymlinks | NotifyFilters.LastWrite | NotifyFilters.Size;

            Action action = () =>
            {
                File.AppendAllText(filePath, "Appended text");
                
            };

            ExpectEvent(watcher,
                WatcherChangeTypes.Changed | WatcherChangeTypes.Deleted,
                action,
                expectedPath: dirSymLinkPath);
        }
    }
}
