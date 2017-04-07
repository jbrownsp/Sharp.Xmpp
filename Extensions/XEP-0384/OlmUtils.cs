using org.whispersystems.curve25519;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using System.IO;

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
    }
}