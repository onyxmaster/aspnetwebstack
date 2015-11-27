// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Text;
using System.Threading;

namespace System.Web.WebPages
{
    internal static class StringWriterExtensions
    {
        public const int BufferSize = 1024;

        private static readonly ThreadLocal<char[]> _bufferCache = new ThreadLocal<char[]>(() => new char[BufferSize]);

        // Used to copy data from a string writer to avoid allocating the full string
        // which can end up on LOH (and cause memory fragmentation).
        public static void CopyTo(this StringWriter input, TextWriter output)
        {
            StringBuilder builder = input.GetStringBuilder();

            int remainingChars = builder.Length;
            if (remainingChars == 0)
            {
                return;
            }
            var outputWriter = output as StringWriter;
            if (outputWriter != null)
            {
                var outputBuilder = outputWriter.GetStringBuilder();
                outputBuilder.EnsureCapacity(outputBuilder.Length + remainingChars);
            }
            int bufferSize = Math.Min(builder.Length, BufferSize);

            var buffer = _bufferCache.Value;
            int currentPosition = 0;

            while (remainingChars > 0)
            {
                int copyLen = Math.Min(bufferSize, remainingChars);

                builder.CopyTo(currentPosition, buffer, 0, copyLen);

                output.Write(buffer, 0, copyLen);

                currentPosition += copyLen;
                remainingChars -= copyLen;
            }
        }
    }
}
