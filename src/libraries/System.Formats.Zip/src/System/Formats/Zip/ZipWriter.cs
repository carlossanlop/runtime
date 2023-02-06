// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System;
using System.IO;

namespace System.Formats.Zip;

// Zip Spec: http://www.pkware.com/documents/casestudies/APPNOTE.TXT

public class ZipWriter : IDisposable
{
    public ZipWriter(Stream archiveStream)
    {
    }

    public void Dispose() => throw new NotImplementedException();
}
