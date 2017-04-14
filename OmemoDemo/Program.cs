using Sharp.Xmpp;
using Sharp.Xmpp.Client;
using Sharp.Xmpp.Extensions;
using Sharp.Xmpp.Im;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml;

namespace OmemoDemo
{
    class InMemoryOmemoStore : IOmemoStore
    {
        private readonly Dictionary<Jid, List<Guid>> _devices = new Dictionary<Jid, List<Guid>>();
        private readonly Dictionary<Guid, OmemoBundle> _bundles = new Dictionary<Guid, OmemoBundle>();
        private readonly Dictionary<Guid, OlmSessionState> _sessionState = new Dictionary<Guid, OlmSessionState>();

        public void SaveDeviceId(Jid jid, Guid deviceId)
        {
            if (!_devices.ContainsKey(jid))
            {
                _devices[jid] = new List<Guid>();
            }

            _devices[jid].Add(deviceId);
        }

        public IList<Guid> GetDeviceIds(Jid jid)
        {
            return _devices.ContainsKey(jid) ? _devices[jid] : new List<Guid>();
        }

        public void SaveBundle(Guid deviceId, OmemoBundle bundle)
        {
            _bundles[deviceId] = bundle;
        }

        public OmemoBundle GetBundle(Guid deviceId)
        {
            return _bundles.ContainsKey(deviceId) ? _bundles[deviceId] : null;
        }

        public void SaveSession(Guid deviceId, OlmSessionState sessionState)
        {
            _sessionState[deviceId] = sessionState;
        }

        public OlmSessionState GetSession(Guid deviceId)
        {
            return _sessionState.ContainsKey(deviceId) ? _sessionState[deviceId] : null;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Debug.Listeners.Add(new ConsoleTraceListener());

            // bob will receive first
            var bobStore = new InMemoryOmemoStore();
            var bobJid = new Jid("openfire.local", "bob");
            var bobDeviceId = Guid.NewGuid();
            var bobBundle = OmemoBundle.Generate();
            

            // alice will send first
            var aliceStore = new InMemoryOmemoStore();
            var aliceJid = new Jid("openfire.local", "alice");
            var aliceDeviceId = Guid.NewGuid();
            var aliceBundle = OmemoBundle.Generate();
            var aliceEphemeralKey = KeyPair.Generate();

            // alice picks one of bob's prekeys at random
            var bobEphemeralKey = bobBundle.PreKeys[new Random().Next(0, bobBundle.PreKeys.Count)];

            /*
            // alice and bob would publish their information and they would store eachother's device ids and bundles in their own stores
            aliceStore.SaveDeviceId(bobJid, bobDeviceId);
            aliceStore.SaveBundle(bobDeviceId, bobBundle);

            bobStore.SaveDeviceId(aliceJid, aliceDeviceId);
            bobStore.SaveBundle(aliceDeviceId, aliceBundle);


            // alice will initiate the session with bob
            var input = "this is a test message";
            var recipientJids = new[] { bobJid };

            var header = new OmemoHeader();
            
            
            foreach (var jid in recipientJids)
            {
                
            }
            */

            // alice calculates the secret
            var aliceSecret = OlmUtils.SenderTripleDh(aliceBundle.IdentityKey.PrivateKey, aliceEphemeralKey.PrivateKey, bobBundle.IdentityKey.PublicKey, bobEphemeralKey.PublicKey);
            Debug.WriteLine(string.Format("aliceSecret = {0}", Convert.ToBase64String(aliceSecret)));

            // alice initiates the session
            Debug.WriteLine("creating sender's session");
            var aliceSessionState = OlmSessionState.InitializeAsSender(aliceSecret, aliceBundle.IdentityKey, aliceEphemeralKey, bobEphemeralKey.PublicKey);
            var aliceSession = new OlmSession(aliceSessionState);

            // alice sends initial prekey message
            var alicePreKeyMessage = aliceSession.CreatePreKeyMessage(Encoding.UTF8.GetBytes("hello bob"));

            // bob recvs prekey message
            var incomingPreKeyMessage = OlmPreKeyMessage.Deserialize(alicePreKeyMessage);
            KeyPair bobEphemeralKeyPair = null;

            // bob searches his prekeys to find the keypair alice used
            foreach (var prekey in bobBundle.PreKeys)
            {
                if (prekey.PublicKey.SequenceEqual(incomingPreKeyMessage.OneTimeKey))
                {
                    bobEphemeralKeyPair = prekey;
                    break;
                }
            }

            // bob calculates the secret
            var bobSecret = OlmUtils.ReceiverTripleDh(bobBundle.IdentityKey.PrivateKey, bobEphemeralKeyPair.PrivateKey, incomingPreKeyMessage.IdentityKey, incomingPreKeyMessage.BaseKey);
            Debug.WriteLine(string.Format("bobSecret = {0}", Convert.ToBase64String(bobSecret)));

            // bob sets up his session with the secret and prekey used by alice
            Debug.WriteLine("creating recvr's session");
            var bobSessionState = OlmSessionState.InitializeAsReceiver(bobSecret, aliceSessionState.MyRatchetKey.PublicKey);
            var bobSession = new OlmSession(bobSessionState);

            // bob reads the prekey's cipher text
            Debug.WriteLine(string.Format("recvd prekey message {0}", Encoding.UTF8.GetString(bobSession.ReadMessage(incomingPreKeyMessage.Message, true))));
            
            // normal messages can now be sent between the sessions
            var messages = new List<byte[]>();

            for (var i = 0; i < 100; i++)
            {
                OlmSession sender;
                string senderName;

                OlmSession receiver;
                string receiverName;

                if (true)
                {
                    sender = aliceSession;
                    senderName = "alice";

                    receiver = bobSession;
                    receiverName = "bob";
                }
                //else
                //{
                //    sender = bobSession;
                //    senderName = "bob";

                //    receiver = aliceSession;
                //    receiverName = "alice";
                //}

                var sent = sender.CreateMessage(Encoding.UTF8.GetBytes($"message {i}"));
                Debug.WriteLine($"{senderName} => {Convert.ToBase64String(sent)}");

                var recvd = Encoding.UTF8.GetString(receiver.ReadMessage(sent));
                Debug.WriteLine($"{receiverName} <= {recvd}");
            }

            Console.WriteLine("Done");
            Console.ReadLine();
        }
    }
}
