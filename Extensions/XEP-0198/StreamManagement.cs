using System;
using System.Collections.Generic;
using Sharp.Xmpp.Core;
using Sharp.Xmpp.Im;

namespace Sharp.Xmpp.Extensions
{
    internal class StreamManagement : XmppExtension, IOutputFilter<Iq>, IOutputFilter<Message>
    {
        private bool _enabled = false;
        private int _count = 0;

        public StreamManagement(XmppIm im) : base(im)
        {
        }

        public override void Initialize()
        {
            im.StreamManagementEnabled += (sender, args) => _enabled = true;
        }

        public void Enable()
        {
            im.Send(new Enable());
        }

        void IOutputFilter<Iq>.Output(Iq stanza)
        {
            throw new NotImplementedException();
        }

        void IOutputFilter<Core.Message>.Output(Core.Message stanza)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<string> Namespaces
        {
            get
            {
                return new[] { "urn:xmpp:sm:3" };
            }            
        }
        public override Extension Xep
        {
            get
            {
                return Extension.StreamManagement;
            }
        }
    }
}