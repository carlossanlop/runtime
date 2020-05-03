// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Xunit;

namespace System.IO.Tests
{
    [PlatformSpecific(TestPlatforms.AnyUnix)]
    public class RemoveRedundantSegmentsTests_Unix : RemoveRedundantSegmentsTests
    {
        #region TestData

        // Normal paths
        public static TheoryData<string, string> UnixNormalData => new TheoryData<string, string>
        {
            // AltDirectorySeparatorChar gets normalized to DirectorySeparatorChar, if they are not the same in the current platform
            { @"/",                @"/" },
            { @"/home",            @"/home" },
            { @"/home/",           @"/home/" },
            { @"/home/myuser",     @"/home/myuser" },
            { @"/home/myuser/",    @"/home/myuser/" },
        };

        // Paths with '..' to indicate the removal of the previous segment
        public static TheoryData<string, string> UnixValidParentBacktrackingData => new TheoryData<string, string>
        {
            { @"/home/..",                  @"/" },
            { @"/home/../",                 @"/" },
            { @"/home/../myuser",           @"/myuser" },
            { @"/home/../myuser/",          @"/myuser/" },
            { @"/home/myuser/..",           @"/home" },
            { @"/home/myuser/../",          @"/home/" },
            { @"/home/myuser/../..",        @"/" },
            { @"/home/myuser/../../",       @"/" },
            { @"/home/../myuser/..",        @"/" },
            { @"/home/../myuser/../",       @"/" },
            { @"/home/../myuser/../..",     @"/" },
            { @"/home/../myuser/../../",    @"/" },
        };

        // Paths with '.' to indicate current directory
        public static TheoryData<string, string> UnixValidCurrentDirectoryData => new TheoryData<string, string>
        {
            { @"/.",                    @"/" },
            { @"/./",                   @"/" },
            { @"/./.",                  @"/" },
            { @"/././",                 @"/" },
            { @"/./home",               @"/home" },
            { @"/./home/",              @"/home/" },
            { @"/././home",             @"/home" },
            { @"/././home/",            @"/home/" },
            { @"/home/.",               @"/home" },
            { @"/home/./",              @"/home/" },
            { @"/home/./.",             @"/home" },
            { @"/home/././",            @"/home/" },
            { @"/./home/myuser",        @"/home/myuser" },
            { @"/./home/myuser/",       @"/home/myuser/" },
            { @"/./home/./myuser",      @"/home/myuser" },
            { @"/./home/./myuser/",     @"/home/myuser/" },
            { @"/./home/./myuser/.",    @"/home/myuser" },
            { @"/./home/./myuser/./",   @"/home/myuser/" },
            { @"/home/././myuser/./.",  @"/home/myuser" },
            { @"/home/././myuser/././", @"/home/myuser/" },
        };

        // Combined '.' and '..'
        public static TheoryData<string, string> UnixCombinedRedundantData => new TheoryData<string, string>
        {
            { @"/home/./..",         @"/" },
            { @"/home/./../",        @"/" },
            { @"/home/../.",         @"/" },
            { @"/home/.././",        @"/" },
            { @"/./home/..",         @"/" },
            { @"/./home/../",        @"/" },
            { @"/./home/myuser/..",  @"/home" },
            { @"/./home/myuser/../", @"/home/" },
            { @"/./home/../myuser",  @"/myuser" },
            { @"/./home/../myuser/", @"/myuser/" },
        };

        // Duplicate separators
        public static TheoryData<string, string> UnixDuplicateSeparatorsData => new TheoryData<string, string>
        {
            { @"/home/",                   @"/home/" },
            { @"/home\\\",                 @"/home/" },
            { @"/home//",                  @"/home/" },
            { @"/home///",                 @"/home/" },
            { @"/home\/",                  @"/home/" },
            { @"/home/\",                  @"/home/" },
            { @"/home\/\",                 @"/home/" },
            { @"/home/\\",                 @"/home/" },
            { @"/home\//",                 @"/home/" },
            { @"/home/\/",                 @"/home/" },
            { @"/home\\myuser",            @"/home/myuser" },
            { @"/home\\\myuser",           @"/home/myuser" },
            { @"/home\\myuser\\\",         @"/home/myuser/" },
            { @"/home\\\myuser\\",         @"/home/myuser/" },
            { @"/home\\myuser\\/\",        @"/home/myuser/" },
            { @"/home\\\myuser\/\",        @"/home/myuser/" },
            { @"/home\/myuser",            @"/home/myuser" },
            { @"/home\/myuser/",           @"/home/myuser/" },
            { @"/home\/myuser/",           @"/home/myuser/" },
            { @"/home\\myuser\\.\",        @"/home/myuser/" },
            { @"/home\\\myuser\..\\",      @"/home/" },
            { @"/home\\myuser\\./.\",      @"/home/myuser/" },
            { @"/home\.\\myuser\/./\\",    @"/home/myuser/" },
            { @"/home\\.\myuser\/../\\./", @"/home/" },
        };
        
        #endregion

        #region Tests

        // The expected string is returned in Unix with '/' as separator.
        // The trailing '/' is considered the root, hence it's a fully qualified path.

        [Theory]
        [MemberData(nameof(UnixNormalData))]
        [MemberData(nameof(UnixValidParentBacktrackingData))]
        [MemberData(nameof(UnixValidCurrentDirectoryData))]
        [MemberData(nameof(UnixCombinedRedundantData))]
        [MemberData(nameof(UnixDuplicateSeparatorsData))]
        [PlatformSpecific(TestPlatforms.AnyUnix)]
        public static void UnixValid_String(string path, string expected)
        {
            if (!PlatformDetection.IsWindows)
            {
                expected = expected.Replace('\\', '/');
            }
            TestRedundantSegments(path, expected);
        }

        [Theory]
        [MemberData(nameof(UnixNormalData))]
        [MemberData(nameof(UnixValidParentBacktrackingData))]
        [MemberData(nameof(UnixValidCurrentDirectoryData))]
        [MemberData(nameof(UnixCombinedRedundantData))]
        [MemberData(nameof(UnixDuplicateSeparatorsData))]
        [PlatformSpecific(TestPlatforms.AnyUnix)]
        public static void UnixValid_Span(string path, string expected)
        {
            while (!Debugger.IsAttached)
            {
                Console.WriteLine("Attach to {0}", Process.GetCurrentProcess().Id);
                Threading.Thread.Sleep(1000);
            }
            Debugger.Break();

            if (!PlatformDetection.IsWindows)
            {
                expected = expected.Replace('\\', '/');
            }
            TestRedundantSegments(path.AsSpan(), expected);
        }

        [Theory]
        [MemberData(nameof(UnixNormalData))]
        [MemberData(nameof(UnixValidParentBacktrackingData))]
        [MemberData(nameof(UnixValidCurrentDirectoryData))]
        [MemberData(nameof(UnixCombinedRedundantData))]
        [MemberData(nameof(UnixDuplicateSeparatorsData))]
        [PlatformSpecific(TestPlatforms.AnyUnix)]
        public static void UnixValid_Try(string path, string expected)
        {
            if (!PlatformDetection.IsWindows)
            {
                expected = expected.Replace('\\', '/');
            }
            TestTryRedundantSegments(path, expected, true, expected.Length);
        }

        #endregion
    }
}
