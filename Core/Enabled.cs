using System.Xml;

namespace Sharp.Xmpp.Core
{
    public class Enabled : StreamManagementStanza
    {
        public Enabled()
        {
        }

        public Enabled(XmlElement element) : base(element)
        {
        }
    }
}