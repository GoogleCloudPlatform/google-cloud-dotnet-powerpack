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
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Xml.Linq;
using Xunit;

namespace Google.Cloud.AspNetCore.IntegrationTests.DataProtection.Kms
{
    public class KmsXmlEncryptorDecryptorTest
    {
        [Theory]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(1_000_000)]
        public void RoundtripViaDataProtector(int dataSize)
        {
            var keyName = Environment.GetEnvironmentVariable("TEST_PROJECT_KMS_KEY");
            var key = CryptoKeyName.Parse(keyName);
            var xmlDocument = new XDocument(new XElement("root"));
            var random = new Random();
            var plaintext = new byte[dataSize];
            random.NextBytes(plaintext);

            Assert.Empty(xmlDocument.Root.Elements());
            var encrypted = Encrypt(xmlDocument, key, plaintext);
            Assert.Single(xmlDocument.Root.Elements());
            var roundtrip = Decrypt(xmlDocument, key, encrypted);
            Assert.Equal(plaintext, roundtrip);

            byte[] Encrypt(XDocument document, CryptoKeyName cryptoKeyName, byte[] bytes)
            {
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddDataProtection()
                    .ProtectKeysWithGoogleKms(cryptoKeyName)
                    .PersistKeysToMemory(document);
                var serviceProvider = serviceCollection.BuildServiceProvider();
                var protector = serviceProvider.GetDataProtector(new[] { "test" });
                return protector.Protect(bytes);
            }

            byte[] Decrypt(XDocument document, CryptoKeyName cryptoKeyName, byte[] bytes)
            {
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddDataProtection()
                    .ProtectKeysWithGoogleKms(cryptoKeyName)
                    .PersistKeysToMemory(document);
                var serviceProvider = serviceCollection.BuildServiceProvider();
                var protector = serviceProvider.GetDataProtector(new[] { "test" });
                return protector.Unprotect(bytes);
            }
        }

        // Note: this is an "integration with DI" test rather than "integration with the real KMS" test.
        [Fact]
        public void KmsClientDependencyUsed()
        {
            var kmsClient = new PassThroughKmsClient();
            var keyName = Environment.GetEnvironmentVariable("TEST_PROJECT_KMS_KEY");
            var xmlDocument = new XDocument(new XElement("root"));
            var random = new Random();
            var plaintext = new byte[100];
            random.NextBytes(plaintext);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<KeyManagementServiceClient>(kmsClient);
            serviceCollection.AddDataProtection()
                .ProtectKeysWithGoogleKms(keyName)
                .PersistKeysToMemory(xmlDocument);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var protector = serviceProvider.GetDataProtector(new[] { "test" });

            var encrypted = protector.Protect(plaintext);
            Assert.Equal(1, kmsClient.EncryptCalls);
            var roundtrip = protector.Unprotect(encrypted);
            Assert.Equal(1, kmsClient.DecryptCalls);
            Assert.Equal(plaintext, roundtrip);
        }
    }
}
