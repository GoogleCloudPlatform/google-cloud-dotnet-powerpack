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
using Google.Cloud.AspNetCore.DataProtection.Storage;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.DataProtection
{
    /// <summary>
    /// Contains extension methods for modifying a <see cref="IDataProtectionBuilder"/> to work with Google Cloud Storage.
    /// </summary>
    public static class GoogleCloudDataProtectionBuilderExtensions
    {
        /// <summary>
        /// Configures the data protection system to persist keys in Google Cloud Storage.
        /// </summary>
        /// <param name="builder">The data protection builder to configure. Must not be null.</param>
        /// <param name="bucketName">The name of the bucket in which to store the object containing the keys. Must not be null.</param>
        /// <param name="objectName">The name of the object in which to store the keys. Must not be null.</param>
        /// <returns>The same builder, for chaining purposes.</returns>
        public static IDataProtectionBuilder PersistKeysToGoogleCloudStorage(
            this IDataProtectionBuilder builder, string bucketName, string objectName) =>
            PersistKeysToGoogleCloudStorage(builder, bucketName, objectName, null);

        /// <summary>
        /// Configures the data protection system to persist keys in Google Cloud Storage.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If <paramref name="client"/> is null, the client is constructed as follows:
        /// <list type="bullet">
        ///   <item><description>If a <c>StorageClient</c> is configured via dependency injection, that is used.</description></item>
        ///   <item><description>If a <c>GoogleCredential</c> is configured via dependency injection, that is used to construct a <c>StorageClient</c>.</description></item>
        ///   <item><description>Otherwise, the default credentials are used to construct a <c>StorageClient</c>.</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <param name="builder">The data protection builder to configure. Must not be null.</param>
        /// <param name="bucketName">The name of the bucket in which to store the object containing the keys. Must not be null.</param>
        /// <param name="objectName">The name of the object in which to store the keys. Must not be null.</param>
        /// <param name="client">The Google Cloud Storage client to use for network requests. May be null, in which case the client will
        /// be fetched from dependency injection or created with the default credentials.</param>
        /// <returns>The same builder, for chaining purposes.</returns>
        public static IDataProtectionBuilder PersistKeysToGoogleCloudStorage(
            this IDataProtectionBuilder builder, string bucketName, string objectName, StorageClient client)
        {
            GaxPreconditions.CheckNotNull(builder, nameof(builder));
            GaxPreconditions.CheckNotNull(bucketName, nameof(bucketName));
            GaxPreconditions.CheckNotNull(objectName, nameof(objectName));

            builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(services =>
            {
                if (client == null)
                {
                    client = services.GetService<StorageClient>();
                    if (client == null)
                    {
                        var credential = services.GetService<GoogleCredential>();
                        // If credential is null, this will use the default credentials automatically.
                        client = StorageClient.Create(credential);
                    }
                }
                return new ConfigureOptions<KeyManagementOptions>(options =>
                    options.XmlRepository = new CloudStorageXmlRepository(client, bucketName, objectName));
            });
            return builder;
        }
    }
}
