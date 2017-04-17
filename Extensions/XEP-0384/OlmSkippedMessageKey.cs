using System;

namespace Sharp.Xmpp.Extensions
{
    public class OlmSkippedMessageKey
    {
        public DateTime Timestamp { get; set; }
        public byte[] Key { get; set; }
    }
}