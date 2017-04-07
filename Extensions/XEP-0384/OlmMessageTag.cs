namespace Sharp.Xmpp.Extensions
{
    public enum OlmMessageTag
    {
        OneTimeKey = 0x0a,
        BaseKey = 0x12,
        IdentityKey = 0x1a,
        Message = 0x22,
        RatchetKey = 0x0a,
        ChainIndex = 0x10,
        CipherText = 0x22
    }
}