using org.whispersystems.curve25519;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using System.IO;
using System.Linq;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;

namespace Sharp.Xmpp.Extensions
{
    public static class OlmUtils
    {
        public static byte[] Hkdf(byte[] salt, byte[] ikm, byte[] info, int length)
        {
            var hkdf = new HkdfBytesGenerator(new Sha256Digest());
            hkdf.Init(new HkdfParameters(ikm, salt, info));

            var buffer = new byte[length];
            hkdf.GenerateBytes(buffer, 0, length);

            return buffer;
        }

        public static byte[] Hmac(byte[] key, byte[] input)
        {
            var mac = new byte[32];
            var hmac = new HMac(new Sha256Digest());
            hmac.Init(new KeyParameter(key));
            hmac.BlockUpdate(input, 0, input.Length);
            hmac.DoFinal(mac, 0);
            return mac;
        }

        public static byte[] Agreement(byte[] publicKey, byte[] privateKey)
        {
            var curve = Curve25519.getInstance(Curve25519.BEST);
            return curve.calculateAgreement(privateKey, publicKey);
        }

        public static byte[] SenderTripleDh(byte[] myIdentityKey, byte[] myEphemeralKey, byte[] theirIdentityKey, byte[] theirEphemeralKey)
        {
            using (var stream = new MemoryStream())
            {
                var agreement1 = Agreement(theirIdentityKey, myEphemeralKey);
                stream.Write(agreement1, 0, agreement1.Length);                

                var agreement2 = Agreement(theirEphemeralKey, myIdentityKey);
                stream.Write(agreement2, 0, agreement2.Length);

                var agreement3 = Agreement(theirEphemeralKey, myEphemeralKey);                
                stream.Write(agreement3, 0, agreement3.Length);

                return stream.ToArray();
            }
        }

        public static byte[] ReceiverTripleDh(byte[] myIdentityKey, byte[] myEphemeralKey, byte[] theirIdentityKey, byte[] theirEphemeralKey)
        {
            using (var stream = new MemoryStream())
            {
                var agreement1 = Agreement(theirEphemeralKey, myIdentityKey);
                stream.Write(agreement1, 0, agreement1.Length);

                var agreement2 = Agreement(theirIdentityKey, myEphemeralKey);
                stream.Write(agreement2, 0, agreement2.Length);

                var agreement3 = Agreement(theirEphemeralKey, myEphemeralKey);
                stream.Write(agreement3, 0, agreement3.Length);

                return stream.ToArray();
            }
        }

        public static void ComputeNextRootAndChainKey(byte[] currentRootKey, byte[] publicRatchetKey, byte[] privateRatchetKey, out byte[] nextRootKey, out byte[] nextChainKey)
        {
            var buffer = Hkdf(currentRootKey, Agreement(publicRatchetKey, privateRatchetKey), Encoding.UTF8.GetBytes("OLM_RATCHET"), 64);
            nextRootKey = buffer.Take(32).ToArray();
            nextChainKey = buffer.Skip(32).ToArray();
        }

        public static void ComputeCipherAndAuthenticationKeys(byte[] messageKey, out byte[] cipherKey, out byte[] cipherIv, out byte[] hmacKey)
        {
            var buffer = Hkdf(new byte[80], messageKey, Encoding.UTF8.GetBytes("OLM_KEYS"), 80);
            cipherKey = buffer.Take(32).ToArray();
            hmacKey = buffer.Skip(32).Take(32).ToArray();
            cipherIv = buffer.Skip(64).ToArray();
        }

        public static byte[] Encrypt(byte[] key, byte[] iv, byte[] input)
        {
            return ExecuteCipher(GetCipher(true, key, iv), input);
        }

        public static byte[] Decrypt(byte[] key, byte[] iv, byte[] input)
        {
            return ExecuteCipher(GetCipher(false, key, iv), input);
        }

        private static IBufferedCipher GetCipher(bool encrypting, byte[] key, byte[] iv)
        {
            var engine = new AesEngine();
            var cbc = new CbcBlockCipher(engine);
            var padding = new Pkcs7Padding();
            var cipher = new PaddedBufferedBlockCipher(cbc, padding);
            var cipherParams = new ParametersWithIV(new KeyParameter(key), iv);
            cipher.Init(encrypting, cipherParams);

            return cipher;
        }

        private static byte[] ExecuteCipher(IBufferedCipher cipher, byte[] input)
        {
            var cipherText = new byte[cipher.GetOutputSize(input.Length)];
            var cipherLength = cipher.ProcessBytes(input, 0, input.Length, cipherText, 0);
            cipher.DoFinal(cipherText, cipherLength);
            return cipherText;
        }
    }
}