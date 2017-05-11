using System;

namespace Sharp.Xmpp.Im
{
    /// <summary>
    /// Provides data for the Message event.
    /// </summary>
    public class MessageEventArgs : EventArgs
    {
        /// <summary>
        /// The JID of the user or resource who sent the message.
        /// </summary>
        public Jid Jid
        {
            get;
            private set;
        }

        /// <summary>
        /// The received chat message.
        /// </summary>
        public Message Message
        {
            get;
            private set;
        }

        public byte[] AesKey { get; private set; }
        public byte[] AesIv { get; private set; }

        /// <summary>
        /// Initializes a new instance of the MessageEventArgs class.
        /// </summary>
        /// <exception cref="ArgumentNullException">The jid parameter or the message
        /// parameter is null.</exception>
        public MessageEventArgs(Jid jid, Message message)
        {
            jid.ThrowIfNull("jid");
            message.ThrowIfNull("message");
            Jid = jid;
            Message = message;
        }

        public MessageEventArgs(Jid jid, Message message, byte[] aesKey, byte[] aesIv)
        {
            jid.ThrowIfNull("jid");
            message.ThrowIfNull("message");
            Jid = jid;
            Message = message;
            AesKey = aesKey;
            AesIv = aesIv;
        }
    }
}