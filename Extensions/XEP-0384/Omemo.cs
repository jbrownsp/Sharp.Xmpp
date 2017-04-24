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
        private object _lock = new object();
        
        public Omemo(XmppIm im) : base(im)
        {
        }

        public override void Initialize()
        {
            _pep = im.GetExtension<Pep>();
            _pep.Subscribe(Namespace + ":devicelist", OnDeviceListUpdated);
            _pep.Subscribe(Namespace + ":bundles:(.*)", OnBundleUpdated, true);
        }

        private void OnDeviceListUpdated(Jid jid, XmlElement el)
        {
            Debug.WriteLine(string.Format("received device list update from {0}", jid));

            try
            {
                var namespaceManager = new XmlNamespaceManager(el.OwnerDocument.NameTable);
                namespaceManager.AddNamespace("o", Namespace);

                var devices = el.SelectNodes(".//o:device", namespaceManager);

                foreach (var device in devices.Cast<XmlElement>())
                {
                    var bareJid = jid.GetBareJid();
                    var deviceId = Guid.Parse(device.GetAttribute("id"));
                    var bundleNodeId = string.Format("{0}:bundles:{1}", Namespace, deviceId);

                    lock (_lock)
                    {
                        Store.SaveDeviceId(bareJid, deviceId);
                    }

                    _pep.Subscribe(bundleNodeId, OnBundleUpdated);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("error parsing device list");
                Debug.WriteLine(e);
            }
        }

        private void OnBundleUpdated(Jid jid, XmlElement el)
        {
            Debug.WriteLine(string.Format("bundled updated by {0}", jid));

            if (jid == im.Jid)
            {
                Debug.WriteLine("ignoring our own bundle update");
                return;
            }

            try
            {
                var bundle = OmemoBundle.FromXml(el);

                lock (_lock)
                {
                    Store.SaveBundle(bundle.DeviceId, bundle);    
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("error parsing bundle");
                Debug.WriteLine(e);
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
            lock (_lock)
            {
                var list = Xml.Element("list", "urn:xmpp:omemo:0");

                var deviceIds = new[] { Store.GetCurrentDeviceId() };

                foreach (var deviceId in deviceIds)
                {
                    list.Child(Xml.Element("device").Attr("id", deviceId.ToString()));
                }

                _pep.Publish("urn:xmpp:omemo:0:devicelist", null, list);
            }
        }

        public void PublishBundles()
        {
            lock (_lock)
            {
                var deviceIds = Store.GetDeviceIds(im.Jid.GetBareJid());

                foreach (var deviceId in deviceIds)
                {
                    var bundle = Store.GetBundle(deviceId);
                    _pep.Publish("urn:xmpp:omemo:0:bundles:" + deviceId, null, bundle.ToXml());
                }
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
