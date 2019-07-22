using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Google.Cloud.AspNetCore.Firestore.DistributedCache
{
    public static class FirestoreCacheExtensions
    {
        public static IServiceCollection AddFirestoreDistributedCache(
            this IServiceCollection services,
            string projectId)
        {
            services.AddSingleton<FirestoreCache>(provider =>
                new FirestoreCache(projectId, 
                    provider.GetService<ILogger<FirestoreCache>>()));
            return services;
        }

        public static IServiceCollection AddFirestoreDistributedCacheGarbageCollector(
            this IServiceCollection services)
        {
            services.AddHostedService<FirestoreCacheGarbageCollector>();
            return services;
        }
    }
}
