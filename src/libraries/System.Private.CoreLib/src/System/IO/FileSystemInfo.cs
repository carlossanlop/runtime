// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.Serialization;

#if MS_IO_REDIST
namespace Microsoft.IO
#else
namespace System.IO
#endif
{
    public abstract partial class FileSystemInfo : MarshalByRefObject, ISerializable
    {
        // FullPath and OriginalPath are documented fields
        protected string FullPath = null!;          // fully qualified path of the file or directory
        protected string OriginalPath = null!;      // path passed in by the user

        internal string _name = null!; // Fields initiated in derived classes

        protected FileSystemInfo(SerializationInfo info, StreamingContext context)
        {
            throw new PlatformNotSupportedException();
        }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            throw new PlatformNotSupportedException();
        }

        // Full path of the directory/file
        public virtual string FullName => FullPath;

        public string Extension
        {
            get
            {
                int length = FullPath.Length;
                for (int i = length; --i >= 0;)
                {
                    char ch = FullPath[i];
                    if (ch == '.')
                        return FullPath.Substring(i, length - i);
                    if (PathInternal.IsDirectorySeparator(ch) || ch == Path.VolumeSeparatorChar)
                        break;
                }
                return string.Empty;
            }
        }

        public virtual string Name => _name;

        // Whether a file/directory exists
        public virtual bool Exists
        {
            get
            {
                try
                {
                    return ExistsCore;
                }
                catch
                {
                    return false;
                }
            }
        }

        // Delete a file/directory
        public abstract void Delete();

        public DateTime CreationTime
        {
            get => CreationTimeUtc.ToLocalTime();
            set => CreationTimeUtc = value.ToUniversalTime();
        }

        public DateTime CreationTimeUtc
        {
            get => CreationTimeCore.UtcDateTime;
            set => CreationTimeCore = File.GetUtcDateTimeOffset(value);
        }


        public DateTime LastAccessTime
        {
            get => LastAccessTimeUtc.ToLocalTime();
            set => LastAccessTimeUtc = value.ToUniversalTime();
        }

        public DateTime LastAccessTimeUtc
        {
            get => LastAccessTimeCore.UtcDateTime;
            set => LastAccessTimeCore = File.GetUtcDateTimeOffset(value);
        }

        public DateTime LastWriteTime
        {
            get => LastWriteTimeUtc.ToLocalTime();
            set => LastWriteTimeUtc = value.ToUniversalTime();
        }

        public DateTime LastWriteTimeUtc
        {
            get => LastWriteTimeCore.UtcDateTime;
            set => LastWriteTimeCore = File.GetUtcDateTimeOffset(value);
        }

        /// <summary>
        /// Returns the original path. Use FullName or Name properties for the full path or file/directory name.
        /// </summary>
        public override string ToString() => OriginalPath ?? string.Empty;

        /// <summary>
        /// Creates a symbolic link that points to the specified target.
        /// </summary>
        /// <param name="pathToTarget">The path, absolute or relative, of the symbolic link target.</param>
        public void CreateAsSymbolicLink(string pathToTarget)
        {
            if (pathToTarget == null)
            {
                throw new ArgumentNullException(nameof(pathToTarget));
            }
            else if (pathToTarget.Length == 0)
            {
                throw new ArgumentException(SR.Argument_EmptyPath);
            }

            bool isDirectory = this is DirectoryInfo;

            CreateAsSymbolicLinkInternal(pathToTarget, isDirectory);
        }

        /// <summary>
        /// If the current <see cref="FileSystemInfo"/> wraps a link, returns a <see cref="FileSystemInfo"/> instance that wraps the target.
        /// </summary>
        /// <param name="returnFinalTarget"><see langword="true"/> if the returned instance should wrap the final target in a chain of links; <see langword="false"/> if the returned instance should wrap the immediate target.</param>
        /// <returns>A <see cref="FileSystemInfo"/> representing the specified link target.</returns>
        public FileSystemInfo? ResolveLinkTarget(bool returnFinalTarget = false) => ResolveLinkTargetInternal(returnFinalTarget);

        /// <summary>
        /// If the current <see cref="FileSystemInfo"/> wraps a link, returns the path of the target file, which can be relative or absolute.
        /// </summary>
        public string? LinkTarget { get; }
    }
}
