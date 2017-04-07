using System.IO;
using System.Text;

namespace Sharp.Xmpp.Extensions
{
    public class OlmWriter : BinaryWriter
    {
        public OlmWriter(Stream output) : base(output)
        {
        }

        public OlmWriter(Stream output, Encoding encoding) : base(output, encoding)
        {
        }

        public OlmWriter(Stream output, Encoding encoding, bool leaveOpen) : base(output, encoding, leaveOpen)
        {
        }

        public void WriteOlmTag(OlmMessageTag tag)
        {
            Write((byte) tag);
        }

        public void WriteOlmString(byte[] buffer)
        {
            Write(buffer.Length);
            Write(buffer);
        }
    }
}