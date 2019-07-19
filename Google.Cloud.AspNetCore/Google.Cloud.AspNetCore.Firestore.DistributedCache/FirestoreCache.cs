using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Distributed;

namespace Google.Cloud.AspNetCore.Firestore.DistributedCache
{
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
        public TimeSpan? SlidingExpiration { get; set; }

        [FirestoreProperty]
        public DateTime? LastRefresh { get; set; }
    }
    public class FirestoreCache : IDistributedCache
    {
        private FirestoreDb _firestore;
        private CollectionReference _sessions;

        public FirestoreCache(string projectId)
        {
             _firestore = FirestoreDb.Create(projectId);
            _sessions = _firestore.Collection("Sessions");           
        }

        byte[] IDistributedCache.Get(string key) =>
            ValueFromSnapshot(_sessions.Document(key).GetSnapshotAsync().Result);

        async Task<byte[]> IDistributedCache.GetAsync(string key, CancellationToken token) =>
            ValueFromSnapshot(await _sessions.Document(key).GetSnapshotAsync(token));

        byte[] ValueFromSnapshot(DocumentSnapshot snapshot)
        {
            if (!snapshot.Exists)
            {
                return null;
            }
            CacheDoc doc = snapshot.ConvertTo<CacheDoc>();
            var now = DateTime.UtcNow;
            if (doc.AbsoluteExpiration.GetValueOrDefault() < now)
            {
                return null;
            }
            var slidingExpiration = doc.LastRefresh.GetValueOrDefault() 
                + doc.SlidingExpiration.GetValueOrDefault();
            if (slidingExpiration < now)
            {
                return null;
            }
            return doc.Value;
        }
        void IDistributedCache.Refresh(string key) =>
            _sessions.Document(key).UpdateAsync("LastRefresh", DateTime.UtcNow).Wait(); 

        Task IDistributedCache.RefreshAsync(string key, CancellationToken token) =>
            _sessions.Document(key).UpdateAsync("LastRefresh", DateTime.UtcNow,
                cancellationToken:token); 

        void IDistributedCache.Remove(string key) =>
            _sessions.Document(key).DeleteAsync().Wait();

        Task IDistributedCache.RemoveAsync(string key, CancellationToken token) =>
            _sessions.Document(key).DeleteAsync(cancellationToken:token);

        CacheDoc MakeCacheDoc(byte[] value, DistributedCacheEntryOptions options)
        {
            CacheDoc doc = new CacheDoc()
            {
                LastRefresh = DateTime.UtcNow,
                Value = value,
                SlidingExpiration = options.SlidingExpiration
            };
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
            _sessions.Document(key).SetAsync(MakeCacheDoc(value, options)).Wait();

        Task IDistributedCache.SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, 
            CancellationToken token) =>
            _sessions.Document(key).SetAsync(MakeCacheDoc(value, options),
            cancellationToken:token);

    }
}
