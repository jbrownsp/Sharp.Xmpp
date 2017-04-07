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
    class Program
    {
        static void Main(string[] args)
        {
            Debug.Listeners.Add(new ConsoleTraceListener());

            // alice will send first
            var aliceBundle = OmemoBundle.Generate();
            var aliceEphemeralKey = KeyPair.Generate();

            // bob will receive first
            var bobBundle = OmemoBundle.Generate();
            var bobEphemeralKey = bobBundle.PreKeys.First(); // this would be determined by which of bob's prekeys alice chose when she sends the prekey message

            // calculate secrets -- this would be done with data from a prekey message
            Debug.WriteLine("calculating secrets");

            // sender secret
            var aliceSecret = OlmUtils.SenderTripleDh(aliceBundle.IdentityKey.PrivateKey, aliceEphemeralKey.PrivateKey, bobBundle.IdentityKey.PublicKey, bobEphemeralKey.PublicKey);
            Debug.WriteLine(string.Format("aliceSecret = {0}", Convert.ToBase64String(aliceSecret)));

            // recvr secret
            var bobSecret = OlmUtils.ReceiverTripleDh(bobBundle.IdentityKey.PrivateKey, bobEphemeralKey.PrivateKey, aliceBundle.IdentityKey.PublicKey, aliceEphemeralKey.PublicKey);
            Debug.WriteLine(string.Format("bobSecret = {0}", Convert.ToBase64String(bobSecret)));

            // setup sending session
            Debug.WriteLine("creating sender's session");
            var aliceSessionState = OlmSessionState.InitializeAsSender(aliceSecret, aliceBundle.IdentityKey, aliceEphemeralKey, bobEphemeralKey.PublicKey);
            var aliceSession = new OlmSession(aliceSessionState);
            // todo create prekey message


            // setup recv session
            Debug.WriteLine("creating recvr's session");
            var bobSessionState = OlmSessionState.InitializeAsReceiver(bobSecret, aliceSessionState.MyRatchetKey.PublicKey);
            var bobSession = new OlmSession(bobSessionState);
            // todo decode prekey message and generate bobSecret down here

            var messages = new List<byte[]>();

            for (var i = 0; i < 10; i++)
            {
                messages.Add(aliceSession.CreateMessage(Encoding.UTF8.GetBytes($"message {i}")));
                Debug.WriteLine($"{i} => {Convert.ToBase64String(messages[i])}");
            }

            foreach (var message in messages)
            {
                var buffer = bobSession.ReadMessage(message);
                var text = Encoding.UTF8.GetString(buffer);
                Debug.WriteLine($"recvd => {text}");
            }

            Console.WriteLine("Done");
            Console.ReadLine();
        }
    }
}
