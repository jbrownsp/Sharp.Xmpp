using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using System.IO;
using System.Linq;
using System.Text;

namespace Sharp.Xmpp.Extensions
{
    /// <summary>
    /// Implementation of OLM double ratchet https://matrix.org/docs/spec/olm.html
    /// </summary>
    public class OlmSession
    {
        private const byte OlmVersion = 0x03;
        private readonly OlmSessionState _state;
        
        public OlmSessionState SessionState
        {
            get { return _state; }
        }

        public bool IsEstablished
        {
            get { return _state.TheirRatchetKey != null; }
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
            if (IsEstablished && (_state.Ratchet || _state.SendChainKey == null))
            {
                _state.MyRatchetKey = KeyPair.Generate();

                var keys = OlmUtils.Hkdf(_state.RootKey, OlmUtils.Agreement(_state.TheirRatchetKey, _state.MyRatchetKey.PrivateKey), Encoding.UTF8.GetBytes("OLM_RATCHET"), 64);
                _state.RootKey = keys.Take(32).ToArray();
                _state.SendChainKey = keys.Skip(32).ToArray();
                _state.SendChainIndex = 0;
                _state.Ratchet = false;
            }

            var messageKey = ComputeMessageKey(_state.SendChainKey);

            // Compute aes key/ic, and hmac key
            //
            // HKDF(salt,  IKM,  info,  L)
            // AES_KEYi, j ∥ HMAC_KEYi, j ∥ AES_IVi, j	 = HKDF(0,  Mi, j, "OLM_KEYS",  80)
            var buffer = OlmUtils.Hkdf(new byte[80], messageKey, Encoding.UTF8.GetBytes("OLM_KEYS"), 80);
            var aesKey = buffer.Take(32).ToArray();
            var hmacKey = buffer.Skip(32).Take(32).ToArray();
            var aesIv = buffer.Skip(64).ToArray();

            // encrypt input
            var engine = new AesEngine();
            var cbc = new CbcBlockCipher(engine);
            var padding = new Pkcs7Padding();
            var cipher = new PaddedBufferedBlockCipher(cbc, padding);
            var cipherParams = new ParametersWithIV(new KeyParameter(aesKey), aesIv);
            cipher.Init(true, cipherParams);

            var cipherText = new byte[cipher.GetOutputSize(input.Length)];
            var cipherLength = cipher.ProcessBytes(input, 0, input.Length, cipherText, 0);
            cipher.DoFinal(cipherText, cipherLength);

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

            // non prekey messages with new ratchet keys require us to compute new keys
            if (!prekey && (_state.TheirRatchetKey == null || !message.RatchetKey.SequenceEqual(_state.TheirRatchetKey)))
            {
                // Ri∥ Ci, 0 = HKDF(Ri − 1, ECDH(Ti − 1, Ti), "OLM_RATCHET", 64)
                var keys = OlmUtils.Hkdf(_state.RootKey, OlmUtils.Agreement(message.RatchetKey, _state.MyRatchetKey.PrivateKey), Encoding.UTF8.GetBytes("OLM_RATCHET"), 64);
                _state.RootKey = keys.Take(32).ToArray();
                _state.RecvChainKey = keys.Skip(32).ToArray();
                _state.RecvChainIndex = 0;
                _state.TheirRatchetKey = message.RatchetKey;
                _state.Ratchet = true;
            }

            while (_state.RecvChainIndex < message.SendChainIndex)
            {
                _state.RatchetReceiveChain();
            }

            var messageKey = ComputeMessageKey(_state.RecvChainKey);

            // Compute aes key/ic, and hmac key
            //
            // HKDF(salt,  IKM,  info,  L)
            // AES_KEYi, j ∥ HMAC_KEYi, j ∥ AES_IVi, j	 = HKDF(0,  Mi, j, "OLM_KEYS",  80)
            var buffer = OlmUtils.Hkdf(new byte[80], messageKey, Encoding.UTF8.GetBytes("OLM_KEYS"), 80);
            var aesKey = buffer.Take(32).ToArray();
            var hmacKey = buffer.Skip(32).Take(32).ToArray();
            var aesIv = buffer.Skip(64).ToArray();

            // encrypt input
            var engine = new AesEngine();
            var cbc = new CbcBlockCipher(engine);
            var padding = new Pkcs7Padding();
            var cipher = new PaddedBufferedBlockCipher(cbc, padding);
            var cipherParams = new ParametersWithIV(new KeyParameter(aesKey), aesIv);
            cipher.Init(false, cipherParams);

            var cipherText = new byte[cipher.GetOutputSize(message.CipherText.Length)];
            var cipherLength = cipher.ProcessBytes(message.CipherText, 0, message.CipherText.Length, cipherText, 0);
            cipher.DoFinal(cipherText, cipherLength);

            return cipherText;
        }

        private byte[] ComputeMessageKey(byte[] chainKey)
        {
            return OlmUtils.Hmac(chainKey, new byte[] { 0x01 });
        }
    }
}