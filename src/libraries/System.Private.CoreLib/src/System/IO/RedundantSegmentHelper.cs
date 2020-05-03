// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace System.IO
{
    /// <summary>Contains internal helpers for removing redundant segments that are shared between many projects.</summary>
    internal static partial class RedundantSegmentHelper
    {
        // Adjusts the length of the buffer to the position of the previous directory separator due to a double dot.
        private static bool TryBacktrackToPreviousSeparator(ref ValueStringBuilder sb, ReadOnlySpan<char> path, int charsToSkip, int currPos)
        {
            bool isRedundantUntilTheRoot = true;
            int totalDots;
            // If all the previous paths are '.' or '..' and separators, it means none
            // of the previous segments is backtrackable.
            // The exception is segments with 3 or more dots (it's a valid file or folder name).
            int pos = currPos;
            while (pos >= charsToSkip)
            {
                if (IsPreviousSegmentOnlyDots(path, charsToSkip, pos, out totalDots))
                {
                    if (totalDots >= 3)
                    {
                        isRedundantUntilTheRoot = false;
                        break;
                    }
                    pos -= totalDots;
                }
                else
                {
                    isRedundantUntilTheRoot = false;
                    break;
                }
            }

            // Can't backtrack, we are in unqualified territory
            if (isRedundantUntilTheRoot)
            {
                return false;
            }

            int unwindPosition;
            for (unwindPosition = sb.Length - 1; unwindPosition >= charsToSkip; unwindPosition--)
            {
                if (PathInternal.IsDirectorySeparator(sb[unwindPosition]))
                {
                    sb.Length = unwindPosition;
                    break;
                }
            }

            // Never go beyond the root.
            // Or in the case of an unqualified path, if the initial segment was a folder
            // without a separator at the beginning, the resulting string is empty.
            // e.g. "C:test\.." => "C:"
            if (unwindPosition < charsToSkip)
            {
                sb.Length = charsToSkip;
            }

            return true;
        }

        // If the character is a directory separator, ensure it is set to the current operating system's character.
        private static bool TryNormalizeSeparatorCharacter(ref char c)
        {
            if (c != PathInternal.DirectorySeparatorChar && c == PathInternal.AltDirectorySeparatorChar)
            {
                c = PathInternal.DirectorySeparatorChar;
                return true;
            }
            return false;
        }

        // Checks if the segment before the specified position in the path is a ".." segment.
        private static bool IsPreviousSegmentDoubleDot(ReadOnlySpan<char> path, int currPos)
        {
            return currPos == 0 ||
                (currPos - 2 >= 0 && path[currPos - 2] == '.' && path[currPos - 1] == '.');
        }

        // Checks if the next segment consists of only dots
        private static bool IsNextSegmentOnlyDots(ReadOnlySpan<char> path, int currPos, out int totalDots)
        {
            totalDots = 0;
            int i = currPos;
            while (i < path.Length && !PathInternal.IsDirectorySeparator(path[i]))
            {
                if (path[i] != '.')
                {
                    return false;
                }
                i++;
            }
            totalDots = i - currPos;
            return totalDots > 0;
        }

        private static bool IsPreviousSegmentOnlyDots(ReadOnlySpan<char> path, int charsToSkip, int currPos, out int totalDots)
        {
            totalDots = 0;
            int i = currPos;
            while (i > charsToSkip && !PathInternal.IsDirectorySeparator(path[i]))
            {
                if (path[i] != '.')
                {
                    return false;
                }
                i--;
            }
            totalDots = currPos - i;
            return totalDots > 0;
        }

        // CHecks if the segment after the specified position in the path is a ".." segment.
        private static bool IsNextSegmentDoubleDot(ReadOnlySpan<char> path, int currPos)
        {
            return currPos + 2 < path.Length &&
                (currPos + 3 == path.Length || PathInternal.IsDirectorySeparator(path[currPos + 3])) &&
                path[currPos + 1] == '.' && path[currPos + 2] == '.';
        }

        // CHecks if the segment after the specified position in the path is a "." segment.
        private static bool IsNextSegmentSingleDot(ReadOnlySpan<char> path, int currPos)
        {
            return (currPos + 2 == path.Length || PathInternal.IsDirectorySeparator(path[currPos + 2])) &&
                path[currPos + 1] == '.';
        }
    }
}
