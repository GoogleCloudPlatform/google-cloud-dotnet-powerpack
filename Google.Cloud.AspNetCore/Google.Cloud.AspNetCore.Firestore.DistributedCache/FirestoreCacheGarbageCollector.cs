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
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Google.Cloud.AspNetCore.Firestore.DistributedCache
{
    public class FirestoreCacheGarbageCollector : BackgroundService
    {
        private readonly FirestoreCache _cache;
        private readonly TimeSpan _frequency;
        private readonly Random _random = new Random();
        private readonly ILogger<FirestoreCacheGarbageCollector> _logger;


        public FirestoreCacheGarbageCollector(FirestoreCache cache,
            ILogger<FirestoreCacheGarbageCollector> logger,
            TimeSpan? frequency = null)
        {
            if (cache is null)
            {
                throw new System.ArgumentNullException(nameof(cache));
            }

            if (logger is null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _cache = cache;
            _logger = logger;
            _frequency = frequency.GetValueOrDefault(TimeSpan.FromDays(1));
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Wait a random interval.
                int seconds = (int)_frequency.TotalSeconds;
                int randomSeconds = _random.Next(seconds * 2);
                _logger.LogTrace("Waiting {0} seconds before collecting garbage...", randomSeconds);
                await Task.Delay(TimeSpan.FromSeconds(randomSeconds));
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                // Collect the garbage.
                try
                {
                    await _cache.CollectGarbageAsync(stoppingToken);
                }
                catch (Exception e)
                {
                    _logger.LogError("Exception while collecting garbage.", e);
                }
            }
        }
    }
}

