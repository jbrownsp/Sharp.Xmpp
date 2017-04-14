using System;

namespace Sharp.Xmpp.Extensions
{
    public class OmemoHeaderKey
    {
        public Guid RecipientDeviceId { get; set; }
        public bool PreKey { get; set; }
        public byte[] Key { get; set; }

        public string ToXmlString()
        {
            var key = Xml.Element("key");
            key.Attr("rid", RecipientDeviceId.ToString());

            if (PreKey)
            {
                key.Attr("prekey", "true");
            }

            key.Text(Convert.ToBase64String(Key));

            return key.ToXmlString();
        }
    }
}