using System;

namespace Sharp.Xmpp.Core
{
    public class AnswerEventArgs : EventArgs
    {
        public Answer Stanza
        {
            get;
            private set;
        }

        public AnswerEventArgs(Answer stanza)
        {
            Stanza = stanza;
        }
    }
}