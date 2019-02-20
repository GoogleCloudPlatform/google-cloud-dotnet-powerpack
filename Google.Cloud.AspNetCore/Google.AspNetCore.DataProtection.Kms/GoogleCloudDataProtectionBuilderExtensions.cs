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
using Google.AspNetCore.DataProtection.Kms;
using Google.Cloud.Kms.V1;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.DataProtection
{
    /// <summary>
    /// Contains extension methods for modifying a <see cref="IDataProtectionBuilder"/> to work with Google KMS.
    /// </summary>
    public static class GoogleCloudDataProtectionBuilderExtensions
    {
        /// <summary>
        /// Configures the data protection system to protect keys with specified key in Google Cloud KMS.
        /// </summary>
        /// <param name="builder">The data protection builder to configure.</param>
        /// <param name="projectId">The Google Cloud project ID containing the KMS key ring.</param>
        /// <param name="locationId">The location of the KMS key ring, e.g. "global".</param>
        /// <param name="keyRingId">The ID of the KMS key ring.</param>
        /// <param name="keyId">The ID of the key within the KMS key ring.</param>
        /// <returns>The same builder, for chaining purposes.</returns>
        public static IDataProtectionBuilder ProtectKeysWithGoogleKms(
            this IDataProtectionBuilder builder,
            string projectId,
            string locationId,
            string keyRingId,
            string keyId) =>
            ProtectKeysWithGoogleKms(builder, new CryptoKeyName(projectId, locationId, keyRingId, keyId));

        /// <summary>
        /// Configures the data protection system to protect keys with specified key in Google Cloud KMS.
        /// </summary>
        /// <param name="builder">The data protection builder to configure.</param>
        /// <param name="keyName">The name of the KMS key to use.</param>
        /// <returns>The same builder, for chaining purposes.</returns>
        public static IDataProtectionBuilder ProtectKeysWithGoogleKms(
            this IDataProtectionBuilder builder,
            CryptoKeyName keyName) =>
            // TODO: Use builder.Services to try to find a KMS client? Or credentials?
            ProtectKeysWithGoogleKms(builder, KeyManagementServiceClient.Create(), keyName);


        /// <summary>
        /// Configures the data protection system to protect keys with specified key in Google Cloud KMS.
        /// </summary>
        /// <param name="builder">The data protection builder to configure.</param>
        /// <param name="kmsClient">The KMS client to use for network operations.</param>
        /// <param name="projectId">The Google Cloud project ID containing the KMS key ring.</param>
        /// <param name="locationId">The location of the KMS key ring, e.g. "global".</param>
        /// <param name="keyRingId">The ID of the KMS key ring.</param>
        /// <param name="keyId">The ID of the key within the KMS key ring.</param>
        /// <returns>The same builder, for chaining purposes.</returns>
        public static IDataProtectionBuilder ProtectKeysWithGoogleKms(
            this IDataProtectionBuilder builder,
            KeyManagementServiceClient kmsClient,
            string projectId,
            string locationId,
            string keyRingId,
            string keyId) =>
            ProtectKeysWithGoogleKms(builder, kmsClient, new CryptoKeyName(projectId, locationId, keyRingId, keyId));

        /// <summary>
        /// Configures the data protection system to protect keys with specified key in Google Cloud KMS.
        /// </summary>
        /// <param name="builder">The data protection builder to configure.</param>
        /// <param name="kmsClient">The KMS client to use for network operations.</param>
        /// <param name="keyName">The name of the KMS key to use.</param>
        /// <returns>The same builder, for chaining purposes.</returns>
        public static IDataProtectionBuilder ProtectKeysWithGoogleKms(this IDataProtectionBuilder builder, KeyManagementServiceClient kmsClient, CryptoKeyName keyName)
        {
            GaxPreconditions.CheckNotNull(builder, nameof(builder));
            GaxPreconditions.CheckNotNull(kmsClient, nameof(kmsClient));
            GaxPreconditions.CheckNotNull(keyName, nameof(keyName));
            // Add the KMS client to DI so that it can be provided when constructing decryptors.
            builder.Services.AddSingleton(kmsClient);
            builder.Services.Configure<KeyManagementOptions>(options => options.XmlEncryptor = new KmsXmlEncryptor(kmsClient, keyName));
            return builder;
        }
    }
}
