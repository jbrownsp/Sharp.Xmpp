using System.IO;
using System.Text;

namespace Sharp.Xmpp.Extensions
{
    public class OlmPreKeyMessage
    {
        public byte Version { get; set; }
        public byte[] OneTimeKey { get; set; }
        public byte[] BaseKey { get; set; }
        public byte[] IdentityKey { get; set; }
        public byte[] Message { get; set; }

        public byte[] Serialize()
        {
            using (var stream = new MemoryStream())
            using (var writer = new OlmWriter(stream, Encoding.UTF8))
            {
                writer.Write(Version);

                writer.WriteOlmTag(OlmMessageTag.OneTimeKey);
                writer.WriteOlmString(OneTimeKey);

                writer.WriteOlmTag(OlmMessageTag.BaseKey);
                writer.WriteOlmString(BaseKey);

                writer.WriteOlmTag(OlmMessageTag.IdentityKey);
                writer.WriteOlmString(IdentityKey);

                writer.WriteOlmTag(OlmMessageTag.Message);
                writer.WriteOlmString(Message);

                return stream.ToArray();
            }
        }

        public static OlmPreKeyMessage Deserialize(byte[] buffer)
        {
            using (var stream = new MemoryStream(buffer))
            using (var reader = new OlmReader(stream))
            {
                var message = new OlmPreKeyMessage();
                message.Version = reader.ReadByte();

                reader.ReadOlmTag();
                message.OneTimeKey = reader.ReadOlmString();

                reader.ReadOlmTag();
                message.BaseKey = reader.ReadOlmString();

                reader.ReadOlmTag();
                message.IdentityKey = reader.ReadOlmString();

                reader.ReadOlmTag();
                message.Message = reader.ReadOlmString();

                return message;
            }
        }
    }
}