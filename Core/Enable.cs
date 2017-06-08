using System.Xml;

namespace Sharp.Xmpp.Core
{
    public class Enable : StreamManagementStanza
    {
        public Enable()
        {
        }

        public Enable(XmlElement element) : base(element)
        {
        }
    }
}