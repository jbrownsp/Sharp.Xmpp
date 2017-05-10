using System.Xml;

namespace Sharp.Xmpp.Core
{
    public class Answer : StreamManagementStanza
    {
        public Answer()
        {
        }

        public Answer(XmlElement element) : base(element)
        {
        }

        public int H
        {
            get
            {
                var value = element.GetAttribute("h");
                int h;
                return int.TryParse(value, out h) ? h : 0;
            }

            set
            {
                element.SetAttribute("h", value.ToString());
            }
        }

        protected override string RootElementName
        {
            get
            {
                return "a";
            }            
        }
    }
}