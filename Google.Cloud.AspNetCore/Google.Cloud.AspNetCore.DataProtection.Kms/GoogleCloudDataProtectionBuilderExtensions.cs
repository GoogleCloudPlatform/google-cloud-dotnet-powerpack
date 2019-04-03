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
using Google.Apis.Auth.OAuth2;
using Google.Cloud.AspNetCore.DataProtection.Kms;
using Google.Cloud.Kms.V1;
using Grpc.Auth;
using Grpc.Core;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        /// <param name="builder">The data protection builder to configure. Must not be null.</param>
        /// <param name="projectId">The Google Cloud project ID containing the KMS key ring. Must not be null.</param>
        /// <param name="locationId">The location of the KMS key ring, e.g. "global". Must not be null.</param>
        /// <param name="keyRingId">The ID of the KMS key ring. Must not be null.</param>
        /// <param name="keyId">The ID of the key within the KMS key ring. Must not be null.</param>
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
        /// <param name="builder">The data protection builder to configure. Must not be null.</param>
        /// <param name="keyName">The name of the KMS key to use. Must not be null.</param>
        /// <returns>The same builder, for chaining purposes.</returns>
        public static IDataProtectionBuilder ProtectKeysWithGoogleKms(
            this IDataProtectionBuilder builder,
            CryptoKeyName keyName) =>
            ProtectKeysWithGoogleKms(builder, keyName, null);

        /// <summary>
        /// Configures the data protection system to protect keys with specified key in Google Cloud KMS.
        /// </summary>
        /// <param name="builder">The data protection builder to configure. Must not be null.</param>
        /// <param name="keyName">The name of the KMS key to use. Must not be null.</param>
        /// <returns>The same builder, for chaining purposes.</returns>
        public static IDataProtectionBuilder ProtectKeysWithGoogleKms(
            this IDataProtectionBuilder builder,
            string keyName) =>
            ProtectKeysWithGoogleKms(builder, CryptoKeyName.Parse(GaxPreconditions.CheckNotNull(keyName, nameof(keyName))), null);

        /// <summary>
        /// Configures the data protection system to protect keys with specified key in Google Cloud KMS.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If <paramref name="kmsClient"/> is null, the client is constructed as follows:
        /// <list type="bullet">
        ///   <item><description>If a <c>KeyManagementServiceClient</c> is configured via dependency injection, that is used.</description></item>
        ///   <item><description>Otherwise, the default credentials are used to construct a <c>KeyManagementServiceClient</c>.</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <param name="builder">The data protection builder to configure. Must not be null.</param>
        /// <param name="keyName">The name of the KMS key to use. Must not be null.</param>
        /// <param name="kmsClient">The KMS client to use for network operations. May be null.</param>
        /// <returns>The same builder, for chaining purposes.</returns>
        public static IDataProtectionBuilder ProtectKeysWithGoogleKms(this IDataProtectionBuilder builder, CryptoKeyName keyName, KeyManagementServiceClient kmsClient)
        {
            GaxPreconditions.CheckNotNull(builder, nameof(builder));
            GaxPreconditions.CheckNotNull(keyName, nameof(keyName));
            
            builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(services =>
            {
                if (kmsClient == null)
                {
                    kmsClient = services.GetService<KeyManagementServiceClient>();
                    if (kmsClient == null)
                    {
                        // Note: this is consistent with the GCS support, but unfortunate in that we want to move away
                        // from GoogleCredential for gRPC. Ideally, we want to be able to remove the Google.Apis.Auth dependency,
                        // but still use a GoogleCredential if it *has* been configured by a user, probably via reflection.
                        // This can be addressed when we've got the new auth library, which is a while off (as of 2019-04-03).
                        // (It's expected that by then we'll have a cleaner way of building a client with custom credentials, too.)
                        GoogleCredential credential = services.GetService<GoogleCredential>();
                        if (credential == null)
                        {
                            kmsClient = KeyManagementServiceClient.Create();
                        }
                        else
                        {
                            var scopedCredential = credential.CreateScoped(KeyManagementServiceClient.DefaultScopes);
                            var channel = new Channel(KeyManagementServiceClient.DefaultEndpoint.ToString(), scopedCredential.ToChannelCredentials());
                            kmsClient = KeyManagementServiceClient.Create(channel);
                        }
                    }
                    
                }
                
                return new ConfigureOptions<KeyManagementOptions>
                    (options => options.XmlEncryptor = new KmsXmlEncryptor(kmsClient, keyName));
            });

            // Add the KMS client to DI so that it can be provided when constructing decryptors.
            // This will only be used by KmsXmlDecryptor, and will only be called after the lambda expression above
            // has executed, creating a new KeyManagementServiceClient if necessary.
            
            // The use of InternalDependency has three important aspects:
            // - We can register the provider now, even though it's only usable after the lambda expression has been run
            // - We can fetch any user-specified KMS client without triggering *this* dependency
            // - We can register this dependency without interfering with any user-specified KMS client. (This
            //   is important if the user configures one KMS client for general use, but specifies another one in
            //   the call to this method.)
            builder.Services.AddSingleton(provider => new InternalDependency<KeyManagementServiceClient>(kmsClient));
            return builder;
        }
    }
}
