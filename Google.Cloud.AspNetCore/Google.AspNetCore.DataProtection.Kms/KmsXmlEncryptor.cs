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
using System.IO;
using System.Xml.Linq;

namespace Google.AspNetCore.DataProtection.Kms
{
    internal sealed class KmsXmlEncryptor : IXmlEncryptor
    {
        private readonly KeyManagementServiceClient _kmsClient;
        private readonly CryptoKeyName _keyName;
        private readonly CryptoKeyPathName _keyPathName;

        // TODO: Accept a CryptoKeyPathName as well or instead? (That would allow a specific version to be used for encryption.)
        internal KmsXmlEncryptor(KeyManagementServiceClient kmsClient, CryptoKeyName keyName)
        {
            _kmsClient = GaxPreconditions.CheckNotNull(kmsClient, nameof(kmsClient));
            _keyName = GaxPreconditions.CheckNotNull(keyName, nameof(keyName));
            _keyPathName = new CryptoKeyPathName(keyName.ProjectId, keyName.LocationId, keyName.KeyRingId, keyName.CryptoKeyId);
        }

        public EncryptedXmlInfo Encrypt(XElement plaintextElement)
        {
            ByteString plainTextData;

            using (var stream = new MemoryStream())
            {
                plaintextElement.Save(stream, SaveOptions.DisableFormatting);
                stream.Position = 0;
                plainTextData = ByteString.FromStream(stream);
            }

            ByteString encryptedData = _kmsClient.Encrypt(_keyPathName, plainTextData).Ciphertext;

            var encryptedElement = new XElement(KmsXmlConstants.EncryptedElement,
                new XComment(" This key is encrypted with Google KMS. "),
                new XAttribute(KmsXmlConstants.KeyNameAttribute, _keyName),
                new XElement(KmsXmlConstants.ValueElement, encryptedData.ToBase64()));
            return new EncryptedXmlInfo(encryptedElement, typeof(KmsXmlDecryptor));
        }
    }
}
