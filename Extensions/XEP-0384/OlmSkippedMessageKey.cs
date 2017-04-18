using System;

namespace Sharp.Xmpp.Extensions
{
    [Serializable]
    public class OlmSkippedMessageKey
    {
        public DateTime Timestamp { get; set; }
        public byte[] Key { get; set; }
    }
}