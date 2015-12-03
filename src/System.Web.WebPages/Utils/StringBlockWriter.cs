// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace System.Web.WebPages
{
    public sealed class StringBlockWriter : TextWriter
    {
        private static readonly Encoding _encoding = new UnicodeEncoding(false, false);

        private static readonly ThreadLocal<LinkedList<StringBuilder>> _charBufferCache = new ThreadLocal<LinkedList<StringBuilder>>(() => new LinkedList<StringBuilder>());

        private const int ConcatBufferLimit = 10000;

        private const int PerThreadBufferCacheLimit = 32;

        private const int CharBufferThreshold = 31;

        private const int InitialPartsCapacity = 15;

        public override Encoding Encoding
        {
            get
            {
                return _encoding;
            }
        }

        public int Length
        {
            get
            {
                Flush();
                return _parts.Count;
            }

            set
            {
                var diff = _parts.Count - value;
                if (diff != 0)
                {
                    Flush();
                    _parts.RemoveRange(value, diff);
                }
            }
        }

        private readonly List<object> _parts = new List<object>(InitialPartsCapacity);

        private StringBuilder _charBuffer;

        private void Write(StringBlockWriter writer)
        {
            Flush();
            _parts.Add(writer);
        }

        public override void Write(string value)
        {
            if (value == null)
            {
                return;
            }
            var length = value.Length;
            if (length == 0)
            {
                return;
            }
            if (length <= CharBufferThreshold)
            {
                var charBuffer = GetCharBuffer();
                if (charBuffer.Length + length <= charBuffer.Capacity)
                {
                    charBuffer.Append(value);
                    return;
                }
            }
            Flush();
            _parts.Add(value);
        }

        public override void Flush()
        {
            var charBuffer = _charBuffer;
            if (charBuffer != null)
            {
                var value = charBuffer.ToString();
                _parts.Add(value);
                _charBuffer = null;
                var charBufferCache = _charBufferCache.Value;
                if (charBufferCache.Count < PerThreadBufferCacheLimit)
                {
                    charBufferCache.AddFirst(charBuffer);
                }
            }
        }

        public override void Write(char value)
        {
            var charBuffer = GetCharBuffer();
            if (charBuffer.Length == charBuffer.Capacity)
            {
                Flush();
                charBuffer = GetCharBuffer();
            }
            charBuffer.Append(value);
        }

        private StringBuilder GetCharBuffer()
        {
            var charBuffer = _charBuffer;
            if (charBuffer == null)
            {
                var charBufferCache = _charBufferCache.Value;
                var cacheNode = charBufferCache.First;
                if (cacheNode != null)
                {
                    charBufferCache.Remove(cacheNode);
                    charBuffer = cacheNode.Value;
                    charBuffer.Clear();
                }
                else
                {
                    charBuffer = new StringBuilder(StringWriterExtensions.BufferSize);
                }
                _charBuffer = charBuffer;
            }

            return charBuffer;
        }

        public override void Write(char[] buffer, int index, int count)
        {
            if (count == 0)
            {
                return;
            }
            if (count <= CharBufferThreshold)
            {
                var charBuffer = GetCharBuffer();
                if (charBuffer.Length + count <= charBuffer.Capacity)
                {
                    charBuffer.Append(buffer, index, count);
                    return;
                }
            }
            Flush();
            _parts.Add(new string(buffer, index, count));
        }

        public void CopyTo(TextWriter target)
        {
            var writer = target as StringBlockWriter;
            if (writer != null)
            {
                writer.Write(this);
                Flush();
                return;
            }
            CopyToInternal(target);
        }

        private void CopyToInternal(TextWriter target)
        {
            Flush();
            foreach (var item in _parts)
            {
                var itemWriter = item as StringBlockWriter;
                if (itemWriter != null)
                {
                    itemWriter.CopyToInternal(target);
                    continue;
                }
                target.Write((string)item);
            }
        }

        private int GetCount()
        {
            Flush();
            int count = 0;
            foreach (var item in _parts)
            {
                var itemWriter = item as StringBlockWriter;
                if (itemWriter != null)
                {
                    count += itemWriter.GetCount();
                    continue;
                }
                ++count;
            }
            return count;
        }

        private int CopyTo(string[] buffer, int index)
        {
            foreach (var item in _parts)
            {
                var itemWriter = item as StringBlockWriter;
                if (itemWriter != null)
                {
                    index = itemWriter.CopyTo(buffer, index);
                    continue;
                }
                buffer[index++] = (string)item;
            }
            return index;
        }

        public override string ToString()
        {
            var count = GetCount();
            if (count <= ConcatBufferLimit)
            {
                var buffer = new string[count];
                CopyTo(buffer, 0);
                return String.Concat(buffer);
            }
            using (var writer = new StringWriter(FormatProvider))
            {
                CopyToInternal(writer);
                return writer.ToString();
            }
        }

        public StringBlockWriter(IFormatProvider formatProvider)
            : base(formatProvider)
        {
        }
    }
}
