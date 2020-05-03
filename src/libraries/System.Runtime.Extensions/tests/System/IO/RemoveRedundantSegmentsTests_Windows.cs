// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace System.IO.Tests
{
    [PlatformSpecific(TestPlatforms.Windows)]
    public class RemoveRedundantSegmentsTests_Windows : RemoveRedundantSegmentsTests
    {
        #region TestData

        private static readonly string[] s_Prefixes = new string[] { "", @"\\?\", @"\\.\" };
        private static readonly string[] s_UncPrefixes = new string[] { @"\\", @"\\?\UNC\", @"\\.\UNC\" };
        
        // Normal paths
        public static TheoryData<string, string> WindowsNormalData => new TheoryData<string, string>
        {
            // A '\' inside a string that is prefixed with @ is actually passed as an escaped backward slash "\\"
            { @"C:",               @"C:" },
            { @"C:\",              @"C:\" },
            { @"C:\Users",         @"C:\Users" },
            { @"C:\Users\",        @"C:\Users\" },
            { @"C:\Users\myuser",  @"C:\Users\myuser" },
            { @"C:\Users\myuser\", @"C:\Users\myuser\" },
        };

        // Paths with '..' to indicate the removal of the previous segment
        public static TheoryData<string, string> WindowsValidParentBacktrackingData => new TheoryData<string, string>
        {
            { @"C:\..",                        @"C:\" },
            { @"C:\..\",                       @"C:\" },
            { @"C:\..\Users",                  @"C:\Users" },
            { @"C:\..\Users\",                 @"C:\Users\" },
            { @"C:\Users\..",                  @"C:\" },
            { @"C:\Users\..\",                 @"C:\" },
            { @"C:\Users\..\..",               @"C:\" },
            { @"C:\Users\..\..\",              @"C:\" },
            { @"C:\Users\..\myuser",           @"C:\myuser" },
            { @"C:\Users\..\myuser\",          @"C:\myuser\" },
            { @"C:\Users\..\..\myuser",        @"C:\myuser" },
            { @"C:\Users\..\..\myuser\",       @"C:\myuser\" },
            { @"C:\Users\myuser\..",           @"C:\Users" },
            { @"C:\Users\myuser\..\",          @"C:\Users\" },
            { @"C:\Users\myuser\..\..",        @"C:\" },
            { @"C:\Users\myuser\..\..\",       @"C:\" },
            { @"C:\Users\..\myuser\..",        @"C:\" },
            { @"C:\Users\..\myuser\..\",       @"C:\" },
            { @"C:\Users\..\myuser\..\..",     @"C:\" },
            { @"C:\Users\..\myuser\..\..\",    @"C:\" },
            { @"C:\Users\..\myuser\..\..\..",  @"C:\" },
            { @"C:\Users\..\myuser\..\..\..\", @"C:\" },
            { @"C:\Users\..\..\myuser\..\..",  @"C:\" },
            { @"C:\Users\..\..\myuser\..\..\", @"C:\" },
        };
    
        // Paths with '.' to indicate current directory
        public static TheoryData<string, string> WindowsValidCurrentDirectoryData => new TheoryData<string, string>
        {
            { @"C:\.",                     @"C:\" },
            { @"C:\.\",                    @"C:\" },
            { @"C:\.\.",                   @"C:\" },
            { @"C:\.\.\",                  @"C:\" },
            { @"C:\.\Users",               @"C:\Users" },
            { @"C:\.\Users\",              @"C:\Users\" },
            { @"C:\.\.\Users",             @"C:\Users" },
            { @"C:\.\.\Users\",            @"C:\Users\" },
            { @"C:\Users\.",               @"C:\Users" },
            { @"C:\Users\.\",              @"C:\Users\" },
            { @"C:\Users\.\.",             @"C:\Users" },
            { @"C:\Users\.\.\",            @"C:\Users\" },
            { @"C:\.\Users\myuser",        @"C:\Users\myuser" },
            { @"C:\.\Users\myuser\",       @"C:\Users\myuser\" },
            { @"C:\.\Users\.\myuser",      @"C:\Users\myuser" },
            { @"C:\.\Users\.\myuser\",     @"C:\Users\myuser\" },
            { @"C:\.\Users\.\myuser\.",    @"C:\Users\myuser" },
            { @"C:\.\Users\.\myuser\.\",   @"C:\Users\myuser\" },
            { @"C:\Users\.\.\myuser\.\.",  @"C:\Users\myuser" },
            { @"C:\Users\.\.\myuser\.\.\", @"C:\Users\myuser\" },
        };

        // Combined '.' and '..'
        public static TheoryData<string, string> WindowsCombinedRedundantData => new TheoryData<string, string>
        {
            { @"C:\.\..",               @"C:\" },
            { @"C:\.\..\",              @"C:\" },
            { @"C:\..\.",               @"C:\" },
            { @"C:\..\.\",              @"C:\" },
            { @"C:\.\..\.",             @"C:\" },
            { @"C:\.\..\.\",            @"C:\" },
            { @"C:\..\.\.",             @"C:\" },
            { @"C:\..\.\.\",            @"C:\" },
            { @"C:\.\..\..",            @"C:\" },
            { @"C:\.\..\..\",           @"C:\" },
            { @"C:\..\.\..",            @"C:\" },
            { @"C:\..\.\..\",           @"C:\" },
            { @"C:\Users\.\..",         @"C:\" },
            { @"C:\Users\.\..\",        @"C:\" },
            { @"C:\Users\..\.",         @"C:\" },
            { @"C:\Users\..\.\",        @"C:\" },
            { @"C:\.\Users\..",         @"C:\" },
            { @"C:\.\Users\..\",        @"C:\" },
            { @"C:\..\Users\.",         @"C:\Users" },
            { @"C:\..\Users\.\",        @"C:\Users\" },
            { @"C:\.\..\Users",         @"C:\Users" },
            { @"C:\.\..\Users\",        @"C:\Users\" },
            { @"C:\..\.\Users",         @"C:\Users" },
            { @"C:\..\.\Users\",        @"C:\Users\" },
            { @"C:\.\Users\myuser\..",  @"C:\Users" },
            { @"C:\.\Users\myuser\..\", @"C:\Users\" },
            { @"C:\..\Users\myuser\.",  @"C:\Users\myuser" },
            { @"C:\..\Users\myuser\.\", @"C:\Users\myuser\" },
            { @"C:\.\Users\..\myuser",  @"C:\myuser" },
            { @"C:\.\Users\..\myuser\", @"C:\myuser\" },
            { @"C:\..\Users\.\myuser",  @"C:\Users\myuser" },
            { @"C:\..\Users\.\myuser\", @"C:\Users\myuser\" },
        };

        // Duplicate separators
        public static TheoryData<string, string> WindowsDuplicateSeparatorsData => new TheoryData<string, string>
        {
            { @"C:\\",                 @"C:\" },
            { @"C:\\\",                @"C:\" },
            { @"C://",                 @"C:\" },
            { @"C:///",                @"C:\" },
            { @"C:\/",                 @"C:\" },
            { @"C:/\",                 @"C:\" },
            { @"C:\/\",                @"C:\" },
            { @"C:/\\",                @"C:\" },
            { @"C:\//",                @"C:\" },
            { @"C:/\/",                @"C:\" },
            { @"C:\\Users",            @"C:\Users" },
            { @"C:\\\Users",           @"C:\Users" },
            { @"C:\\Users\\\",         @"C:\Users\" },
            { @"C:\\\Users\\",         @"C:\Users\" },
            { @"C:\\Users\\/\",        @"C:\Users\" },
            { @"C:\\\Users\/\",        @"C:\Users\" },
            { @"C:\/Users",            @"C:\Users" },
            { @"C:\/Users/",           @"C:\Users\" },
            { @"C:\/Users/",           @"C:\Users\" },
            { @"C:\\Users\\.\",        @"C:\Users\" },
            { @"C:\\\Users\..\\",      @"C:\" },
            { @"C:\\Users\\./.\",      @"C:\Users\" },
            { @"C:\.\\Users\/./\\",    @"C:\Users\" },
            { @"C:\\.\Users\/../\\./", @"C:\" },
        };

        // Network locations - "Server\Share" always stays. UNC prefixes get prepended in the tests.
        public static TheoryData<string, string> UncData => new TheoryData<string, string>
        {
            { @"Server\Share\git\runtime",              @"Server\Share\git\runtime"},
            { @"Server\Share\\git\runtime",             @"Server\Share\git\runtime"},
            { @"Server\Share\git\\runtime",             @"Server\Share\git\runtime"},
            { @"Server\Share\git\.\runtime\.\\",        @"Server\Share\git\runtime\"},
            { @"Server\Share\git\runtime",              @"Server\Share\git\runtime"},
            { @"Server\Share\git\..\runtime",           @"Server\Share\runtime"},
            { @"Server\Share\git\runtime\..\",          @"Server\Share\git\"},
            { @"Server\Share\git\runtime\..\..\..\",    @"Server\Share\"},
            { @"Server\Share\git\runtime\..\..\.\",     @"Server\Share\"},
            { @"Server\Share\git\..\.\runtime\temp\..", @"Server\Share\runtime"},
            { @"Server\Share\git\..\\\.\..\runtime",    @"Server\Share\runtime"},
            { @"Server\Share\git\runtime\",             @"Server\Share\git\runtime\"},
            { @"Server\Share\git\temp\..\runtime\",     @"Server\Share\git\runtime\"},
        };

        #endregion

        #region Tests

        [Theory]
        [MemberData(nameof(WindowsNormalData))]
        [MemberData(nameof(WindowsValidParentBacktrackingData))]
        [MemberData(nameof(WindowsValidCurrentDirectoryData))]
        [MemberData(nameof(WindowsCombinedRedundantData))]
        [MemberData(nameof(WindowsDuplicateSeparatorsData))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public static void WindowsValid_String(string path, string expected)
        {
            foreach (string prefix in s_Prefixes)
            {
                TestRedundantSegments(prefix + path, prefix + expected);
            }
        }

        [Theory]
        [MemberData(nameof(WindowsNormalData))]
        [MemberData(nameof(WindowsValidParentBacktrackingData))]
        [MemberData(nameof(WindowsValidCurrentDirectoryData))]
        [MemberData(nameof(WindowsCombinedRedundantData))]
        [MemberData(nameof(WindowsDuplicateSeparatorsData))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public static void WindowsValid_Span(string path, string expected)
        {
            foreach (string prefix in s_Prefixes)
            {
                TestRedundantSegments((prefix + path).AsSpan(), prefix + expected);
            }
        }

        [Theory]
        [MemberData(nameof(WindowsNormalData))]
        [MemberData(nameof(WindowsValidParentBacktrackingData))]
        [MemberData(nameof(WindowsValidCurrentDirectoryData))]
        [MemberData(nameof(WindowsCombinedRedundantData))]
        [MemberData(nameof(WindowsDuplicateSeparatorsData))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public static void WindowsValid_Try(string path, string expected)
        {
            foreach (string prefix in s_Prefixes)
            {
                TestTryRedundantSegments(prefix + path, prefix + expected, true, (prefix + expected).Length);
            }
        }

        [Theory]
        [MemberData(nameof(UncData))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public static void WindowsUnc_String(string path, string expected)
        {
            foreach (string prefix in s_UncPrefixes)
            {
                TestRedundantSegments(prefix + path, prefix + expected);
            }
        }

        [Theory]
        [MemberData(nameof(UncData))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public static void WindowsUnc_Span(string path, string expected)
        {
            foreach (string prefix in s_UncPrefixes)
            {
                TestRedundantSegments((prefix + path).AsSpan(), prefix + expected);
            }
        }

        [Theory]
        [MemberData(nameof(UncData))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public static void WindowsUnc_Try(string path, string expected)
        {
            foreach (string prefix in s_UncPrefixes)
            {
                TestTryRedundantSegments(prefix + path, prefix + expected, true, (prefix + expected).Length);
            }
        }

        #endregion
    }
}
