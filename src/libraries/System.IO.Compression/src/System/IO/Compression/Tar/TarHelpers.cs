// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace System.IO.Compression.Tar
{
    internal static class TarHelpers
    {
        // Returns true if all the bytes in the specified array are nulls, false otherwise.
        internal static bool IsAllNullBytes(byte[] array)
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] != 0)
                {
                    return false;
                }
            }
            return true;
        }

        // Reads the specified number of bytes and stores it in the byte buffer passed by reference.
        // Throws if end of stream is reached.
        internal static void ReadOrThrow(Stream archiveStream, ref byte[] buffer, int bytesToRead)
        {
            if (archiveStream.Read(buffer.AsSpan()) != bytesToRead)
            {
                throw new EndOfStreamException();
            }
        }

        // Returns the ASCII string contained in the specified buffer of bytes,
        // removing the trailing null or space chars.
        internal static string GetTrimmedAsciiString(ReadOnlySpan<byte> buffer, bool trim = true) =>
            GetTrimmedString(buffer, Encoding.ASCII, trim);

        // Returns the UTF8 string contained in the specified buffer of bytes,
        // removing the trailing null or space chars.
        internal static string GetTrimmedUtf8String(ReadOnlySpan<byte> buffer, bool trim = true) =>
            GetTrimmedString(buffer, Encoding.UTF8, trim);

        // Returns the string contained in the specified buffer of bytes,
        // in the specified encoding, removing the trailing null or space chars.
        internal static string GetTrimmedString(ReadOnlySpan<byte> buffer, Encoding encoding, bool trim = true)
        {
            int trimmedLength = buffer.Length;
            while (trim && trimmedLength > 0 && IsByteNullOrSpace(buffer[trimmedLength - 1]))
            {
                trimmedLength--;
            }

            return trimmedLength == 0 ? string.Empty : encoding.GetString(buffer.Slice(0, trimmedLength));

            static bool IsByteNullOrSpace(byte c) => c is 0 or 32;
        }

        // Receives a byte array that represents an ASCII string containing a number in octal base.
        // Converts the array to an octal base number, then transforms it to decimal base and returns it.
        internal static int GetTenBaseNumberFromOctalAsciiChars(Span<byte> buffer)
        {
            string str = GetTrimmedAsciiString(buffer);
            return string.IsNullOrEmpty(str) ? 0 : Convert.ToInt32(str, fromBase: 8);
        }

        // Returns a DateTime instance representing the number of seconds that have passed since the Unix Epoch.
        internal static DateTime DateTimeFromSecondsSinceEpoch(double secondsSinceUnixEpoch)
        {
            DateTimeOffset offset = DateTimeOffset.UnixEpoch.AddSeconds(secondsSinceUnixEpoch);
            return offset.DateTime;
        }
    }
}