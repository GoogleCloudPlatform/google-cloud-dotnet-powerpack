// Copyright (c) 2019 Google LLC.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); you may not
// use this file except in compliance with the License. You may obtain a copy of
// the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
// WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
// License for the specific language governing permissions and limitations under
// the License.

using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Google.Cloud.AspNetCore.Firestore.DistributedCache
{
    public static class FirestoreCacheExtensions
    {
        /// <summary>
        /// Add a distributed cache that stores cache entries in Firestore.
        /// </summary>
        /// <param name="services">The service collection to which to add the cache.</param>
        /// <param name="projectId">Your Google Cloud Project Id.
        /// If null, pulls your project id from the current application
        /// default credentials.
        /// </param>
        public static IServiceCollection AddFirestoreDistributedCache(
            this IServiceCollection services,
            string projectId = null)
        {
            services.AddSingleton<FirestoreCache>(provider =>
                new FirestoreCache(projectId ?? FirestoreCache.GetProjectId(),
                    provider.GetService<ILogger<FirestoreCache>>()));
            services.AddSingleton<IDistributedCache>(provider =>
                provider.GetService<FirestoreCache>());
            return services;
        }

        /// <summary>
        /// Adds a background service that perodically 
        /// (randomly about once every 24 hours) deletes stale cache entries
        /// from Firestore.
        /// </summary>
        /// <param name="services">The service collection to which to add the cache.</param>
        public static IServiceCollection AddFirestoreDistributedCacheGarbageCollector(
            this IServiceCollection services)
        {
            services.AddHostedService<FirestoreCacheGarbageCollector>();
            return services;
        }
    }
}
