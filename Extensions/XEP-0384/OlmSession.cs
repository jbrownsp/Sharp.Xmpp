using System;
using System.IO;
using System.Linq;

namespace Sharp.Xmpp.Extensions
{
    /// <summary>
    /// Implementation of OLM double ratchet https://matrix.org/docs/spec/olm.html
    /// </summary>
    public class OlmSession
    {
        private const byte OlmVersion = 0x03;
        private readonly OlmSessionState _state;

        public bool IsEstablished
        {
            get
            {
                return _state.IsEstablished;
            }
        }
        
        public OlmSessionState SessionState
        {
            get { return _state; }
        }

        public OlmSession(OlmSessionState state)
        {
            _state = state;
        }

        public byte[] CreatePreKeyMessage(byte[] input)
        {
            var message = new OlmPreKeyMessage
            {
                Version = OlmVersion,
                OneTimeKey = _state.TheirEphemeralKey,
                BaseKey = _state.MyEphemeralKey.PublicKey,
                IdentityKey = _state.MyIdentityKey.PublicKey,
                Message = CreateMessage(input)
            };

            return message.Serialize();
        }

        public byte[] CreateMessage(byte[] input)
        {
            // ratchet root
            if (_state.TheirRatchetKey != null && (_state.Ratchet || _state.SendChainKey == null))
            {
                _state.MyRatchetKey = KeyPair.Generate();

                byte[] nextRootKey;
                byte[] nextChainKey;

                OlmUtils.ComputeNextRootAndChainKey(_state.RootKey, _state.TheirRatchetKey, _state.MyRatchetKey.PrivateKey, out nextRootKey, out nextChainKey);

                _state.RootKey = nextRootKey;
                _state.SendChainKey = nextChainKey;
                _state.SendChainIndex = 0;
                _state.Ratchet = false;
            }

            byte[] aesKey;
            byte[] aesIv;
            byte[] hmacKey;

            OlmUtils.ComputeCipherAndAuthenticationKeys(_state.ComputeSendChainMessageKey(), out aesKey, out aesIv, out hmacKey);
            var cipherText = OlmUtils.Encrypt(aesKey, aesIv, input);

            var message = new OlmMessage
            {
                Version = OlmVersion,
                CipherText = cipherText,
                RatchetKey = _state.MyRatchetKey.PublicKey,
                SendChainIndex = _state.SendChainIndex
            };

            var payload = message.Serialize();
            var hmac = OlmUtils.Hmac(hmacKey, payload);

            _state.RatchetSendChain();

            // create message payload + hmac
            using (var stream = new MemoryStream())
            {
                stream.Write(payload, 0, payload.Length);
                stream.Write(hmac, 0, 8); // truncate to 64 bits
                return stream.ToArray();
            }
        }

        public byte[] ReadMessage(byte[] input, bool prekey = false)
        {
            // deserialize buffer - hmac
            var message = OlmMessage.Deserialize(input.Take(input.Length - 8).ToArray());
            var hmac = input.Skip(input.Length - 8).ToArray();

            var output = TrySkippedMessageKeys(message.CipherText);

            if (output != null)
            {
                return output;
            }

            // non prekey messages with new ratchet keys require us to compute new root and chain keys
            if (!prekey && (_state.TheirRatchetKey == null || !message.RatchetKey.SequenceEqual(_state.TheirRatchetKey)))
            {
                byte[] nextRootKey;
                byte[] nextChainKey;

                OlmUtils.ComputeNextRootAndChainKey(_state.RootKey, message.RatchetKey, _state.MyRatchetKey.PrivateKey, out nextRootKey, out nextChainKey);

                _state.RootKey = nextRootKey;
                _state.RecvChainKey = nextChainKey;
                _state.RecvChainIndex = 0;
                _state.TheirRatchetKey = message.RatchetKey;
                _state.Ratchet = true;
            }

            while (_state.RecvChainIndex < message.SendChainIndex)
            {
                _state.RatchetReceiveChain();

                if (_state.RecvChainIndex < message.SendChainIndex - 1)
                {
                    _state.SkippedMessageKeys.Add(new OlmSkippedMessageKey
                    {
                        Timestamp = DateTime.UtcNow,
                        Key = _state.ComputeRecvChainMessageKey()
                    });
                }
            }

            return DecryptWithMessageKey(_state.ComputeRecvChainMessageKey(), message.CipherText);
        }

        private byte[] TrySkippedMessageKeys(byte[] input)
        {
            OlmSkippedMessageKey matchingKey = null;
            byte[] output = null;

            foreach (var key in _state.SkippedMessageKeys)
            {
                try
                {
                    output = DecryptWithMessageKey(key.Key, input);
                    matchingKey = key;
                }
                catch
                {
                    // todo log anything here?
                }
            }

            if (matchingKey != null)
            {
                _state.SkippedMessageKeys.Remove(matchingKey);
            }

            return output;
        }

        private byte[] DecryptWithMessageKey(byte[] messageKey, byte[] input)
        {
            byte[] aesKey;
            byte[] aesIv;
            byte[] hmacKey;
            OlmUtils.ComputeCipherAndAuthenticationKeys(messageKey, out aesKey, out aesIv, out hmacKey);
            return OlmUtils.Decrypt(aesKey, aesIv, input);
        }
    }
}