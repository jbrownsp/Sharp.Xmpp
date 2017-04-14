using System.Collections.Generic;

namespace Sharp.Xmpp.Extensions
{
    public class OmemoHeader
    {
        public IList<OmemoHeaderKey> Keys { get; set; }

        public OmemoHeader()
        {
            Keys = new List<OmemoHeaderKey>();
        }
    }
}