using System;

namespace Sharp.Xmpp.Core
{
    public class EnabledEventArgs : EventArgs
    {
        public Enabled Stanza
        {
            get;
            private set;
        }

        public EnabledEventArgs(Enabled stanza)
        {
            Stanza = stanza;
        }
    }
}