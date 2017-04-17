using Sharp.Xmpp;
using Sharp.Xmpp.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

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

            var messages = new List<Tuple<OlmSession, string, OlmSession, string, byte[]>>();

            // normal messages can now be sent between the sessions
            for (var i = 0; i < 100; i++)
            {
                OlmSession sender;
                string senderName;

                OlmSession receiver;
                string receiverName;

                if (i % 4 == 0)
                {
                    sender = aliceSession;
                    senderName = "alice";

                    receiver = bobSession;
                    receiverName = "bob";
                }
                else
                {
                    sender = bobSession;
                    senderName = "bob";

                    receiver = aliceSession;
                    receiverName = "alice";
                }

                var message = $"message {i}";
                var sent = sender.CreateMessage(Encoding.UTF8.GetBytes(message));
                messages.Add(new Tuple<OlmSession, string, OlmSession, string, byte[]>(sender, senderName, receiver, receiverName, sent));
            }

            messages.Shuffle();

            foreach (var message in messages)
            {
                Debug.WriteLine(string.Format("{0} => {1} : {2}", message.Item2, message.Item4, Encoding.UTF8.GetString(message.Item3.ReadMessage(message.Item5))));
            }

            Console.WriteLine("Done");
            Console.ReadLine();
        }
    }

    public static class ListExtensions
    {
        public static void Shuffle<T>(this IList<T> list)
        {
            if (list.Count <= 1)
            {
                return;
            }

            var random = new Random();

            for (var i = list.Count - 1; i >= 1; --i)
            {
                var j = random.Next(0, i + 1);
                var temp = list[j];
                list[j] = list[i];
                list[i] = temp;
            }
        }
    }
}
