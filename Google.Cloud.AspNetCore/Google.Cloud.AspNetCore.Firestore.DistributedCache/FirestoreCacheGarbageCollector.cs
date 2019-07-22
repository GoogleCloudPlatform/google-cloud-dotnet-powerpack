using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Hosting;

namespace Google.Cloud.AspNetCore.Firestore.DistributedCache
{
    public class FirestoreCacheGarbageCollector : BackgroundService
    {
        private readonly FirestoreCache _cache;
        private readonly TimeSpan _frequency;
        private readonly Random _random = new Random();


        public FirestoreCacheGarbageCollector(FirestoreCache cache,
            TimeSpan? frequency = null)
        {
            if (cache is null)
            {
                throw new System.ArgumentNullException(nameof(cache));
            }
            _cache = cache;
            _frequency = frequency.GetValueOrDefault(TimeSpan.FromDays(1));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Wait a random interval.
                int seconds = (int) _frequency.TotalSeconds;
                int randomSeconds = _random.Next(seconds * 2);
                await Task.Delay(TimeSpan.FromSeconds(randomSeconds));
                
            }
        }
    }

    [FirestoreData]
    internal class FirestoreCacheGarbageCollectorSchedule
    {
    }
}