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

namespace Google.AspNetCore.DataProtection.Kms
{
    internal sealed class KmsXmlDecryptor : IXmlDecryptor
    {
        private readonly KeyManagementServiceClient _kmsClient;

        public KmsXmlDecryptor(IServiceProvider serviceProvider) : this(serviceProvider.GetService<KeyManagementServiceClient>())
        {
        }

        public KmsXmlDecryptor(KeyManagementServiceClient kmsClient)
        {
            _kmsClient = kmsClient;
        }

        public XElement Decrypt(XElement encryptedElement)
        {
            GaxPreconditions.CheckNotNull(encryptedElement, nameof(encryptedElement));
            XElement value = encryptedElement.Element(KmsXmlConstants.ValueElement);
            GaxPreconditions.CheckArgument(value != null, nameof(encryptedElement), "Expected '{0}' element", KmsXmlConstants.ValueElement);
            XAttribute keyName = encryptedElement.Attribute(KmsXmlConstants.KeyNameAttribute);
            GaxPreconditions.CheckArgument(keyName != null, nameof(encryptedElement), "Expected '{0}' element", KmsXmlConstants.KeyNameAttribute);

            CryptoKeyName cryptoKeyName = CryptoKeyName.Parse(keyName.Value);
            ByteString encryptedData = ByteString.FromBase64(value.Value);
            ByteString plaintextData = _kmsClient.Decrypt(cryptoKeyName, encryptedData).Plaintext;

            var plaintextBytes = plaintextData.ToByteArray();
            using (var stream = new MemoryStream(plaintextBytes))
            {
                return XElement.Load(stream);
            }
        }
    }
}
