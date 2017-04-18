using Sharp.Xmpp.Im;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml;

namespace Sharp.Xmpp.Extensions
{
    internal class Omemo : XmppExtension, IInputFilter<Message>
    {
        public static string Namespace = "urn:xmpp:omemo:0";

        private Pep _pep;
        
        public Omemo(XmppIm im) : base(im)
        {
        }

        public override void Initialize()
        {
            _pep = im.GetExtension<Pep>();
            _pep.Subscribe("urn:xmpp:omemo:0:devicelist", OnDeviceListUpdated);
            PublishDeviceList();
            PublishBundles();
        }

        private void OnDeviceListUpdated(Jid jid, XmlElement xmlElement)
        {
            var devices = xmlElement.SelectNodes("./device");

            foreach (var device in devices.Cast<XmlElement>())
            {
                Store.SaveDeviceId(jid.GetBareJid(), Guid.Parse(device.GetAttribute("id")));
            }
        }

        public override IEnumerable<string> Namespaces
        {
            get
            {
                return new []{ Namespace };
            }
        }

        public override Extension Xep
        {
            get
            {
                return Extension.Omemo;
            }
        }

        public bool Input(Message stanza)
        {
            // todo find session and decrypt
            return false;
        }

        public void PublishDeviceList()
        {
            var list = Xml.Element("list", "urn:xmpp:omemo:0");

            var deviceIds = Store.GetDeviceIds(im.Jid.GetBareJid());

            foreach (var deviceId in deviceIds)
            {
                list.Child(Xml.Element("device").Attr("id", deviceId.ToString()));
            }

            _pep.Publish("urn:xmpp:omemo:0:devicelist", null, list);
        }

        public void PublishBundles()
        {
            var deviceIds = Store.GetDeviceIds(im.Jid.GetBareJid());

            foreach (var deviceId in deviceIds)
            {
                var bundle = Store.GetBundle(deviceId);
                _pep.Publish("urn:xmpp:omemo:0:bundles:" + deviceId, null, bundle.ToXml());
            }
        }

        public string Encrypt(IEnumerable<Jid> recipients, string message)
        {
            return message; // todo return original message until pubsub works!

            var aesKey = "AES_KEY";
            var aesIv = "";
            var payload = "";

            var encrypted = Xml.Element("encrypted", "urn:xmpp:omemo:0");
            var header = Xml.Element("header");
            header.Attr("sid", Store.GetCurrentDeviceId().ToString());

            // iv
            header.Child(Xml.Element("iv").Text(Convert.ToBase64String(Encoding.UTF8.GetBytes(aesIv))));

            // keys
            foreach (var recipient in recipients)
            {
                var deviceIds = Store.GetDeviceIds(recipient);

                foreach (var deviceId in deviceIds)
                {
                    var sessionState = Store.GetSession(deviceId);
                    var prekey = false;

                    // if no existing session exists create session as sender
                    if (sessionState == null)
                    {
                        var senderBundle = Store.GetCurrentDeviceBundle();
                        var recipientBundle = Store.GetBundle(deviceId);
                        
                        if (recipientBundle == null)
                        {
                            Debug.WriteLine("no bundle found for recipient = {0}, device = {1}", recipient, deviceId);
                            continue;
                        }

                        var senderEphemeralKey = KeyPair.Generate();
                        var recipientEphemeralKey = recipientBundle.PreKeys[new Random().Next(0, recipientBundle.PreKeys.Count)];
                        var secret = OlmUtils.SenderTripleDh(senderBundle.IdentityKey.PrivateKey, senderEphemeralKey.PrivateKey, recipientBundle.IdentityKey.PublicKey, recipientEphemeralKey.PublicKey);
                        sessionState = OlmSessionState.InitializeAsSender(secret, senderBundle.IdentityKey, senderEphemeralKey, recipientEphemeralKey.PublicKey);
                        Store.SaveSession(deviceId, sessionState);
                        prekey = true;
                    }

                    var session = new OlmSession(sessionState);
                    var key = Xml.Element("key");
                    key.Attr("rid", deviceId.ToString());

                    if (prekey)
                    {
                        key.Attr("prekey", "true");
                        key.Text(Convert.ToBase64String(session.CreatePreKeyMessage(Encoding.UTF8.GetBytes(aesKey))));
                    }
                    else
                    {
                        key.Text(Convert.ToBase64String(session.CreateMessage(Encoding.UTF8.GetBytes(aesKey))));
                    }

                    header.Child(key);
                }
            }

            encrypted.Child(Xml.Element("payload").Text(Convert.ToBase64String(Encoding.UTF8.GetBytes(message))));

            return encrypted.ToXmlString();
        }

        public IOmemoStore Store { get; set; }
    }
}
