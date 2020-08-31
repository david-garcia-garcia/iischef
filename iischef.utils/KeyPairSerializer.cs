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
    /// 
    /// </summary>
    public static class KeyPairSerializer
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="privateKey"></param>
        /// <returns></returns>
        public static string SerializePrivateKey(AsymmetricKeyParameter privateKey)
        {
            PrivateKeyInfo privateKeyInfo = PrivateKeyInfoFactory.CreatePrivateKeyInfo(privateKey);
            byte[] serializedPrivateBytes = privateKeyInfo.ToAsn1Object().GetDerEncoded();
            string serializedPrivate = Convert.ToBase64String(serializedPrivateBytes);
            return serializedPrivate;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="publicKey"></param>
        /// <returns></returns>
        public static string SerializePublicKey(AsymmetricKeyParameter publicKey)
        {
            SubjectPublicKeyInfo publicKeyInfo = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(publicKey);
            byte[] serializedPublicBytes = publicKeyInfo.ToAsn1Object().GetDerEncoded();
            string serializedPublic = Convert.ToBase64String(serializedPublicBytes);
            return serializedPublic;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="publicKey"></param>
        /// <returns></returns>
        public static RsaKeyParameters UnserializePublic(string publicKey)
        {
            RsaKeyParameters result = (RsaKeyParameters)PublicKeyFactory.CreateKey(Convert.FromBase64String(publicKey));
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="privateKey"></param>
        /// <returns></returns>
        public static RsaPrivateCrtKeyParameters UnserializePrivate(string privateKey)
        {
            RsaPrivateCrtKeyParameters result = (RsaPrivateCrtKeyParameters)PrivateKeyFactory.CreateKey(Convert.FromBase64String(privateKey));
            return result;
        }

        /// <summary>
        /// Serialize a pair of public/private keys
        /// </summary>
        /// <param name="pair"></param>
        public static string Serialize(AsymmetricCipherKeyPair pair)
        {
            var store = new KeyPairStore()
            {
                PublicKey = SerializePublicKey(pair.Public),
                PrivateKey = SerializePrivateKey(pair.Private)
            };

            return store.SerializeToJson();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serialized"></param>
        /// <returns></returns>
        public static AsymmetricCipherKeyPair Unserialize(string serialized)
        {
            KeyPairStore store = JsonConvert.DeserializeObject<KeyPairStore>(serialized);

            RsaPrivateCrtKeyParameters privateKey = (RsaPrivateCrtKeyParameters)PrivateKeyFactory.CreateKey(Convert.FromBase64String(store.PrivateKey));
            RsaKeyParameters publicKey = (RsaKeyParameters)PublicKeyFactory.CreateKey(Convert.FromBase64String(store.PublicKey));

            return new AsymmetricCipherKeyPair(publicKey, privateKey);
        }
    }
}
