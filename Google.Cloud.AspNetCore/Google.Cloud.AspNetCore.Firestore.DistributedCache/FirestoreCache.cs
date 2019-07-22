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
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Google.Cloud.AspNetCore.Firestore.DistributedCache
{
    public class FirestoreCache : IDistributedCache
    {
        private readonly FirestoreDb _firestore;
        private readonly CollectionReference _cacheEntries;
        private readonly ILogger<FirestoreCache> _logger;

        public FirestoreCache(FirestoreDb firestore, ILogger<FirestoreCache> logger,
            string collection = "CacheEntries")
        {
            if (logger is null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            if (string.IsNullOrWhiteSpace(collection))
            {
                throw new ArgumentException("Must not be empty",
                    nameof(collection));
            }

            _firestore = firestore;
            _cacheEntries = _firestore.Collection(collection);
            _logger = logger;
        }
        public FirestoreCache(string projectId, ILogger<FirestoreCache> logger,
            string collection = "CacheEntries")
            : this(FirestoreDb.Create(projectId), logger, collection)
        {
        }

        byte[] IDistributedCache.Get(string key) =>
            ValueFromSnapshot(_cacheEntries.Document(key).GetSnapshotAsync().Result);

        async Task<byte[]> IDistributedCache.GetAsync(string key, CancellationToken token) =>
            ValueFromSnapshot(await _cacheEntries.Document(key).GetSnapshotAsync(token));

        byte[] ValueFromSnapshot(DocumentSnapshot snapshot)
        {
            if (!snapshot.Exists)
            {
                return null;
            }
            CacheDoc doc = snapshot.ConvertTo<CacheDoc>();
            var now = DateTime.UtcNow;
            if (doc.AbsoluteExpiration.HasValue &&
                doc.AbsoluteExpiration.Value < now)
            {
                return null;
            }
            var slidingExpiration = doc.LastRefresh.GetValueOrDefault()
                + TimeSpan.FromSeconds(doc.SlidingExpirationSeconds.GetValueOrDefault());
            if (slidingExpiration < now)
            {
                return null;
            }
            return doc.Value;
        }
        void IDistributedCache.Refresh(string key)
        {
            try
            {
                _cacheEntries.Document(key)
                    .UpdateAsync("LastRefresh", DateTime.UtcNow).Wait();
            }
            catch (Grpc.Core.RpcException e)
            when (e.StatusCode == Grpc.Core.StatusCode.NotFound)
            {
                // Curiously, ASP.NET middleware will call Refresh for
                // cache entries that have never been Set.  That's ok,
                // but there's nothing for us to do.
            }
        }

        async Task IDistributedCache.RefreshAsync(string key, CancellationToken token)
        {
            try
            {
                await _cacheEntries.Document(key).UpdateAsync(
                    "LastRefresh", DateTime.UtcNow, cancellationToken: token);
            }
            catch (Grpc.Core.RpcException e)
            when (e.StatusCode == Grpc.Core.StatusCode.NotFound)
            {
                // Curiously, ASP.NET middleware will call Refresh for
                // cache entries that have never been Set.  That's ok,
                // but there's nothing for us to do.
            }
        }

        void IDistributedCache.Remove(string key) =>
            _cacheEntries.Document(key).DeleteAsync().Wait();

        Task IDistributedCache.RemoveAsync(string key, CancellationToken token) =>
            _cacheEntries.Document(key).DeleteAsync(cancellationToken: token);

        CacheDoc MakeCacheDoc(byte[] value, DistributedCacheEntryOptions options)
        {
            CacheDoc doc = new CacheDoc()
            {
                LastRefresh = DateTime.UtcNow,
                Value = value,
            };
            if (options.SlidingExpiration.HasValue)
            {
                doc.SlidingExpirationSeconds =
                    options.SlidingExpiration.Value.TotalSeconds;
            }
            else
            {
                var forever = DateTime.MaxValue - doc.LastRefresh.Value;
                doc.SlidingExpirationSeconds = forever.TotalSeconds / 2;
            }
            if (options.AbsoluteExpiration.HasValue)
            {
                doc.AbsoluteExpiration = options.AbsoluteExpiration.Value.UtcDateTime;
            }
            if (options.AbsoluteExpirationRelativeToNow.HasValue)
            {
                doc.AbsoluteExpiration = DateTime.UtcNow +
                    options.AbsoluteExpirationRelativeToNow.Value;
            }
            return doc;
        }

        void IDistributedCache.Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            _cacheEntries.Document(key).SetAsync(MakeCacheDoc(value, options)).Wait();

        Task IDistributedCache.SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
            CancellationToken token) =>
            _cacheEntries.Document(key).SetAsync(MakeCacheDoc(value, options),
            cancellationToken: token);

        public async Task CollectGarbage(CancellationToken token)
        {
            _logger.LogTrace("Begin garbage collection.");
            // Purge entries whose AbsoluteExpiration has passed.
            const int pageSize = 40;
            int batchSize;  // Batches of cache entries to be deleted.
            var now = DateTime.UtcNow;
            do
            {
                QuerySnapshot querySnapshot = await
                    _cacheEntries.OrderByDescending("AbsoluteExpiration")
                    .StartAfter(now)
                    .Limit(pageSize)
                    .GetSnapshotAsync(token);
                batchSize = 0;
                WriteBatch writeBatch = _cacheEntries.Database.StartBatch();
                foreach (DocumentSnapshot docSnapshot in querySnapshot.Documents)
                {
                    if (docSnapshot.ConvertTo<CacheDoc>()
                        .AbsoluteExpiration.HasValue)
                    {
                        writeBatch.Delete(docSnapshot.Reference,
                            Precondition.LastUpdated(
                                docSnapshot.UpdateTime.GetValueOrDefault()));
                        batchSize += 1;
                    }
                }
                if (batchSize > 0)
                {
                    _logger.LogDebug("Collecting {0} cache entries.", batchSize);
                    await writeBatch.CommitAsync(token);
                }
                if (token.IsCancellationRequested)
                {
                    return;
                }
            } while (batchSize == pageSize);

            // Purge entries whose SlidingExpiration has passed.
            do
            {
                QuerySnapshot querySnapshot = await
                    _cacheEntries.OrderBy("LastRefresh")
                    .Limit(pageSize)
                    .GetSnapshotAsync(token);
                batchSize = 0;
                WriteBatch writeBatch = _cacheEntries.Database.StartBatch();
                foreach (DocumentSnapshot docSnapshot in querySnapshot.Documents)
                {
                    CacheDoc doc = docSnapshot.ConvertTo<CacheDoc>();
                    if (doc.SlidingExpirationSeconds.HasValue)
                    {
                        var slidingExpiration =
                            doc.LastRefresh.GetValueOrDefault()
                            + TimeSpan.FromSeconds(doc.SlidingExpirationSeconds.Value);
                        if (slidingExpiration < now)
                        {
                            writeBatch.Delete(docSnapshot.Reference,
                                Precondition.LastUpdated(
                                    docSnapshot.UpdateTime.GetValueOrDefault()));
                            batchSize += 1;
                        }
                    }
                }
                if (batchSize > 0)
                {
                    _logger.LogDebug("Collecting {0} cache entries.", batchSize);
                    await writeBatch.CommitAsync(token);
                }
                if (token.IsCancellationRequested)
                {
                    return;
                }
            } while (batchSize > 0);
            _logger.LogTrace("End garbage collection.");
        }
    }

    /// <summary>
    /// The object stored in Firestore for each cache value.
    /// </summary>
    [FirestoreData]
    internal class CacheDoc
    {
        [FirestoreProperty]
        public byte[] Value { get; set; }

        //
        // Summary:
        //     Gets or sets an absolute expiration date for the cache entry.
        [FirestoreProperty]
        public DateTime? AbsoluteExpiration { get; set; }

        // Summary:
        //     Gets or sets how long a cache entry can be inactive (e.g. not accessed) before
        //     it will be removed. This will not extend the entry lifetime beyond the absolute
        //     expiration (if set).
        [FirestoreProperty]
        public double? SlidingExpirationSeconds { get; set; }

        [FirestoreProperty]
        public DateTime? LastRefresh { get; set; }
    }
}
