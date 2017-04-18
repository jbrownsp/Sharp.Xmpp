using System;
using System.Collections.Generic;

namespace Sharp.Xmpp.Extensions
{
    public interface IOmemoStore
    {
        Guid GetCurrentDeviceId();
        OmemoBundle GetCurrentDeviceBundle();

        void SaveDeviceId(Jid jid, Guid deviceId);
        IList<Guid> GetDeviceIds(Jid jid);

        void SaveBundle(Guid deviceId, OmemoBundle bundle);
        OmemoBundle GetBundle(Guid deviceId);

        void SaveSession(Guid deviceId, OlmSessionState sessionState);
        OlmSessionState GetSession(Guid deviceId);
    }
}