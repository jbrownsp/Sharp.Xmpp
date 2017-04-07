﻿using System;
using org.whispersystems.curve25519;

namespace Sharp.Xmpp.Extensions
{
    public class KeyPair
    {
        public KeyPair(byte[] privateKey, byte[] publicKey)
        {
            PublicKey = publicKey;
            PrivateKey = privateKey;
        }

        public byte[] PublicKey { get; set; }
        public byte[] PrivateKey { get; set; }

        public static KeyPair Generate()
        {
            var curve = Curve25519.getInstance(Curve25519.BEST);
            var curveImplKeyPair = curve.generateKeyPair();
            return new KeyPair(curveImplKeyPair.getPrivateKey(), curveImplKeyPair.getPublicKey());
        }

        public override string ToString()
        {
            return string.Format("{0}: {1}, {2}: {3}", nameof(PublicKey), Convert.ToBase64String(PublicKey), nameof(PrivateKey), Convert.ToBase64String(PrivateKey));
        }
    }
}