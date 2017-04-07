using System;
using System.IO;
using System.Text;

namespace Sharp.Xmpp.Extensions
{
    public class OlmReader : BinaryReader
    {
        public OlmReader(Stream input) : base(input)
        {
        }

        public OlmReader(Stream input, Encoding encoding) : base(input, encoding)
        {
        }

        public OlmReader(Stream input, Encoding encoding, bool leaveOpen) : base(input, encoding, leaveOpen)
        {
        }

        public OlmMessageTag ReadOlmTag()
        {
            return (OlmMessageTag) ReadByte();
        }

        public byte[] ReadOlmString()
        {
            var length = ReadInt32();
            return ReadBytes(length);
        }
    }
}