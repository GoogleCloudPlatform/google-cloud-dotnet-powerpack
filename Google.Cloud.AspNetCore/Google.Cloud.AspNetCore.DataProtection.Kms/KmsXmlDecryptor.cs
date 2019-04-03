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
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Xml.Linq;

namespace Google.Cloud.AspNetCore.DataProtection.Kms
{
    using System.Security.Cryptography;
    using static KmsXmlConstants;

    /// <summary>
    /// Decryptor for elements encrypted with <see cref="KmsXmlEncryptor"/>.
    /// </summary>
    internal sealed class KmsXmlDecryptor : IXmlDecryptor
    {
        private readonly KeyManagementServiceClient _kmsClient;

        /// <summary>
        /// Constructor called by ASP.NET Core dependency injection
        /// </summary>
        /// <param name="serviceProvider">The service provider used to provide the <see cref="KeyManagementServiceClient"/>.</param>
        public KmsXmlDecryptor(IServiceProvider serviceProvider) : this(serviceProvider.GetService<InternalDependency<KeyManagementServiceClient>>()?.Value)
        {
        }

        /// <summary>
        /// Constructor visible for testing purposes.
        /// </summary>
        /// <param name="kmsClient">The KMS client to use to decrypt.</param>
        internal KmsXmlDecryptor(KeyManagementServiceClient kmsClient)
        {
            _kmsClient = GaxPreconditions.CheckNotNull(kmsClient, nameof(kmsClient));
        }

        /// <inheritdoc />
        public XElement Decrypt(XElement encryptedElement)
        {
            GaxPreconditions.CheckNotNull(encryptedElement, nameof(encryptedElement));
            XElement payloadElement = encryptedElement.Element(PayloadElement);
            XAttribute kmsKeyName = encryptedElement.Attribute(KmsKeyNameAttribute);
            XAttribute localKeyDataAttribute = encryptedElement.Attribute(LocalKeyDataAttribute);
            GaxPreconditions.CheckArgument(payloadElement != null, nameof(encryptedElement), "Expected '{0}' element", PayloadElement);
            GaxPreconditions.CheckArgument(kmsKeyName != null, nameof(encryptedElement), "Expected '{0}' attribute", KmsKeyNameAttribute);
            GaxPreconditions.CheckArgument(localKeyDataAttribute != null, nameof(encryptedElement), "Expected '{0}' attribute", LocalKeyDataAttribute);

            CryptoKeyName cryptoKeyName = CryptoKeyName.Parse(kmsKeyName.Value);
            ByteString encryptedLocalKeyData = ByteString.FromBase64(localKeyDataAttribute.Value);
            ByteString plaintextLocalKeyData = _kmsClient.Decrypt(cryptoKeyName, encryptedLocalKeyData).Plaintext;

            SymmetricKey key = SymmetricKey.Parser.ParseFrom(plaintextLocalKeyData);

            using (var algorithm = CreateLocalKey(key))
            {
                byte[] encryptedPayload = Convert.FromBase64String(payloadElement.Value);
                using (var decryptor = algorithm.CreateDecryptor())
                {
                    byte[] plaintextPayload = decryptor.TransformFinalBlock(encryptedPayload, 0, encryptedPayload.Length);
                    using (var stream = new MemoryStream(plaintextPayload))
                    {
                        return XElement.Load(stream);
                    }
                }
            }
        }

        private static SymmetricAlgorithm CreateLocalKey(SymmetricKey key)
        {
            switch (key.KeyDataCase)
            {
                case SymmetricKey.KeyDataOneofCase.AesKey:
                    var aesKey = key.AesKey;
                    var aes = Aes.Create();
                    aes.Key = aesKey.Key.ToByteArray();
                    aes.IV = aesKey.IV.ToByteArray();
                    return aes;
                default:
                    throw new ArgumentException($"Unknown key type: {key.KeyDataCase}. Check you're using the latest Google.Cloud.AspNetCore.DataProtection.Kms package");
            }
        }
    }
}
