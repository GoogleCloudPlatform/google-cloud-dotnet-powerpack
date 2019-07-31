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

using Google.Cloud.ClientTesting;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Google.Cloud.AspNetCore.Firestore.DistributedCache.IntegrationTests
{
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
