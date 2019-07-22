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
        string _key = Guid.NewGuid().ToString();
        byte[] _value = Guid.NewGuid().ToByteArray();

        public FirestoreCacheTest(FirestoreCacheTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        async Task TestAsync()
        {
            // Entry does not exist before setting.
            Assert.Null(await _fixture.Cache.GetAsync(_key));

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
                    SlidingExpiration = TimeSpan.FromSeconds(-3)
                });
            Assert.Null(await _fixture.Cache.GetAsync(_key));
        }

        [Fact]
        public async Task TestAbsoluteExpires()
        {
            await _fixture.Cache.SetAsync(_key, _value, 
                new DistributedCacheEntryOptions()
                {
                    AbsoluteExpiration = DateTime.UtcNow.AddSeconds(-3),
                });
            Assert.Null(await _fixture.Cache.GetAsync(_key));
        }

        [Fact]
        public async Task TestAbsoluteFromNowExpires()
        {
            await _fixture.Cache.SetAsync(_key, _value, 
                new DistributedCacheEntryOptions()
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(-3)
                });
            Assert.Null(await _fixture.Cache.GetAsync(_key));
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
