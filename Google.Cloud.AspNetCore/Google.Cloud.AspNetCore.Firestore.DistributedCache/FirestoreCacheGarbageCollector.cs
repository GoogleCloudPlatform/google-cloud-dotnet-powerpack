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
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Google.Cloud.AspNetCore.Firestore.DistributedCache
{
    /// <summary>
    /// An implementation of IHostedService that periodically runs
    /// FirestoreCache.CollectGarbageAsync().
    /// </summary>
    public class FirestoreCacheGarbageCollector : BackgroundService
    {
        private readonly FirestoreCache _cache;
        private readonly TimeSpan _frequency;
        private readonly Random _random = new Random();
        private readonly ILogger<FirestoreCacheGarbageCollector> _logger;
        private readonly Api.Gax.IScheduler _scheduler;

        /// <summary>
        /// Constructs a garbage collector for the Firestore distributed cache.
        /// </summary>
        /// <param name="cache">The cache to garbage collect. Must not be null.</param>
        /// <param name="logger">The logger to use for diagnostic messages. Must not be null.</param>
        /// <param name="frequency">The frequency of garbage collection. May be null, in which case a default frequency of 1 day will be used.</param>
        /// <param name="scheduler">The scheduler to use. May be null, in which case the system scheduler will be used.</param>
        public FirestoreCacheGarbageCollector(FirestoreCache cache,
            ILogger<FirestoreCacheGarbageCollector> logger,
            TimeSpan? frequency = null, Api.Gax.IScheduler scheduler = null)
        {
            _cache = GaxPreconditions.CheckNotNull(cache, nameof(cache));
            _logger = GaxPreconditions.CheckNotNull(logger, nameof(logger));
            _frequency = frequency.GetValueOrDefault(TimeSpan.FromDays(1));
            _scheduler = scheduler ?? SystemScheduler.Instance;
        }


        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Wait a random interval.
                int seconds = (int)_frequency.TotalSeconds;
                int randomSeconds = _random.Next(seconds * 2);
                _logger.LogTrace("Waiting {0} seconds before collecting garbage...", randomSeconds);
                await _scheduler.Delay(TimeSpan.FromSeconds(randomSeconds), stoppingToken);
                stoppingToken.ThrowIfCancellationRequested();

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

