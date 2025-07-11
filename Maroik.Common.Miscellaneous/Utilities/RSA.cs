﻿using System.Security.Cryptography;
using System.Text;

namespace Maroik.Common.Miscellaneous.Utilities
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
        public static readonly string publicKey = @"MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAoQh0wEqx/R2H1v00IU12Oc30fosRC/frhH89L6G+fzeaqI19MYQhEPMU13wpeqRONCUta+2iC1sgCNQ9qGGf19yGdZUfueaB1Nu9rdueQKXgVurGHJ+5N71UFm+OP1XcnFUCK4wT5d7ZIifXxuqLehP9Ts6sNjhVfa+yU+VjF5HoIe69OJEPo7OxRZcRTe17khc93Ic+PfyqswQJJlY/bgpcLJQnM+QuHmxNtF7/FpAx9YEQsShsGpVo7JaKgLo+s6AFoJ4QldQKir2vbN9vcKRbG3piElPilWDpjXQkOJZhUloh/jd7QrKFimZFldJ1r6Q59QYUyGKZARUe0KZpMQIDAQAB";
        public static readonly string privateKey = @"MIIEpAIBAAKCAQEAoQh0wEqx/R2H1v00IU12Oc30fosRC/frhH89L6G+fzeaqI19MYQhEPMU13wpeqRONCUta+2iC1sgCNQ9qGGf19yGdZUfueaB1Nu9rdueQKXgVurGHJ+5N71UFm+OP1XcnFUCK4wT5d7ZIifXxuqLehP9Ts6sNjhVfa+yU+VjF5HoIe69OJEPo7OxRZcRTe17khc93Ic+PfyqswQJJlY/bgpcLJQnM+QuHmxNtF7/FpAx9YEQsShsGpVo7JaKgLo+s6AFoJ4QldQKir2vbN9vcKRbG3piElPilWDpjXQkOJZhUloh/jd7QrKFimZFldJ1r6Q59QYUyGKZARUe0KZpMQIDAQABAoIBAQCRZLUlOUvjIVqYvhznRK1OG6p45s8JY1r+UnPIId2Bt46oSLeUkZvZVeCnfq9k0Bzb8AVGwVPhtPEDh73z3dEYcT/lwjLXAkyPB6gG5ZfI/vvC/k7JYV01+neFmktw2/FIJWjEMMF2dvLNZ/Pm4bX1Dz9SfD/45Hwr8wqrvRzvFZsj5qqOxv9RPAudOYwCwZskKp/GF+L+3Ycod1Wu98imzMZUH+L5dQuDGg3kvf3ljIAegTPoqYBg0imNPYY/EGoFKnbxlK5S5/5uAFb16dGJqAz3XQCz9Is/IWrOTu0etteqV2Ncs8uqPdjed+b0j8CMsr4U1xjwPQ8WwdaJtTkRAoGBANAndgiGZkCVcc9975/AYdgFp35W6D+hGQAZlL6DmnucUFdXbWa/x2rTSEXlkvgk9X/PxOptUYsLJkzysTgfDywZwuIXLm9B3oNmv3bVgPXsgDsvDfaHYCgz0nHK6NSrX2AeX3yO/dFuoZsuk+J+UyRigMqYj0wjmxUlqj183hinAoGBAMYMOBgF77OXRII7GAuEut/nBeh2sBrgyzR7FmJMs5kvRh6Ck8wp3ysgMvX4lxh1ep8iCw1R2cguqNATr1klOdsCTOE9RrhuvOp3JrYzuIAK6MpH/uBICy4w1rW2+gQySsHcH40r+tNaTFQ7dQ1tef//iy/IW8v8i0t+csztE1JnAoGABdtWYt8FOYP688+jUmdjWWSvVcq0NjYeMfaGTOX/DsNTL2HyXhW/Uq4nNnBDNmAz2CjMbZwt0y+5ICkj+2REVQVUinAEinTcAe5+LKXNPx4sbX3hcrJUbk0m+rSu4G0B/f5cyXBsi9wFCAzDdHgBduCepxSr04Sc9Hde1uQQi7kCgYB0U20HP0Vh+TG2RLuE2HtjVDD2L/CUeQEiXEHzjxXWnhvTg+MIAnggvpLwQwmMxkQ2ACr5sd/3YuCpB0bxV5o594nsqq9FWVYBaecFEjAGlWHSnqMoXWijwu/6X/VOTbP3VjH6G6ECT4GR4DKKpokIQrMgZ9DzaezvdOA9WesFdQKBgQCWfeOQTitRJ0NZACFUn3Fs3Rvgc9eN9YSWj4RtqkmGPMPvguWo+SKhlk3IbYjrRBc5WVOdoX8JXb2/+nAGhPCuUZckWVmZe5pMSr4EkNQdYeY8kOXGSjoTOUH34ZdKeS+e399BkBWIiXUejX/Srln0H4KoHnTWgxwNpTsBCgXu8Q==";
        private readonly System.Security.Cryptography.RSA? _privateKeyRsaProvider;
        private readonly System.Security.Cryptography.RSA? _publicKeyRsaProvider;
        private readonly HashAlgorithmName _hashAlgorithmName;
        private readonly Encoding _encoding;

        /// <summary>
        /// 实例化RSAHelper
        /// </summary>
        /// <param name="rsaType">加密算法类型 RSA SHA1;RSA2 SHA256 密钥长度至少为2048</param>
        /// <param name="encoding">编码类型</param>
        /// <param name="privateKey">私钥</param>
        /// <param name="publicKey">公钥</param>
        public RSA(RSAType rsaType, Encoding encoding, string privateKey, string? publicKey = null)
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

            byte[]? signatureBytes = _privateKeyRsaProvider?.SignData(dataBytes, _hashAlgorithmName, RSASignaturePadding.Pkcs1);

            return signatureBytes != null ? Convert.ToBase64String(signatureBytes) : string.Empty;
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

            bool verify = _publicKeyRsaProvider?.VerifyData(dataBytes, signBytes, _hashAlgorithmName, RSASignaturePadding.Pkcs1) ?? false;

            return verify;
        }

        #endregion

        #region 解密

        public string Decrypt(string cipherText)
        {
            return _privateKeyRsaProvider == null
                ? throw new Exception("_privateKeyRsaProvider is null")
                : Encoding.UTF8.GetString(_privateKeyRsaProvider.Decrypt(Convert.FromBase64String(cipherText), RSAEncryptionPadding.Pkcs1));
        }

        #endregion

        #region 加密

        public string Encrypt(string text)
        {
            return _publicKeyRsaProvider == null
                ? throw new Exception("_publicKeyRsaProvider is null")
                : Convert.ToBase64String(_publicKeyRsaProvider.Encrypt(Encoding.UTF8.GetBytes(text), RSAEncryptionPadding.Pkcs1));
        }

        #endregion

        #region 使用私钥创建RSA实例

        public System.Security.Cryptography.RSA CreateRsaProviderFromPrivateKey(string privateKey)
        {
            byte[] privateKeyBits = Convert.FromBase64String(privateKey);

            System.Security.Cryptography.RSA rsa = System.Security.Cryptography.RSA.Create();
            RSAParameters rsaParameters = new();

            using (BinaryReader binr = new(new MemoryStream(privateKeyBits)))
            {
                byte bt = 0;
                ushort twobytes = 0;
                twobytes = binr.ReadUInt16();
                _ = twobytes == 0x8130
                    ? binr.ReadByte()
                    : twobytes == 0x8230 ? binr.ReadInt16() : throw new Exception("Unexpected value read binr.ReadUInt16()");

                twobytes = binr.ReadUInt16();
                if (twobytes != 0x0102)
                {
                    throw new Exception("Unexpected version");
                }

                bt = binr.ReadByte();
                if (bt != 0x00)
                {
                    throw new Exception("Unexpected value read binr.ReadByte()");
                }

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

        public System.Security.Cryptography.RSA? CreateRsaProviderFromPublicKey(string publicKeyString)
        {
            // encoded OID sequence for  PKCS #1 rsaEncryption szOID_RSA_RSA = "1.2.840.113549.1.1.1"
            byte[] seqOid = { 0x30, 0x0D, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01, 0x05, 0x00 };
            _ = new byte[15];

            byte[] x509Key = Convert.FromBase64String(publicKeyString);

            // ---------  Set up stream to read the asn.1 encoded SubjectPublicKeyInfo blob  ------
            using MemoryStream mem = new(x509Key);
            using BinaryReader binr = new(mem);  //wrap Memory Stream with BinaryReader for easy reading
            byte bt = 0;
            ushort twobytes = 0;

            twobytes = binr.ReadUInt16();
            if (twobytes == 0x8130) //data read as little endian order (actual data order for Sequence is 30 81)
            {
                _ = binr.ReadByte();    //advance 1 byte
            }
            else if (twobytes == 0x8230)
            {
                _ = binr.ReadInt16();   //advance 2 bytes
            }
            else
            {
                return null;
            }

            byte[] seq = binr.ReadBytes(15);
            if (!CompareBytearrays(seq, seqOid))    //make sure Sequence for OID is correct
            {
                return null;
            }

            twobytes = binr.ReadUInt16();
            if (twobytes == 0x8103) //data read as little endian order (actual data order for Bit String is 03 81)
            {
                _ = binr.ReadByte();    //advance 1 byte
            }
            else if (twobytes == 0x8203)
            {
                _ = binr.ReadInt16();   //advance 2 bytes
            }
            else
            {
                return null;
            }

            bt = binr.ReadByte();
            if (bt != 0x00)     //expect null byte next
            {
                return null;
            }

            twobytes = binr.ReadUInt16();
            if (twobytes == 0x8130) //data read as little endian order (actual data order for Sequence is 30 81)
            {
                _ = binr.ReadByte();    //advance 1 byte
            }
            else if (twobytes == 0x8230)
            {
                _ = binr.ReadInt16();   //advance 2 bytes
            }
            else
            {
                return null;
            }

            twobytes = binr.ReadUInt16();
            byte lowbyte = 0x00;
            byte highbyte = 0x00;

            if (twobytes == 0x8102) //data read as little endian order (actual data order for Integer is 02 81)
            {
                lowbyte = binr.ReadByte();  // read next bytes which is bytes in modulus
            }
            else if (twobytes == 0x8202)
            {
                highbyte = binr.ReadByte(); //advance 2 bytes
                lowbyte = binr.ReadByte();
            }
            else
            {
                return null;
            }

            byte[] modint = { lowbyte, highbyte, 0x00, 0x00 };   //reverse byte order since asn.1 key uses big endian order
            int modsize = BitConverter.ToInt32(modint, 0);

            int firstbyte = binr.PeekChar();
            if (firstbyte == 0x00)
            {   //if first byte (highest order) of modulus is zero, don't include it
                _ = binr.ReadByte();    //skip this null byte
                modsize -= 1;   //reduce modulus buffer size by 1
            }

            byte[] modulus = binr.ReadBytes(modsize);   //read the modulus bytes

            if (binr.ReadByte() != 0x02)            //expect an Integer for the exponent data
            {
                return null;
            }

            int expbytes = binr.ReadByte();        // should only need one byte for actual exponent data (for all useful values)
            byte[] exponent = binr.ReadBytes(expbytes);

            // ------- create RSACryptoServiceProvider instance and initialize with public key -----
            System.Security.Cryptography.RSA rsa = System.Security.Cryptography.RSA.Create();
            RSAParameters rsaKeyInfo = new()
            {
                Modulus = modulus,
                Exponent = exponent
            };
            rsa.ImportParameters(rsaKeyInfo);

            return rsa;
        }

        #endregion

        #region 导入密钥算法

        private int GetIntegerSize(BinaryReader binr)
        {
            byte bt = binr.ReadByte();
            if (bt != 0x02)
            {
                return 0;
            }

            bt = binr.ReadByte();
            int count;
            if (bt == 0x81)
            {
                count = binr.ReadByte();
            }
            else
            if (bt == 0x82)
            {
                byte highbyte = binr.ReadByte();
                byte lowbyte = binr.ReadByte();
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
            _ = binr.BaseStream.Seek(-1, SeekOrigin.Current);
            return count;
        }

        private bool CompareBytearrays(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            int i = 0;
            foreach (byte c in a)
            {
                if (c != b[i])
                {
                    return false;
                }

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