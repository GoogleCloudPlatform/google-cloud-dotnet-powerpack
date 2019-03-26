// Copyright 2019 Google LLC
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     https://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Google.Api.Gax;
using Google.Cloud.Kms.V1;
using Google.Protobuf;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using System;
using System.IO;
using System.Xml.Linq;

namespace Google.Cloud.AspNetCore.DataProtection.Kms
{
    using System.Security.Cryptography;
    using static KmsXmlConstants;

    /// <summary>
    /// Implementation for encryption, configured by <see cref="GoogleCloudDataProtectionBuilderExtensions"/>.
    /// In order to encrypt arbitrary amounts of data, we create a symmetric key within .NET, and encrypt
    /// that key data using KMS. The symmetric key is used to encrypt the plaintext.
    /// </summary>
    internal sealed class KmsXmlEncryptor : IXmlEncryptor
    {
        private readonly KeyManagementServiceClient _kmsClient;
        private readonly CryptoKeyName _keyName;
        private readonly CryptoKeyPathName _keyPathName;

        internal KmsXmlEncryptor(KeyManagementServiceClient kmsClient, CryptoKeyName keyName)
        {
            _kmsClient = GaxPreconditions.CheckNotNull(kmsClient, nameof(kmsClient));
            _keyName = GaxPreconditions.CheckNotNull(keyName, nameof(keyName));
            _keyPathName = new CryptoKeyPathName(keyName.ProjectId, keyName.LocationId, keyName.KeyRingId, keyName.CryptoKeyId);
        }
   
        /// <inheritdoc />
        public EncryptedXmlInfo Encrypt(XElement plaintextElement)
        {
            // Steps:
            // 1) Generate a local symmetric key
            // 2) Encrypt the XML with that key
            // 3) Encrypt the local key data with KMS
            // 4) Return an element containing:
            //    - The KMS crypto key used for encryption
            //    - The encrypted key data
            //    - The encrypted payload

            var keyPair = CreateLocalKey();

            byte[] locallyEncryptedData;
            using (keyPair.algorithm)
            {
                locallyEncryptedData = EncryptElement(keyPair.algorithm, plaintextElement);
            }

            ByteString encryptedKeyData = _kmsClient.Encrypt(_keyPathName, keyPair.proto.ToByteString()).Ciphertext;
            var encryptedElement = new XElement(EncryptedElement,
                new XComment("This key is encrypted with Google KMS."),
                new XAttribute(KmsKeyNameAttribute, _keyName),
                new XAttribute(LocalKeyDataAttribute, encryptedKeyData.ToBase64()),
                new XElement(PayloadElement, Convert.ToBase64String(locallyEncryptedData)));
            return new EncryptedXmlInfo(encryptedElement, typeof(KmsXmlDecryptor));
        }

        private (SymmetricKey proto, SymmetricAlgorithm algorithm) CreateLocalKey()
        {
            // Currently we only support AES.
            var algorithm = Aes.Create();
            algorithm.GenerateKey();
            algorithm.GenerateIV();
            var proto = new SymmetricKey
            {
                AesKey = new AesKey { IV = ByteString.CopyFrom(algorithm.IV), Key = ByteString.CopyFrom(algorithm.Key) }
            };
            return (proto, algorithm);
        }

        private byte[] EncryptElement(SymmetricAlgorithm algorithm, XElement element)
        {
            using (var stream = new MemoryStream())
            {
                element.Save(stream, SaveOptions.DisableFormatting);
                byte[] bytes = stream.ToArray();
                using (var encryptor = algorithm.CreateEncryptor())
                {
                    return encryptor.TransformFinalBlock(bytes, 0, bytes.Length);
                }
            }
        }
    }
}
