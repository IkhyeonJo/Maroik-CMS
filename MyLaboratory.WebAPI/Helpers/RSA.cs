using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MyLaboratory.WebAPI.Helpers
{
    #region Code Example
    //string plaintext = "plaintext";

    //var rsa = new RSA(RSAType.RSA2, Encoding.UTF8, RSA.privateKey, RSA.publicKey);

    //var aa = rsa.Encrypt(plaintext);
    //var bb = rsa.Decrypt(aa);
    //var cc = rsa.Sign(plaintext);
    //var dd = rsa.Verify(plaintext, cc);
    #endregion
    /// <summary>
    /// RSA加解密 使用OpenSSL的公钥加密/私钥解密
    /// 
    /// 公私钥请使用openssl生成  ssh-keygen -t rsa 命令生成的公钥私钥是不行的
    /// 
    /// 作者：李志强
    /// 时间：2017年10月30日15:50:14
    /// QQ:501232752
    /// </summary>
    public class RSA
    {
        public static readonly string publicKey = @"MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAuraNTpiLGdKQYJp5P8lX
lBihAcoGIKSU1L9xD+6g7+9F9AFGLKUU5U/6irxGQgwZIcawD8BatH4Ol8HYuM/D
SZ5Iq2JXI+OQN9XK2oNBT2wLNFfFRmMKt8NuUyimNZJhHSiFXGrQNTmxjn9q//Ci
3OHwvEeOYbBqja0ICCazHL1ydD2Ilk5FTfiCT6NnVAWdjUvEnevadE6YsqVatsnJ
ThqXpmdT9jo8XBkCNPgJ5U71POE88M60Z71olgKy4cuJF2EbwDX+naxXttiaNgYN
nn5j4BncmknNrCdP/eyoqtIgnQiMDrZEB7+aFwX+nsfxLsKVKqWcjdjgv3XVN8HD
rwIDAQAB";
        public static readonly string privateKey = @"MIIEpQIBAAKCAQEAuraNTpiLGdKQYJp5P8lXlBihAcoGIKSU1L9xD+6g7+9F9AFG
LKUU5U/6irxGQgwZIcawD8BatH4Ol8HYuM/DSZ5Iq2JXI+OQN9XK2oNBT2wLNFfF
RmMKt8NuUyimNZJhHSiFXGrQNTmxjn9q//Ci3OHwvEeOYbBqja0ICCazHL1ydD2I
lk5FTfiCT6NnVAWdjUvEnevadE6YsqVatsnJThqXpmdT9jo8XBkCNPgJ5U71POE8
8M60Z71olgKy4cuJF2EbwDX+naxXttiaNgYNnn5j4BncmknNrCdP/eyoqtIgnQiM
DrZEB7+aFwX+nsfxLsKVKqWcjdjgv3XVN8HDrwIDAQABAoIBAAjWjzA7Kri/H/G6
ssHjR8Q2XfS4OaArzIQGbLoF+3hm68KPBuGP+8IEe/OpGtqRpsBTnrvBSPwqp65S
41tIMdJgW6PS6V4OLaJjjGyQhLPXYO+KSOxehtKRBuKI+mkmWFJXO8OyFBlrLDat
XPLDQMXAUQ6QhNc40M75Bgo8Kigt0f1xcAi9w9ftPNusjX/isa5ZJzpUKppQjqNv
51h2Bty8DoaVHBxTh2oyVho9+2VIEBWyQN/LjKkQFL3RYntOLiVPquCTlrJJODHT
6mmmbx5gxhZM5rnO6j3reKVs/3GWb122hh660X55spYCggxsBHEtduVU8w/k+nHE
QfsWXxECgYEA27rgN/kGJyHnfh0OpMbn+AriJJYt7fp69i5pPHRdZIwXt73LsPxA
kpG1tIx5Ir1ej8hdNPRtFH5ot7vItC5iD2ie5O83SWbF6fDcytDn0RPHUT0LwWI7
PEJheP4SlpYjZ1MziNgulq0UCMaQTqyUuAvaqioRkfyuaPgsg1Z7d58CgYEA2Yh7
dfVX0chQil4STWqxtWKt0rG0PS2CqU0tdwdTKBIwEDInMD9CJQGfEM3CUte/wsQL
boPb2tEYv8MIjrD/55Wm79toRBZpQz3HQmOyy20OtTRXZq96TsjeDHJVOjPu0Uc5
WuOZy7i0ncsXS5sBLFdYKj4TW4EWqIXOcryEefECgYEAzDCfSKSLyTXCj/mksA9/
ZYuHl4RP7kTEm52JgfqJB3UAXWlsrm3b+9iYSM35jAT6qRcDNsCl1VObMJK68mXg
Dn2kmw6KDBYLeVixdXAo3KxKnv3hvLXyYg810WAaGgIvqEczLsnmkXMJmRYc6F+M
XHP3ogYyCb4MvdNSWkK1vfUCgYEA0pYBjwvX3EojThALf86OAp5oz9MgPSlVmDgS
j2wT4HLa/JLQxTA4BEMxjb7jI/eguBe5SV908rwBTYKtQhWvZq3FUeYBZgicFnAz
+d0yNSR6XIkzI0E1Ehf6feog/5tO0mI07/vt4v4IO8lj39lKpXZY72vwqWCqYrJG
TWSaisECgYEAsE076eW5jss8bkANMoyUtUqLKz6/eOscp+sabbPclpvwrAl6CEgL
P8WGiIU4AKvZ/CRUQXDnVu6ZgM8bOa0cNu7rpQouefU72/SXtCyeubQzH0HYz4q3
UJGwvsObGSwUfhFISDcxew/exQS5mfJdBLKJI1kf60cjqy5UxWqY/CE=";
        private readonly System.Security.Cryptography.RSA _privateKeyRsaProvider;
        private readonly System.Security.Cryptography.RSA _publicKeyRsaProvider;
        private readonly HashAlgorithmName _hashAlgorithmName;
        private readonly Encoding _encoding;

        /// <summary>
        /// 实例化RSAHelper
        /// </summary>
        /// <param name="rsaType">加密算法类型 RSA SHA1;RSA2 SHA256 密钥长度至少为2048</param>
        /// <param name="encoding">编码类型</param>
        /// <param name="privateKey">私钥</param>
        /// <param name="publicKey">公钥</param>
        public RSA(RSAType rsaType, Encoding encoding, string privateKey, string publicKey = null)
        {
            _encoding = encoding;
            if (!string.IsNullOrEmpty(privateKey))
            {
                _privateKeyRsaProvider = CreateRsaProviderFromPrivateKey(privateKey);
            }

            if (!string.IsNullOrEmpty(publicKey))
            {
                _publicKeyRsaProvider = CreateRsaProviderFromPublicKey(publicKey);
            }

            _hashAlgorithmName = rsaType == RSAType.RSA ? HashAlgorithmName.SHA1 : HashAlgorithmName.SHA256;
        }

        #region 使用私钥签名

        /// <summary>
        /// 使用私钥签名
        /// </summary>
        /// <param name="data">原始数据</param>
        /// <returns></returns>
        public string Sign(string data)
        {
            byte[] dataBytes = _encoding.GetBytes(data);

            var signatureBytes = _privateKeyRsaProvider.SignData(dataBytes, _hashAlgorithmName, RSASignaturePadding.Pkcs1);

            return Convert.ToBase64String(signatureBytes);
        }

        #endregion

        #region 使用公钥验证签名

        /// <summary>
        /// 使用公钥验证签名
        /// </summary>
        /// <param name="data">原始数据</param>
        /// <param name="sign">签名</param>
        /// <returns></returns>
        public bool Verify(string data, string sign)
        {
            byte[] dataBytes = _encoding.GetBytes(data);
            byte[] signBytes = Convert.FromBase64String(sign);

            var verify = _publicKeyRsaProvider.VerifyData(dataBytes, signBytes, _hashAlgorithmName, RSASignaturePadding.Pkcs1);

            return verify;
        }

        #endregion

        #region 解密

        public string Decrypt(string cipherText)
        {
            if (_privateKeyRsaProvider == null)
            {
                throw new Exception("_privateKeyRsaProvider is null");
            }
            return Encoding.UTF8.GetString(_privateKeyRsaProvider.Decrypt(Convert.FromBase64String(cipherText), RSAEncryptionPadding.Pkcs1));
        }

        #endregion

        #region 加密

        public string Encrypt(string text)
        {
            if (_publicKeyRsaProvider == null)
            {
                throw new Exception("_publicKeyRsaProvider is null");
            }
            return Convert.ToBase64String(_publicKeyRsaProvider.Encrypt(Encoding.UTF8.GetBytes(text), RSAEncryptionPadding.Pkcs1));
        }

        #endregion

        #region 使用私钥创建RSA实例

        public System.Security.Cryptography.RSA CreateRsaProviderFromPrivateKey(string privateKey)
        {
            var privateKeyBits = Convert.FromBase64String(privateKey);

            var rsa = System.Security.Cryptography.RSA.Create();
            var rsaParameters = new RSAParameters();

            using (BinaryReader binr = new BinaryReader(new MemoryStream(privateKeyBits)))
            {
                byte bt = 0;
                ushort twobytes = 0;
                twobytes = binr.ReadUInt16();
                if (twobytes == 0x8130)
                    binr.ReadByte();
                else if (twobytes == 0x8230)
                    binr.ReadInt16();
                else
                    throw new Exception("Unexpected value read binr.ReadUInt16()");

                twobytes = binr.ReadUInt16();
                if (twobytes != 0x0102)
                    throw new Exception("Unexpected version");

                bt = binr.ReadByte();
                if (bt != 0x00)
                    throw new Exception("Unexpected value read binr.ReadByte()");

                rsaParameters.Modulus = binr.ReadBytes(GetIntegerSize(binr));
                rsaParameters.Exponent = binr.ReadBytes(GetIntegerSize(binr));
                rsaParameters.D = binr.ReadBytes(GetIntegerSize(binr));
                rsaParameters.P = binr.ReadBytes(GetIntegerSize(binr));
                rsaParameters.Q = binr.ReadBytes(GetIntegerSize(binr));
                rsaParameters.DP = binr.ReadBytes(GetIntegerSize(binr));
                rsaParameters.DQ = binr.ReadBytes(GetIntegerSize(binr));
                rsaParameters.InverseQ = binr.ReadBytes(GetIntegerSize(binr));
            }

            rsa.ImportParameters(rsaParameters);
            return rsa;
        }

        #endregion

        #region 使用公钥创建RSA实例

        public System.Security.Cryptography.RSA CreateRsaProviderFromPublicKey(string publicKeyString)
        {
            // encoded OID sequence for  PKCS #1 rsaEncryption szOID_RSA_RSA = "1.2.840.113549.1.1.1"
            byte[] seqOid = { 0x30, 0x0D, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01, 0x05, 0x00 };
            byte[] seq = new byte[15];

            var x509Key = Convert.FromBase64String(publicKeyString);

            // ---------  Set up stream to read the asn.1 encoded SubjectPublicKeyInfo blob  ------
            using (MemoryStream mem = new MemoryStream(x509Key))
            {
                using (BinaryReader binr = new BinaryReader(mem))  //wrap Memory Stream with BinaryReader for easy reading
                {
                    byte bt = 0;
                    ushort twobytes = 0;

                    twobytes = binr.ReadUInt16();
                    if (twobytes == 0x8130) //data read as little endian order (actual data order for Sequence is 30 81)
                        binr.ReadByte();    //advance 1 byte
                    else if (twobytes == 0x8230)
                        binr.ReadInt16();   //advance 2 bytes
                    else
                        return null;

                    seq = binr.ReadBytes(15);       //read the Sequence OID
                    if (!CompareBytearrays(seq, seqOid))    //make sure Sequence for OID is correct
                        return null;

                    twobytes = binr.ReadUInt16();
                    if (twobytes == 0x8103) //data read as little endian order (actual data order for Bit String is 03 81)
                        binr.ReadByte();    //advance 1 byte
                    else if (twobytes == 0x8203)
                        binr.ReadInt16();   //advance 2 bytes
                    else
                        return null;

                    bt = binr.ReadByte();
                    if (bt != 0x00)     //expect null byte next
                        return null;

                    twobytes = binr.ReadUInt16();
                    if (twobytes == 0x8130) //data read as little endian order (actual data order for Sequence is 30 81)
                        binr.ReadByte();    //advance 1 byte
                    else if (twobytes == 0x8230)
                        binr.ReadInt16();   //advance 2 bytes
                    else
                        return null;

                    twobytes = binr.ReadUInt16();
                    byte lowbyte = 0x00;
                    byte highbyte = 0x00;

                    if (twobytes == 0x8102) //data read as little endian order (actual data order for Integer is 02 81)
                        lowbyte = binr.ReadByte();  // read next bytes which is bytes in modulus
                    else if (twobytes == 0x8202)
                    {
                        highbyte = binr.ReadByte(); //advance 2 bytes
                        lowbyte = binr.ReadByte();
                    }
                    else
                        return null;
                    byte[] modint = { lowbyte, highbyte, 0x00, 0x00 };   //reverse byte order since asn.1 key uses big endian order
                    int modsize = BitConverter.ToInt32(modint, 0);

                    int firstbyte = binr.PeekChar();
                    if (firstbyte == 0x00)
                    {   //if first byte (highest order) of modulus is zero, don't include it
                        binr.ReadByte();    //skip this null byte
                        modsize -= 1;   //reduce modulus buffer size by 1
                    }

                    byte[] modulus = binr.ReadBytes(modsize);   //read the modulus bytes

                    if (binr.ReadByte() != 0x02)            //expect an Integer for the exponent data
                        return null;
                    int expbytes = (int)binr.ReadByte();        // should only need one byte for actual exponent data (for all useful values)
                    byte[] exponent = binr.ReadBytes(expbytes);

                    // ------- create RSACryptoServiceProvider instance and initialize with public key -----
                    var rsa = System.Security.Cryptography.RSA.Create();
                    RSAParameters rsaKeyInfo = new RSAParameters
                    {
                        Modulus = modulus,
                        Exponent = exponent
                    };
                    rsa.ImportParameters(rsaKeyInfo);

                    return rsa;
                }

            }
        }

        #endregion

        #region 导入密钥算法

        private int GetIntegerSize(BinaryReader binr)
        {
            byte bt = 0;
            int count = 0;
            bt = binr.ReadByte();
            if (bt != 0x02)
                return 0;
            bt = binr.ReadByte();

            if (bt == 0x81)
                count = binr.ReadByte();
            else
            if (bt == 0x82)
            {
                var highbyte = binr.ReadByte();
                var lowbyte = binr.ReadByte();
                byte[] modint = { lowbyte, highbyte, 0x00, 0x00 };
                count = BitConverter.ToInt32(modint, 0);
            }
            else
            {
                count = bt;
            }

            while (binr.ReadByte() == 0x00)
            {
                count -= 1;
            }
            binr.BaseStream.Seek(-1, SeekOrigin.Current);
            return count;
        }

        private bool CompareBytearrays(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;
            int i = 0;
            foreach (byte c in a)
            {
                if (c != b[i])
                    return false;
                i++;
            }
            return true;
        }

        #endregion

    }

    /// <summary>
    /// RSA算法类型
    /// </summary>
    public enum RSAType
    {
        /// <summary>
        /// SHA1
        /// </summary>
        RSA = 0,
        /// <summary>
        /// RSA2 密钥长度至少为2048
        /// SHA256
        /// </summary>
        RSA2
    }
}