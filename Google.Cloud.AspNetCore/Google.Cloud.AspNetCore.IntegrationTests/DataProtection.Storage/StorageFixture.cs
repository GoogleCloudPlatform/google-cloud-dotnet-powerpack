// Copyright 2019 Google LLC
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     https://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Google.Cloud.ClientTesting;
using Google.Cloud.Storage.V1;
using Xunit;

namespace Google.Cloud.AspNetCore.IntegrationTests.DataProtection.Storage
{
    [CollectionDefinition(nameof(StorageFixture))]
    public class StorageFixture : CloudProjectFixtureBase, ICollectionFixture<StorageFixture>
    {
        public string Bucket { get; }
        public StorageClient Client { get; }

        public StorageFixture()
        {
            Client = StorageClient.Create();
            Bucket = IdGenerator.FromDateTime(prefix: "tests-", suffix: "-data-protection");
            Client.CreateBucket(ProjectId, Bucket);
        }
    }
}
