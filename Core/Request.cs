using System.Xml;

namespace Sharp.Xmpp.Core
{
    public class Request : StreamManagementStanza
    {
        public Request()
        {
        }

        public Request(XmlElement element) : base(element)
        {
        }
    }
}