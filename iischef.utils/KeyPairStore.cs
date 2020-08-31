using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using System;

namespace iischef.utils
{
    /// <summary>
    /// Store
    /// </summary>
    public class KeyPairStore
    {
        public string PublicKey { get; set; }

        public string PrivateKey { get; set; }
    }
}
