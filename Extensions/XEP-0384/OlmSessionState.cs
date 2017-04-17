using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Sharp.Xmpp.Extensions
{
    public class OlmSessionState
    {
        public KeyPair MyIdentityKey { get; set; }
        public KeyPair MyEphemeralKey { get; set; }
        public KeyPair MyRatchetKey { get; set; }

        public byte[] TheirEphemeralKey { get; set; }
        public byte[] TheirRatchetKey { get; set; }

        public byte[] RootKey { get; set; }

        public byte[] SendChainKey { get; set; }
        public int SendChainIndex { get; set; }

        public byte[] RecvChainKey { get; set; }
        public int RecvChainIndex { get; set; }

        public IList<OlmSkippedMessageKey> SkippedMessageKeys { get; set; }

        public bool Ratchet { get; set; }

        public static OlmSessionState InitializeAsSender(byte[] secret, KeyPair myIdentityKey, KeyPair myEphemeralKey, byte[] theirEphemeralKey)
        {
            var sessionState = new OlmSessionState
            {
                MyIdentityKey = myIdentityKey,
                MyEphemeralKey = myEphemeralKey,
                TheirEphemeralKey = theirEphemeralKey,
                MyRatchetKey = KeyPair.Generate()
            };

            var buffer = OlmUtils.Hkdf(new byte[64], secret, Encoding.UTF8.GetBytes("OLM_ROOT"), 64);
            sessionState.RootKey = buffer.Take(32).ToArray();
            sessionState.SendChainKey = buffer.Skip(32).ToArray();

            Debug.WriteLine(string.Format("R0 = {0}, CKs = {1}", Convert.ToBase64String(sessionState.RootKey), Convert.ToBase64String(sessionState.SendChainKey)));

            return sessionState;
        }

        public static OlmSessionState InitializeAsReceiver(byte[] secret, byte[] theirRatchetKey)
        {
            var sessionState = new OlmSessionState
            {
                TheirRatchetKey = theirRatchetKey
            };

            var buffer = OlmUtils.Hkdf(new byte[64], secret, Encoding.UTF8.GetBytes("OLM_ROOT"), 64);
            sessionState.RootKey = buffer.Take(32).ToArray();
            sessionState.RecvChainKey = buffer.Skip(32).ToArray();

            Debug.WriteLine(string.Format("R0 = {0}, CKr = {1}", Convert.ToBase64String(sessionState.RootKey), Convert.ToBase64String(sessionState.RecvChainKey)));

            return sessionState;
        }

        public OlmSessionState()
        {
            SkippedMessageKeys = new List<OlmSkippedMessageKey>();
        }

        public void RatchetSendChain()
        {
            SendChainKey = OlmUtils.Hmac(SendChainKey, new byte[] { 0x02 });
            ++SendChainIndex;
        }

        public void RatchetReceiveChain()
        {
            RecvChainKey = OlmUtils.Hmac(RecvChainKey, new byte[] { 0x02 });
            ++RecvChainIndex;
        }

        public byte[] ComputeSendChainMessageKey()
        {
            return ComputeMessageKey(SendChainKey);
        }

        public byte[] ComputeRecvChainMessageKey()
        {
            return ComputeMessageKey(RecvChainKey);
        }

        private byte[] ComputeMessageKey(byte[] chainKey)
        {
            return OlmUtils.Hmac(chainKey, new byte[] { 0x01 });
        }
    }
}