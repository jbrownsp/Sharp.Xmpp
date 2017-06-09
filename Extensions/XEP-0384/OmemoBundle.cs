using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Sharp.Xmpp.Extensions
{
    [Serializable]
    public class OmemoBundle
    {
        public OmemoBundle()
        {
            PreKeys = new List<KeyPair>();
        }

        public KeyPair IdentityKey { get; set; }
        public List<KeyPair> PreKeys { get; set; }
        public Guid DeviceId { get; set; }

        public static OmemoBundle Generate()
        {
            var bundle = new OmemoBundle();
            bundle.IdentityKey = KeyPair.Generate();
            bundle.DeviceId = Guid.NewGuid();

            foreach (var i in Enumerable.Range(0, 10))
            {
                bundle.PreKeys.Add(KeyPair.Generate());
            }

            return bundle;
        }

        public static OmemoBundle FromXml(XmlElement xml)
        {
            var bundle = new OmemoBundle();

            var namespaceManager = new XmlNamespaceManager(xml.OwnerDocument.NameTable);
            namespaceManager.AddNamespace("o", Omemo.Namespace);

            bundle.DeviceId = Guid.Parse(xml.SelectSingleNode(".//o:deviceId", namespaceManager).InnerText);

            var identityKey = xml.SelectSingleNode(".//o:identityKey", namespaceManager);
            bundle.IdentityKey = new KeyPair(null, Convert.FromBase64String(identityKey.InnerText));
            bundle.PreKeys = xml.SelectNodes(".//o:preKeyPublic", namespaceManager).Cast<XmlElement>().Select(el => new KeyPair(null, Convert.FromBase64String(el.InnerText))).ToList();

            return bundle;
        }

        public XmlElement ToXml()
        {
            var bundle = Xml.Element("bundle", Omemo.Namespace);
            bundle.Child(Xml.Element("deviceId").Text(DeviceId.ToString()));
            bundle.Child(Xml.Element("identityKey").Text(Convert.ToBase64String(IdentityKey.PublicKey)));

            // todo signed prekey + signature

            var prekeys = Xml.Element("prekeys");

            for (var i = 0; i < PreKeys.Count; i++)
            {
                prekeys.Child(Xml.Element("preKeyPublic").Attr("preKeyId", i.ToString()).Text(Convert.ToBase64String(PreKeys[i].PublicKey)));   
            }

            bundle.Child(prekeys);

            return bundle;
        }
    }
}