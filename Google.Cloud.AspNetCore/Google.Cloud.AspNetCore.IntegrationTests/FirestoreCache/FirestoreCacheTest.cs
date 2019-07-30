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
using Google.Cloud.ClientTesting;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Google.Cloud.AspNetCore.Firestore.DistributedCache.IntegrationTests
{
    public class FirestoreCacheTest : IClassFixture<FirestoreCacheTestFixture>
    {
        private readonly FirestoreCacheTestFixture _fixture;
        private string _key = Guid.NewGuid().ToString();
        private byte[] _value = Guid.NewGuid().ToByteArray();

        public FirestoreCacheTest(FirestoreCacheTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        async Task TestAsync()
        {
            // Entry does not exist before setting.
            Assert.Null(await _fixture.Cache.GetAsync(_key));

            // Makes sure refreshing an entry before it exists doesn't throw
            // an exception.
            await _fixture.Cache.RefreshAsync(_key);

            // Set it.
            await _fixture.Cache.SetAsync(_key, _value, 
                new DistributedCacheEntryOptions()
                {
                    SlidingExpiration = TimeSpan.FromSeconds(1200)
                });

            // Get it.
            Assert.Equal(_value, await _fixture.Cache.GetAsync(_key));

            // Remove it.
            await _fixture.Cache.RemoveAsync(_key);

            // Entry does not exist after removing.
            Assert.Null(await _fixture.Cache.GetAsync(_key));
        }

        [Fact]
        void TestSync()
        {
            // Entry does not exist before setting.
            Assert.Null(_fixture.Cache.Get(_key));

            // Makes sure refreshing an entry before it exists doesn't throw
            // an exception.
            _fixture.Cache.Refresh(_key);

            // Set it.
            _fixture.Cache.Set(_key, _value, 
                new DistributedCacheEntryOptions()
                {
                    SlidingExpiration = TimeSpan.FromSeconds(1200)
                });

            // Get it.
            Assert.Equal(_value, _fixture.Cache.Get(_key));

            // Remove it.
            _fixture.Cache.Remove(_key);

            // Entry does not exist after removing.
            Assert.Null(_fixture.Cache.Get(_key));
        }

        [Fact]
        public async Task TestSlidingExpires()
        {
            await _fixture.Cache.SetAsync(_key, _value, 
                new DistributedCacheEntryOptions()
                {
                    SlidingExpiration = TimeSpan.FromSeconds(4)
                });
            await Task.Delay(3000);
            // Confirm Refresh() prevents expiration.
            await _fixture.Cache.RefreshAsync(_key);
            await Task.Delay(3000);
            Assert.Equal(_value, await _fixture.Cache.GetAsync(_key));
            await Task.Delay(3000);
            // Now, should have expired.
            Assert.Null(await _fixture.Cache.GetAsync(_key));

        }

        [Fact]
        public async Task TestAbsoluteExpires()
        {
            await _fixture.Cache.SetAsync(_key, _value, 
                new DistributedCacheEntryOptions()
                {
                    AbsoluteExpiration = DateTime.UtcNow.AddSeconds(4),
                });
            await Task.Delay(2000);
            Assert.Equal(_value, await _fixture.Cache.GetAsync(_key));
            await Task.Delay(3000);
            Assert.Null(await _fixture.Cache.GetAsync(_key));
        }

        [Fact]
        public async Task TestAbsoluteFromNowExpires()
        {
            await _fixture.Cache.SetAsync(_key, _value, 
                new DistributedCacheEntryOptions()
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(4)
                });
            await Task.Delay(2000);
            Assert.Equal(_value, await _fixture.Cache.GetAsync(_key));
            await Task.Delay(3000);
            Assert.Null(await _fixture.Cache.GetAsync(_key));
        }

        [Fact]
        public async Task TestGcSliding()
        {
            var token = default(CancellationToken);
            await _fixture.Cache.SetAsync(_key, _value, 
                new DistributedCacheEntryOptions()
                {
                    SlidingExpiration = TimeSpan.FromSeconds(4)
                });
            await _fixture.FirestoreCache.CollectGarbageAsync(token);
            DocumentSnapshot snapshot = await _fixture.CacheEntries.Document(_key).GetSnapshotAsync();
            Assert.NotNull(snapshot);
            Assert.True(snapshot.Exists);
            await Task.Delay(5000);
            await _fixture.FirestoreCache.CollectGarbageAsync(token);
            snapshot = await _fixture.CacheEntries.Document(_key).GetSnapshotAsync();
            Assert.NotNull(snapshot);
            Assert.False(snapshot.Exists);
        }

        [Fact]
        public async Task TestGcAbsoluteExpires()
        {
            var token = default(CancellationToken);
            await _fixture.Cache.SetAsync(_key, _value, 
                new DistributedCacheEntryOptions()
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(4)
                });
            await _fixture.FirestoreCache.CollectGarbageAsync(token);
            DocumentSnapshot snapshot = await _fixture.CacheEntries.Document(_key).GetSnapshotAsync();
            Assert.NotNull(snapshot);
            Assert.True(snapshot.Exists);
            await Task.Delay(5000);
            await _fixture.FirestoreCache.CollectGarbageAsync(token);
            snapshot = await _fixture.CacheEntries.Document(_key).GetSnapshotAsync();
            Assert.NotNull(snapshot);
            Assert.False(snapshot.Exists);
        }
    }

    public class FirestoreCacheTestFixture : CloudProjectFixtureBase
    {
        public FirestoreCacheTestFixture() : base()
        {
            LoggerFactory = new LoggerFactory();
            FirestoreDb = FirestoreDb.Create(this.ProjectId);
            FirestoreCache = new FirestoreCache(this.FirestoreDb,
                LoggerFactory.CreateLogger<FirestoreCache>());
            Cache = FirestoreCache;
            CacheEntries = FirestoreDb.Collection("CacheEntries");
        }
        public FirestoreDb FirestoreDb { get; private set; }

        public FirestoreCache FirestoreCache { get; private set; }
        public LoggerFactory LoggerFactory { get; private set; }
        public IDistributedCache Cache { get; private set; }
        public CollectionReference CacheEntries {get; private set; }
    }
}
