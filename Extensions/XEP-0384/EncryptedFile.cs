using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp.Xmpp.Extensions.XEP_0384
{
    public class EncryptedFile
    {
        public byte[] AesKey;
        public byte[] AesIv;
        public byte[] File;
    }
}
