// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace System.IO
{
    internal static partial class RedundantSegmentHelper
    {
        /// <summary>
        /// Tries to remove relative segments from the given path, starting the analysis at the specified location.
        /// </summary>
        /// <param name="path">The input path.</param>
        /// <param name="sb">A reference to a value string builder that will store the result.</param>
        /// <returns><see langword="true" /> if the path was modified; <see langword="false" /> otherwise.</returns>
        internal static bool TryRemoveRedundantSegments(ReadOnlySpan<char> path, ref ValueStringBuilder sb)
        {
            Debug.Assert(path.Length > 0);

            // Windows can do the redundant segment removal for us if the path is fully qualified
            // We take care of partially qualified paths
            if (!PathInternal.IsPartiallyQualified(path))
            {
                PathHelper.GetFullPathName(path, ref sb);
                return true;
            }

            // We can still have a root in a partially qualified path, for example:
            // C:folder  ,  C:../folder  ,  C:./folder
            int rootLength = PathInternal.GetRootLength(path);
            int rootCharsToSkip = rootLength;
            bool flippedSeparator = false;
            char c;

            // If we have a root, remove "\\", "\.\", and "\..\" from the path by copying each character to the output,
            // except the ones we're removing, such that the builder contains the normalized path at the end.
            if (rootLength > 0)
            {
                // We treat "\.." , "\." and "\\" as a redundant segment.
                // We want to collapse the first separator past the root presuming the root actually ends in a separator.
                // In cases like "\\?\C:\.\" and "\\?\C:\..\", the first segment after the root will be ".\" and "..\" which
                // is not considered as a redundant segment and hence should not be removed.
                if (PathInternal.IsDirectorySeparator(path[rootCharsToSkip - 1]))
                {
                    rootCharsToSkip--;
                }

                // Append the root, if any.
                // Normalize its directory separators if needed
                for (int i = 0; i < rootCharsToSkip; i++)
                {
                    c = path[i];
                    flippedSeparator |= TryNormalizeSeparatorCharacter(ref c);
                    sb.Append(c);
                }
            }

            // Iterate the characters after the root, if any.
            for (int currPos = rootCharsToSkip; currPos < path.Length; currPos++)
            {
                c = path[currPos];

                bool isSeparator = PathInternal.IsDirectorySeparator(c);

                // Normal case: Start analysis of current segment on the separator
                if (isSeparator && currPos + 1 < path.Length)
                {
                    // Skip repeated separators, take only the last one.
                    // e.g. "parent//child" => "parent/child", or "parent/////child" => "parent/child"
                    if (PathInternal.IsDirectorySeparator(path[currPos + 1]))
                    {
                        continue;
                    }

                    if (IsNextSegmentOnlyDots(path, currPos, out int totalDots))
                    {
                        Debug.Assert(totalDots > 0);

                        // Skip the next segment if it's a single dot (current directory).
                        // Even if we are at the beginning of a path that is unqualified, we always remove these.
                        // e.g. "parent/./child" => "parent/child", or "parent/." => "parent/" or "./other" => "other"
                        if (totalDots == 1)
                        {
                            currPos++;
                            continue;
                        }

                        // Skip the next segment if it's a double dot (backtrack to parent directory).
                        // e.g. "parent/child/../grandchild" => "parent/grandchild"
                        else if (totalDots == 2)
                        {
                            // Unqualified paths need to check if there is a folder segment before reaching position 0.
                            // So if the previous segment is "..", it means it wasn't processed in a previous loop on purpose
                            // due to the path being unqualified up to the current position, in which case we do nothing.
                            // e.g. "../.." => "../.."  or  "../../folder/../../" => "../../../"
                            // Otherwise, the previous segment is a backtrackable segment.
                            // e.g. "folder/.." => ""  or  "folder/folder/../" => "folder/"
                            // If no backtrackable segments are found behind the current position
                            // (only "." or "..") then we need to keep the current ".." segment too
                            if (!TryBacktrackToPreviousSeparator(ref sb, path, rootCharsToSkip, currPos))
                            {
                                // If the buffer already contains data
                                // add a directory separator only if the buffer does not have one already.
                                // e.g. "..\.\.." => "..\.."
                                if (sb.Length > 0 && !PathInternal.IsDirectorySeparator(sb[sb.Length - 1]))
                                {
                                    sb.Append(path[currPos]);
                                    flippedSeparator |= TryNormalizeSeparatorCharacter(ref sb[sb.Length - 1]);
                                }
                                // Add the double dots
                                sb.Append("..");
                            }

                            currPos += 2;
                            continue;
                        }
                        // Paths larger than 2 are considered valid file or folder names, but in
                        // Windows, if they are the last segment of the path, they need to be removed
                        else
                        {
                            currPos += totalDots;
                            if (currPos == path.Length || currPos + 1 == path.Length)
                            {
                                Debug.Assert(TryBacktrackToPreviousSeparator(ref sb, path, rootCharsToSkip, currPos));
                            }
                        }
                    }
                }
                // Special case: single dot segments at the beginning of the path must be skipped
                else if (rootCharsToSkip == 0 && sb.Length == 0 && IsNextSegmentSingleDot(path, currPos - 1))
                {
                    currPos++;
                    continue;
                }

                // Normalize the directory separator if needed
                if (isSeparator)
                {
                    flippedSeparator |= TryNormalizeSeparatorCharacter(ref c);
                }

                // Always add the character to the buffer if it's not a directory separator.

                // If it's a directory separator, only append it when:
                // - The buffer already has content:
                //     e.g. "folder/" => "folder/"
                // - The buffer is empty but the very first segment is a double dot:
                //     e.g. "/../folder" => "/../folder"

                // If it's a directory separator, do not append when it's the first character of a sequence with these conditions:
                // - Started with actual folders which got removed by double dot segments (buffer is empty), and
                //   has more double dot segments than folders, which would make the double dots reach the beginning of the buffer:
                //     e.g. "folder/../.." => ".." or "folder/folder/../../../" => "../"
                // - Is rooted (even if partially qualified), starts with double dots, or started with actual folders which got removed by
                // double dot segments (buffer is empty), and has more double dot segments than folders, which would make the double dots
                // reach the beginning of the buffer:
                //     e.g. "C:..\System32" => "C:\System32" or "C:System32\..\..\" => "C:..\"
                if (!isSeparator || sb.Length > rootLength ||
                    (IsNextSegmentDoubleDot(path, currPos) && (currPos == 0 || sb.Length > rootLength)))
                {
                    sb.Append(c);
                }
            }

            // If we haven't changed the source path, return the original
            if (!flippedSeparator && sb.Length == path.Length)
            {
                return false;
            }

            // Final adjustments:
            // We may have eaten the trailing separator from the root when we started, and haven't replaced it.
            // Make sure to only append the trailing separator if the buffer contained information.
            if (rootCharsToSkip != rootLength && sb.Length > 0)
            {
                // e.g "C:\"
                if (sb.Length < rootCharsToSkip)
                {
                    sb.Append(path[rootCharsToSkip - 1]);
                }
                // e.g. "C:\." => "C:\" or "\\?\C:\.." => "\\?\C:\"
                else if (sb.Length == rootCharsToSkip && path.Length > rootCharsToSkip &&
                    PathInternal.IsDirectorySeparator(path[rootCharsToSkip]))
                {
                    sb.Append(path[rootCharsToSkip]);
                }
            }
            // If the buffer contained information, but the path finished with a separator, the separator may have
            // been added, but we should never return a single separator for unqualified paths.
            // e.g. "folder/../" => ""
            else if (sb.Length == 1 && PathInternal.IsDirectorySeparator(sb[0]))
            {
                sb.Length = 0;
            }

            return true;
        }
    }
}
