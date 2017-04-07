using Sharp.Xmpp.Im;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

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

        public void SendMessage(IEnumerable<Jid> recipients, string message)
        {
            var aesKey = "AES_KEY";
            var aesIv = "";
            var payload = "";

            var encrypted = Xml.Element("encrypted", "urn:xmpp:omemo:0");
            var header = Xml.Element("header");
            header.Attr("sid", DeviceId.ToString());

            // iv
            header.Child(Xml.Element("iv").Text(Convert.ToBase64String(Encoding.ASCII.GetBytes(aesIv))));

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
                        var senderBundle = Store.GetBundle(DeviceId);
                        var recipientBundle = Store.GetBundle(deviceId);
                        
                        if (recipientBundle == null)
                        {
                            Debug.WriteLine("no bundle found for recipient = {0}, device = {1}", recipient, deviceId);
                            continue;
                        }

                        //sessionState = OmemoSessionState.InitializeAsSender(senderBundle.IdentityKey, senderBundle.IdentityKey, recipientBundle.IdentityKey.PublicKey, recipientBundle.PreKeys.First().PublicKey); // todo pick prekey at random
                        Store.SaveSession(deviceId, sessionState);
                        prekey = true;
                    }

                    var session = new OlmSession(sessionState);
                    var key = Xml.Element("key");
                    key.Attr("rid", deviceId.ToString());

                    if (prekey)
                    {
                        key.Attr("prekey", "true");
                        key.Text(Convert.ToBase64String(session.CreatePreKeyMessage(Encoding.ASCII.GetBytes(aesKey))));
                    }
                    else
                    {
                        key.Text(Convert.ToBase64String(session.CreateMessage(Encoding.ASCII.GetBytes(aesKey))));
                    }

                    header.Child(key);
                }
            }

            encrypted.Child(Xml.Element("payload").Text(Convert.ToBase64String(Encoding.UTF8.GetBytes(message))));

            im.SendMessage(recipients.First(), encrypted.ToString());
        }

        public Guid DeviceId { get; set; }

        public IOmemoStore Store { get; set; }
    }
}
