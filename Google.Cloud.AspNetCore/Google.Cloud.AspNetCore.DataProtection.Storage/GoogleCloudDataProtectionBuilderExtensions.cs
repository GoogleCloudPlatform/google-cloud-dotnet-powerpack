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

using Google.Cloud.AspNetCore.DataProtection.Storage;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;

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
        /// <param name="builder">The data protection builder to configure.</param>
        /// <param name="bucketName">The name of the bucket in which to store the object containing the keys.</param>
        /// <param name="objectName">The name of the object in which to store the keys.</param>
        /// <returns>The same builder, for chaining purposes.</returns>
        public static IDataProtectionBuilder PersistKeysToGoogleCloudStorage(
            this IDataProtectionBuilder builder, string bucketName, string objectName) =>
            PersistKeysToGoogleCloudStorage(builder, GetClient(builder), bucketName, objectName);

        private static StorageClient GetClient(IDataProtectionBuilder builder)
        {
            // TODO: Use DI to find a StorageClient, or credentials if we can. It's not clear how we can
            // resolve existing configured dependencies.
            return StorageClient.Create();            
        }

        /// <summary>
        /// Configures the data protection system to persist keys in Google Cloud Storage.
        /// </summary>
        /// <param name="builder">The data protection builder to configure.</param>
        /// <param name="client">The Google Cloud Storage client to use for network requests.</param>
        /// <param name="bucketName">The name of the bucket in which to store the object containing the keys.</param>
        /// <param name="objectName">The name of the object in which to store the keys.</param>
        /// <returns>The same builder, for chaining purposes.</returns>
        public static IDataProtectionBuilder PersistKeysToGoogleCloudStorage(
            this IDataProtectionBuilder builder, StorageClient client, string bucketName, string objectName)
        {
            builder.Services.Configure<KeyManagementOptions>(options =>
                options.XmlRepository = new CloudStorageXmlRepository(client, bucketName, objectName));
            return builder;
        }
    }
}
