using System;

namespace Sharp.Xmpp.Core
{
    public class RequestEventArgs : EventArgs
    {
        public Request Stanza
        {
            get;
            private set;
        }

        public RequestEventArgs(Request stanza)
        {
            Stanza = stanza;
        }
    }
}