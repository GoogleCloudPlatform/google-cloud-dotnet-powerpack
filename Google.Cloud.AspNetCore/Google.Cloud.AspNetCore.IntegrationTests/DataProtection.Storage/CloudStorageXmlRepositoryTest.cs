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

using Google.Cloud.AspNetCore.IntegrationTests.DataProtection.Storage;
using Google.Cloud.Storage.V1;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace Google.Cloud.AspNetCore.DataProtection.Storage.IntegrationTests
{
    [Collection(nameof(StorageFixture))]
    public class CloudStorageXmlRepositoryTest
    {
        private readonly StorageFixture _fixture;

        public CloudStorageXmlRepositoryTest(StorageFixture fixture) =>
            _fixture = fixture;

        [Fact]
        public void NonXmlDocument()
        {
            string objectName = nameof(NonXmlDocument);
            UploadObject(objectName, "plain text", "text/plain");
            var repo = CreateRepo(objectName);
            Assert.Empty(repo.GetAllElements());
            Assert.Throws<InvalidOperationException>(() => repo.StoreElement(new XElement("foo"), "ignored"));
        }

        [Fact]
        public void NewObject()
        {
            string objectName = nameof(NewObject);
            var repo = CreateRepo(objectName);
            Assert.Empty(repo.GetAllElements());
            repo.StoreElement(new XElement("foo", new XAttribute("id", "id1")), "ignored");
            XElement element = Assert.Single(repo.GetAllElements());
            Assert.Equal("id1", element.Attribute("id").Value);
        }

        [Fact]
        public void EmptyInitialXmlDocument()
        {
            string objectName = nameof(EmptyInitialXmlDocument);
            UploadObject(objectName, "<differentroot />", "text/xml");
            var repo = CreateRepo(objectName);
            Assert.Empty(repo.GetAllElements());
            repo.StoreElement(new XElement("foo", new XAttribute("id", "id1")), "ignored");
            XElement element = Assert.Single(repo.GetAllElements());
            Assert.Equal("id1", element.Attribute("id").Value);
        }

        [Fact]
        public void NonEmptyInitialXmlDocument()
        {
            string objectName = nameof(NonEmptyInitialXmlDocument);
            UploadObject(objectName, "<differentroot><other id=\"old\" /></differentroot>", "text/xml");
            var repo = CreateRepo(objectName);
            XElement element = Assert.Single(repo.GetAllElements());
            repo.StoreElement(new XElement("foo", new XAttribute("id", "new")), "ignored");
            ValidateStoredDocument(objectName, new[] { "old", "new" });
        }

        /// <summary>
        /// Test to update a document multiple times as quickly as possible, either within a single
        /// repository objects (simulating multiple parts of a single process needing to add elements),
        /// or from multiple repository objects (simulating multiple servers all starting up at the same time).
        /// While it would be great for this to work for much larger numbers than are tested here, the GCS
        /// limit of "one update per second, for each object" limits how far this can reliably be tested.
        /// But it would be *very* unusual for this to actually cause a problem - servers don't normally start
        /// up *that* close together, especially ones creating new keys in the repository.
        /// </summary>
        [Theory]
        [InlineData(5, 1)]
        [InlineData(1, 5)]
        [InlineData(2, 2)]
        public async Task ConcurrencyStress(int repositories, int elementsPerRepository)
        {
            var client = StorageClient.Create();
            string objectName = $"concurrency-{repositories}-{elementsPerRepository}.xml";
            var semaphore = new SemaphoreSlim(0, repositories * elementsPerRepository);
            // Start all the tasks, but they won't do anything until we release the semaphore
            var tasks =
                (from repoIndex in Enumerable.Range(0, repositories)
                 let repo = CreateRepo(objectName)
                 from elementIndex in Enumerable.Range(0, elementsPerRepository)
                 let element = CreateElement($"{repoIndex}/{elementIndex}")
                 select StoreAsync(repo, element, semaphore)).ToList();

            semaphore.Release(repositories * elementsPerRepository);
            await Task.WhenAll(tasks);
            var expectedIds = from repoIndex in Enumerable.Range(0, repositories)
                              from elementIndex in Enumerable.Range(0, elementsPerRepository)
                              select $"{repoIndex}/{elementIndex}";

            ValidateStoredDocument(objectName, expectedIds);
        }

        /// <summary>
        /// Checks that the document stored in the given object has elements with the given IDs (and only those),
        /// in any order.
        /// </summary>
        private void ValidateStoredDocument(string objectName, IEnumerable<string> expectedIds)
        {
            var client = StorageClient.Create();
            XDocument document;
            using (var stream = new MemoryStream())
            {
                client.DownloadObject(_fixture.Bucket, objectName, stream);
                stream.Position = 0;
                document = XDocument.Load(stream);
            }
            var allIds = document.Root.Elements().Select(x => x.Attribute("id").Value).OrderBy(id => id).ToList();
            expectedIds = expectedIds.OrderBy(id => id).ToList();
            Assert.Equal(expectedIds, allIds);
        }

        private XElement CreateElement(string id) => new XElement("element", new XAttribute("id", id));

        /// <summary>
        /// Starts a task which will wait for the given semaphore to be released, then
        /// adds the given item to the given repository.
        /// </summary>
        private static async Task StoreAsync(CloudStorageXmlRepository repository, XElement element, SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync().ConfigureAwait(false);
            repository.StoreElement(element, "ignored");
        }

        private CloudStorageXmlRepository CreateRepo(string objectName)
        {
            var client = StorageClient.Create();
            return new CloudStorageXmlRepository(client, _fixture.Bucket, objectName);
        }

        private void UploadObject(string objectName, string text, string contentType)
        {
            var client = StorageClient.Create();
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(text)))
            {
                client.UploadObject(_fixture.Bucket, objectName, contentType, stream);
            }
        }
    }
}
