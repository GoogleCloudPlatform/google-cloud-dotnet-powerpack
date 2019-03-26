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

using Google.Cloud.Kms.V1;
using System;
using System.Xml.Linq;
using Xunit;

namespace Google.Cloud.AspNetCore.DataProtection.Kms.Tests
{
    public class KmsXmlEncryptorDecryptorTest
    {
        [Theory]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(1_000_000)]
        public void Roundtrip(int dataSize)
        {
            var key = new CryptoKeyName("projectId", "locationId", "keyRingId", Guid.NewGuid().ToString());
            var client = new FakeKmsClient();
            var encryptor = new KmsXmlEncryptor(client, key);
            var decryptor = new KmsXmlDecryptor(client);
            var plain = new XElement("Original", new string ('x', dataSize));
            var encrypted = encryptor.Encrypt(plain);
            Assert.DoesNotContain("Plaintext value", encrypted.EncryptedElement.ToString());
            var decrypted = decryptor.Decrypt(encrypted.EncryptedElement);
            Assert.Equal(plain.ToString(), decrypted.ToString());
        }

        [Fact]
        public void EncryptFormat()
        {
            var key = new CryptoKeyName("projectId", "locationId", "keyRingId", Guid.NewGuid().ToString());
            var encryptor = new KmsXmlEncryptor(new FakeKmsClient(), key);
            var plain = new XElement("Original", "Plaintext value");
            var encrypted = encryptor.Encrypt(plain);

            Assert.Equal(typeof(KmsXmlDecryptor), encrypted.DecryptorType);
            var element = encrypted.EncryptedElement;
            Assert.Equal(KmsXmlConstants.EncryptedElement, element.Name);
            Assert.Equal(key.ToString(), element.Attribute(KmsXmlConstants.KmsKeyNameAttribute).Value);
            // Validate that the key data contains valid base64 data
            Convert.FromBase64String(element.Attribute(KmsXmlConstants.LocalKeyDataAttribute).Value);
            // Validate that the payload contains valid base64 data.
            Convert.FromBase64String(element.Element(KmsXmlConstants.PayloadElement).Value);
        }
    }
}
