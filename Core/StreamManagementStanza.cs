using System.Xml;

namespace Sharp.Xmpp.Core
{
    public class StreamManagementStanza : Stanza
    {
        public StreamManagementStanza() : base("urn:xmpp:sm:3")
        {
        }

        public StreamManagementStanza(XmlElement element) : base(element)
        {
        }
    }
}