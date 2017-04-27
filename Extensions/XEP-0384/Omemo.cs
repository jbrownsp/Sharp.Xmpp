using Sharp.Xmpp.Im;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
                var bundle = Store.GetCurrentDeviceBundle();
                _pep.Publish("urn:xmpp:omemo:0:bundles:" + bundle.DeviceId, null, bundle.ToXml());
            }
        }

        public bool Input(Message stanza)
        {
            try
            {
                var namespaceManager = new XmlNamespaceManager(stanza.Data.OwnerDocument.NameTable);
                namespaceManager.AddNamespace("o", Namespace);

                // search for omemo encrypted element
                var encrypted = stanza.Data.SelectSingleNode(".//o:encrypted", namespaceManager);

                if (encrypted == null)
                {
                    return false;
                }

                Debug.WriteLine("found omemo encrypted message");

                var header = encrypted.SelectSingleNode("./o:header", namespaceManager);

                // get sender device id so we can locate their bundle if necessary
                var senderDeviceId = Guid.Parse(header.Attributes["sid"].Value);
                Debug.WriteLine("omemo sender device is " + senderDeviceId);

                // find the encrypted aes key for our device id
                var key = header.SelectSingleNode(string.Format("./o:key[@rid='{0}']", Store.GetCurrentDeviceId()), namespaceManager);

                if (key == null)
                {
                    Debug.WriteLine("received message that did not contain key for recipient's device id " + Store.GetCurrentDeviceId());
                    return true;
                }

                var state = Store.GetSession(senderDeviceId);
                var prekeyAttribute = key.Attributes["prekey"];
                var prekey = prekeyAttribute != null && bool.Parse(prekeyAttribute.Value);
                byte[] messageBuffer;
                OlmMessage message;
                
                if (state == null || prekey)
                {
                    var prekeyMessage = OlmPreKeyMessage.Deserialize(Convert.FromBase64String(key.InnerText));
                    var bundle = Store.GetCurrentDeviceBundle();
                    var ephemeralKey = bundle.PreKeys.FirstOrDefault(x => x.PublicKey.SequenceEqual(prekeyMessage.OneTimeKey));
                    var secret = OlmUtils.ReceiverTripleDh(bundle.IdentityKey.PrivateKey, ephemeralKey.PrivateKey, prekeyMessage.IdentityKey, prekeyMessage.BaseKey);
                    message = OlmMessage.Deserialize(prekeyMessage.Message.Take(prekeyMessage.Message.Length - 8).ToArray());
                    messageBuffer = prekeyMessage.Message;
                    state = OlmSessionState.InitializeAsReceiver(secret, message.RatchetKey);
                }
                else
                {
                    messageBuffer = Convert.FromBase64String(key.InnerText);
                }

                var session = new OlmSession(state);
                var aesKey = session.ReadMessage(messageBuffer, prekey).Take(32).ToArray();
                var iv = Convert.FromBase64String(encrypted.SelectSingleNode(".//o:iv", namespaceManager).InnerText);
                var payload = Convert.FromBase64String(encrypted.SelectSingleNode(".//o:payload", namespaceManager).InnerText);
                var originalMessage = string.Empty;

                using (var cipher = new RijndaelManaged())
                {
                    var transform = cipher.CreateDecryptor(aesKey, iv);

                    using (var stream = new MemoryStream(payload))
                    using (var cryptoStream = new CryptoStream(stream, transform, CryptoStreamMode.Read))
                    {
                        using (var reader = new StreamReader(cryptoStream))
                        {
                            originalMessage = reader.ReadToEnd();
                        }
                    }
                }

                Debug.WriteLine(originalMessage);

                // raise event with decrypted message
                stanza.Body = originalMessage;
                im.RaiseEncryptedMessage(stanza);

                return true;
            }
            catch (Exception e)
            {
                Debug.WriteLine("error parsing incoming omemo message");
                Debug.WriteLine(e);
                return false;
            }
        }

        public string Encrypt(IEnumerable<Jid> recipients, string message)
        {
            // generate key and iv, then encrypt message
            byte[] aesKey;
            byte[] aesIv;
            byte[] payload;

            using (var cipher = new RijndaelManaged())
            {
                cipher.GenerateKey();
                cipher.GenerateIV();

                aesKey = cipher.Key;
                aesIv = cipher.IV;

                var encryptor = cipher.CreateEncryptor(aesKey, aesIv);

                using (var stream = new MemoryStream())
                using (var cryptoStream = new CryptoStream(stream, encryptor, CryptoStreamMode.Write))
                {
                    using (var writer = new StreamWriter(cryptoStream))
                    {
                        writer.Write(message);                        
                    }

                    payload = stream.ToArray();
                }
            }

            var encrypted = Xml.Element("encrypted", "urn:xmpp:omemo:0");
            var header = Xml.Element("header");
            header.Attr("sid", Store.GetCurrentDeviceId().ToString());

            // iv
            header.Child(Xml.Element("iv").Text(Convert.ToBase64String(aesIv)));

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

                        // get sender and recipient one time keys
                        var senderEphemeralKey = KeyPair.Generate();
                        var recipientEphemeralKey = recipientBundle.PreKeys[new Random().Next(0, recipientBundle.PreKeys.Count)];

                        // perform triple-dh
                        var secret = OlmUtils.SenderTripleDh(senderBundle.IdentityKey.PrivateKey, senderEphemeralKey.PrivateKey, recipientBundle.IdentityKey.PublicKey, recipientEphemeralKey.PublicKey);

                        // create session
                        sessionState = OlmSessionState.InitializeAsSender(secret, senderBundle.IdentityKey, senderEphemeralKey, recipientEphemeralKey.PublicKey);
                        Store.SaveSession(deviceId, sessionState);
                        prekey = true;
                    }
                    else
                    {
                        prekey = !sessionState.IsEstablished;
                    }

                    var session = new OlmSession(sessionState);
                    var key = Xml.Element("key");
                    key.Attr("rid", deviceId.ToString());

                    // encrypt aes key for this session
                    if (prekey)
                    {
                        key.Attr("prekey", "true");
                        key.Text(Convert.ToBase64String(session.CreatePreKeyMessage(aesKey)));
                    }
                    else
                    {
                        key.Text(Convert.ToBase64String(session.CreateMessage(aesKey)));
                    }

                    header.Child(key);
                    encrypted.Child(header);
                }
            }

            encrypted.Child(Xml.Element("payload").Text(Convert.ToBase64String(payload)));

            return encrypted.ToXmlString();
        }

        public IOmemoStore Store { get; set; }
    }
}
