using System;
using System.Threading.Tasks;
using Google.Cloud.ClientTesting;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Google.Cloud.AspNetCore.Firestore.DistributedCache
{
    public class FirestoreCacheTest
    {
        private readonly FirestoreCacheTestFixture _fixture;

        public FirestoreCacheTest(FirestoreCacheTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        async Task TestAsync()
        {
            string key = Guid.NewGuid().ToString();
            byte[] value = Guid.NewGuid().ToByteArray();

            // Entry does not exist before setting.
            Assert.Null(await _fixture.Cache.GetAsync(key));

            // Set it.
            await _fixture.Cache.SetAsync(key, value, 
                new DistributedCacheEntryOptions()
                {
                    SlidingExpiration = TimeSpan.FromSeconds(1200)
                });

            // Get it.
            Assert.Equal(value, await _fixture.Cache.GetAsync(key));

            // Remove it.
            await _fixture.Cache.RemoveAsync(key);

            // Entry does not exist after removing.
            Assert.Null(await _fixture.Cache.GetAsync(key));
        }

        [Fact]
        void TestSync()
        {
            string key = Guid.NewGuid().ToString();
            byte[] value = Guid.NewGuid().ToByteArray();

            // Entry does not exist before setting.
            Assert.Null(_fixture.Cache.Get(key));

            // Set it.
            _fixture.Cache.Set(key, value, 
                new DistributedCacheEntryOptions()
                {
                    SlidingExpiration = TimeSpan.FromSeconds(1200)
                });

            // Get it.
            Assert.Equal(value, _fixture.Cache.Get(key));

            // Remove it.
            _fixture.Cache.Remove(key);

            // Entry does not exist after removing.
            Assert.Null(_fixture.Cache.Get(key));
        }

    }

    public class FirestoreCacheTestFixture : CloudProjectFixtureBase
    {
        public FirestoreCacheTestFixture(string testProjectEnvironmentVariable = "TEST_PROJECT") : base(testProjectEnvironmentVariable)
        {
            LoggerFactory = new LoggerFactory();
            FirestoreCache = new FirestoreCache(ProjectId,
                LoggerFactory.CreateLogger<FirestoreCache>());
            Cache = FirestoreCache;
        }

        public FirestoreCache FirestoreCache { get; private set; }
        public LoggerFactory LoggerFactory { get; private set; }
        public IDistributedCache Cache { get; private set; }
    }
}
