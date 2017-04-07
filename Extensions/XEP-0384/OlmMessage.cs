using System.IO;
using System.Text;

namespace Sharp.Xmpp.Extensions
{
    public class OlmMessage
    {
        public byte Version { get; set; }
        public byte[] RatchetKey { get; set; }
        public int SendChainIndex { get; set; }
        public byte[] CipherText { get; set; }

        public byte[] Serialize()
        {
            using (var stream = new MemoryStream())
            using (var writer = new OlmWriter(stream, Encoding.UTF8))
            {
                writer.Write(Version);

                writer.WriteOlmTag(OlmMessageTag.RatchetKey);
                writer.WriteOlmString(RatchetKey);

                writer.WriteOlmTag(OlmMessageTag.ChainIndex);
                writer.Write(SendChainIndex);

                writer.WriteOlmTag(OlmMessageTag.CipherText);
                writer.WriteOlmString(CipherText);

                return stream.ToArray();
            }
        }

        public static OlmMessage Deserialize(byte[] buffer)
        {
            using (var stream = new MemoryStream(buffer))
            using (var reader = new OlmReader(stream))
            {
                var message = new OlmMessage();

                message.Version = reader.ReadByte();

                reader.ReadOlmTag();
                message.RatchetKey = reader.ReadOlmString();

                reader.ReadOlmTag();
                message.SendChainIndex = reader.ReadInt32();

                reader.ReadOlmTag();
                message.CipherText = reader.ReadOlmString();

                return message;
            }
        }
    }
}