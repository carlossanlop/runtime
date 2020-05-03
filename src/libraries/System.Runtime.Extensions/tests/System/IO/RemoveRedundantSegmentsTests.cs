// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace System.IO.Tests
{
    public class RemoveRedundantSegmentsTests
    {
        #region TestData

        // Null or empty tests
        public static TheoryData<string, string> NullOrEmptyData => new TheoryData<string, string>
        {
            { null,   null },
            { "",     ""   },
            { " ",    ""   },
            { "    ", ""   }
        };

        // Paths with '..' to indicate the removal of the previous segment
        public static TheoryData<string, string, string> DifferentBehaviorBacktrackingData => new TheoryData<string, string, string>
        {
            // Path                         Unix              Windows
            { @"/home/../myuser/../..",     @"/",             @".." },
            { @"/home/../myuser/../../",    @"/",             @"..\" },
            { @"/home/../myuser/../../..",  @"/",             @"..\.." },
            { @"/home/../myuser/../../../", @"/",             @"..\..\" },
            { @"/home/../..",               @"/",             @".." },
            { @"/home/../../",              @"/",             @"..\" },
            { @"/home/../../myuser",        @"/myuser",       @"..\myuser" },
            { @"/home/../../myuser/",       @"/myuser/",      @"..\myuser\" },
            { @"/home/../../myuser/../..",  @"/",             @"..\.." },
            { @"/home/../../myuser/../../", @"/",             @"..\..\" },
            { @"/..",                       @"/",             @"\.." },
            { @"/../",                      @"/",             @"\..\" },
            { @"/../home",                  @"/home",         @"\..\home" },
            { @"/../home/",                 @"/home/",        @"\..\home\" },
            { @"/../home/./myuser",         @"/home/myuser",  @"\..\home\myuser" },
            { @"/../home/./myuser/",        @"/home/myuser/", @"\..\home\myuser\" },
        };

        // Paths with '.' to indicate current directory
        public static TheoryData<string, string, string> DifferentBehaviorCurrentDirectoryData => new TheoryData<string, string, string>
        {
            // Path                   Unix              Windows
            { @"/./home",             @"/home",         @"home" },
            { @"/././home/",          @"/home/",        @"home\" },
            { @"/home/.",             @"/home",         @"home" },
            { @"/home/./",            @"/home/",        @"home\" },
            { @"/home/./.",           @"/home",         @"home" },
            { @"/home/././",          @"/home/",        @"home\" },
            { @"/./home/./",          @"/home/",        @"home\" },
            { @"/./home/./.",         @"/home",         @"home" },
            { @"/./home/././",        @"/home/",        @"home\" },
            { @"/./home/././folder",  @"/home/folder",  @"home\folder" },
            { @"/./home/././folder/", @"/home/folder/", @"home\folder\" },
        };

        // Combined '.' and '..'
        public static TheoryData<string, string, string> DifferentBehaviorCombinedRedundantData => new TheoryData<string, string, string>
        {
            // Path                         Unix                 Windows
            { @"/./..",                       @"/",             @".." },
            { @"/./../",                      @"/",             @"..\" },
            { @"/../.",                       @"/",             @"\.." },
            { @"/.././",                      @"/",             @"\..\" },
            { @"/./../.",                     @"/",             @".." },
            { @"/./.././",                    @"/",             @"..\" },
            { @"/.././.",                     @"/",             @"\.." },
            { @"/../././",                    @"/",             @"\..\" },
            { @"/./../..",                    @"/",             @"..\.." },
            { @"/./../../",                   @"/",             @"..\..\" },
            { @"/.././..",                    @"/",             @"\..\.." },
            { @"/.././../",                   @"/",             @"\..\..\" },
            { @"/../home/.",                  @"/home",         @"\..\home" },
            { @"/../home/./",                 @"/home/",        @"\..\home\" },
            { @"/./../home",                  @"/home",         @"..\home" },
            { @"/./../home/",                 @"/home/",        @"..\home\" },
            { @"/.././home",                  @"/home",         @"\..\home" },
            { @"/.././home/",                 @"/home/",        @"\..\home\" },
            { @"/../home/myuser/.",           @"/home/myuser",  @"\..\home\myuser" },
            { @"/../home/myuser/./",          @"/home/myuser/", @"\..\home\myuser\" },
            { @"/../home/myuser/./../",       @"/home/",        @"\..\home\" },
            { @"/../home/myuser/./../folder", @"/home/folder",  @"\..\home\folder" },
        };

        // Paths that are not rooted
        public static TheoryData<string, string> UnqualifiedPathsData => new TheoryData<string, string>
        {
            { @"Users\myuser\..\",             @"Users\" },
            { @"Users\myuser\..",              @"Users" },
            { @"Users\..\..",                  @".." },
            { @"Users\..\..\",                 @"..\" },
            { @"myuser\..\",                   @"" },
            { @"myuser",                       @"myuser" },
            { @".\myuser",                     @"myuser" },
            { @".\myuser\",                    @"myuser\" },
            { @".\.\myuser",                   @"myuser" },
            { @".\.\myuser\",                  @"myuser\" },
            { @"..\myuser",                    @"..\myuser" },
            { @"..\myuser\",                   @"..\myuser\" },
            { @"..\\myuser\",                  @"..\myuser\" },
            { @"..\myuser\..",                 @".." },
            { @"..\first\..\second",           @"..\second" },
            { @"..\first\..\..\second\..",     @"..\.." },
            { @"..\first\..\..\second\..\",    @"..\..\" },
            { @"..\first\..\..\second\..",     @"..\.." },
            { @"..\first\..\..\second\..\..",  @"..\..\.." },
            { @"..\first\..\..\second\..\..\", @"..\..\..\" },
        };

        // Edge cases
        public static TheoryData<string, string> ValidEdgeCasesData => new TheoryData<string, string>
        {
            { @"C:\Users\myuser\folder.with\one\dot",   @"C:\Users\myuser\folder.with\one\dot" },
            { @"C:\Users\myuser\folder..with\two\dots", @"C:\Users\myuser\folder..with\two\dots" },
            { @"C:\Users\.folder\startswithdot",        @"C:\Users\.folder\startswithdot" },
            { @"C:\Users\folder.\endswithdot",          @"C:\Users\folder.\endswithdot" },
            { @"C:\Users\..folder\startswithtwodots",   @"C:\Users\..folder\startswithtwodots" },
            { @"C:\Users\folder..\endswithtwodots",     @"C:\Users\folder..\endswithtwodots" },
            { @"C:\...", @"C:\" },
            { @"C:\...\folder", @"C:\...\folder" },
            { @"C:\...\..", @"C:\" },
            { @"C:\...\.", @"C:\..." },
            { @"C:\...\.\", @"C:\...\" },
            { @"C:\Users\myuser\this\is\a\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\long\path\but\it\should\not\matter\extraword\..\", @"C:\Users\myuser\this\is\a\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\really\long\path\but\it\should\not\matter\" },
            { @"C:\Users\myuser\this_is_a_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_long_foldername\but_it_should_not_matter\extraword\..\", @"C:\Users\myuser\this_is_a_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_really_long_foldername\but_it_should_not_matter\" },
        };

        // Another edge case is the triple dot as file or folder name.
        // It's an evil but valid file or folder name, supported both in Unix and Windows.
        // In Windows, having it at the end of the path, gets it removed.
        // In both Windows and Unix, having them as a middle segment, behaves as a valid folder.
        public static TheoryData<string, string, string> DifferentBehaviorTripleDotData => new TheoryData<string, string, string>
        {
            // Path                         Unix              Windows
            { @"/...",                @"/...",          @"\" },
            { @"/.../",               @"/.../",         @"\" },
            { @"/.../..",             @"/",             @"\" },
            { @"/.../../",            @"/",             @"\" },
            { @"/.../.",              @"/...",          @"\..." },
            { @"/..././",             @"/.../",         @"\...\" },
            { @"C:\...",              @"C:\...",        @"C:\" },
            { @"C:\...\",             @"C:\...\",       @"C:\" },
            { @"C:\...\folder",       @"C:\...\folder", @"C:\...\folder" },
            { @"C:\...\.\folder",     @"C:\...\folder", @"C:\...\folder" },
            { @"C:\...\..\folder",    @"C:\folder",     @"C:\folder" },
            { @"C:\...\folder\..",    @"C:\...",        @"C:\..." },
            { @"C:\...\folder\..\",   @"C:\...\",       @"C:\...\" },
        };

        #endregion

        #region Tests

        [Theory]
        [MemberData(nameof(NullOrEmptyData))]
        [MemberData(nameof(UnqualifiedPathsData))]
        [MemberData(nameof(ValidEdgeCasesData))]
        public static void SpecialCases_String(string path, string expected) => TestRedundantSegments(path, expected);

        [Theory]
        [MemberData(nameof(NullOrEmptyData))]
        [MemberData(nameof(UnqualifiedPathsData))]
        [MemberData(nameof(ValidEdgeCasesData))]
        public static void SpecialCases_Span(string path, string expected) => TestRedundantSegments(path.AsSpan(), expected);

        [Theory]
        [MemberData(nameof(UnqualifiedPathsData))]
        [MemberData(nameof(ValidEdgeCasesData))]
        public static void SpecialCases_True_Try(string path, string expected) => TestTryRedundantSegments(path, expected, true, expected.Length);

        [Theory]
        [MemberData(nameof(NullOrEmptyData))]
        public static void SpecialCases_False_Try(string path, string expected) => TestTryRedundantSegments(path, expected, false, 0);

        [Theory]
        [MemberData(nameof(DifferentBehaviorCurrentDirectoryData))]
        [MemberData(nameof(DifferentBehaviorBacktrackingData))]
        [MemberData(nameof(DifferentBehaviorCombinedRedundantData))]
        [MemberData(nameof(DifferentBehaviorTripleDotData))]
        public static void DifferentBehavior_String(string path, string expectedUnix, string expectedWindows)
        {
            string expected = (PlatformDetection.IsWindows) ? expectedWindows : expectedUnix;
            TestRedundantSegments(path, expected);
        }

        [Theory]
        [MemberData(nameof(DifferentBehaviorCurrentDirectoryData))]
        [MemberData(nameof(DifferentBehaviorBacktrackingData))]
        [MemberData(nameof(DifferentBehaviorCombinedRedundantData))]
        [MemberData(nameof(DifferentBehaviorTripleDotData))]
        public static void DifferentBehavior_Span(string path, string expectedUnix, string expectedWindows)
        {
            string expected = (PlatformDetection.IsWindows) ? expectedWindows : expectedUnix;
            TestRedundantSegments(path.AsSpan(), expected);
        }

        [Theory]
        [MemberData(nameof(DifferentBehaviorCurrentDirectoryData))]
        [MemberData(nameof(DifferentBehaviorBacktrackingData))]
        [MemberData(nameof(DifferentBehaviorCombinedRedundantData))]
        [MemberData(nameof(DifferentBehaviorTripleDotData))]
        public static void DifferentBehavior_Try(string path, string expectedUnix, string expectedWindows)
        {
            string expected = (PlatformDetection.IsWindows) ? expectedWindows : expectedUnix;
            TestTryRedundantSegments(path, expected, true, expected.Length);
        }

        [Fact]
        public static void DestinationTooSmall_Try()
        {
            Span<char> actualDestination = stackalloc char[1];
            bool actualReturn = Path.TryRemoveRedundantSegments(@"C:\Users\myuser", actualDestination, out int actualCharsWritten);
            string stringDestination = actualDestination.Slice(0, actualCharsWritten).ToString();
            Assert.False(actualReturn);
            Assert.Equal(0, actualCharsWritten);
            Assert.Equal(0, stringDestination.Length);
        }

        #endregion

        #region Helper methods

        protected static void TestTryRedundantSegments(string path, string expected, bool expectedReturn, int expectedCharsWritten)
        {
            Span<char> actualDestination = stackalloc char[(path != null) ? path.Length : 1];
            bool actualReturn = Path.TryRemoveRedundantSegments(path.AsSpan(), actualDestination, out int actualCharsWritten);
            Assert.Equal(expectedReturn, actualReturn);
            Assert.Equal(expected ?? string.Empty, actualDestination.Slice(0, actualCharsWritten).ToString());
            Assert.Equal(expectedCharsWritten, actualCharsWritten);
        }

        protected static void TestRedundantSegments(ReadOnlySpan<char> path, string expected)
        {
            string actual = Path.RemoveRedundantSegments(path);
            Assert.Equal(expected ?? string.Empty, actual);
        }

        protected static void TestRedundantSegments(string path, string expected)
        {
            string actual = Path.RemoveRedundantSegments(path);
            Assert.Equal(expected, actual);
        }

        #endregion
    }
}
