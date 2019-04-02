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

using Google.Api.Gax;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.Repositories;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml;
using System.Xml.Linq;

using Object = Google.Apis.Storage.v1.Data.Object;

namespace Google.Cloud.AspNetCore.DataProtection.Storage
{
    /// <summary>
    /// Implementation of <see cref="IXmlRepository"/> that stores the protected elements in a single Google Cloud Storage file.
    /// This class is configured by <see cref="GoogleCloudDataProtectionBuilderExtensions.PersistKeysToGoogleCloudStorage(IDataProtectionBuilder, string, string, StorageClient)" />
    /// (and other overloads).
    /// </summary>
    internal sealed partial class CloudStorageXmlRepository : IXmlRepository
    {
        /// <summary>
        /// Retry handler for loading the latest document.
        /// </summary>
        private static readonly RetryHandler s_loadLatestRetryHandler = new RetryHandler(
            maxAttempts: 5,
            initialBackoff: TimeSpan.FromSeconds(0.2),
            backoffMultiplier: 1.5,
            maxBackoff: TimeSpan.FromSeconds(1));

        /// <summary>
        /// Retry handler for storing changes. This has longer backoffs than the "load latest" retry handler,
        /// as object updates are rate-limited.
        /// </summary>
        private static readonly RetryHandler s_storeRetryHandler = new RetryHandler(
            maxAttempts: 5,
            initialBackoff: TimeSpan.FromSeconds(1),
            backoffMultiplier: 2.0,
            maxBackoff: TimeSpan.FromSeconds(3));

        private readonly StorageClient _client;
        private readonly string _bucketName;
        private readonly string _objectName;

        private readonly object _lock = new object();
        private LoadDocumentResult _latestDocument;

        /// <summary>
        /// Constructor, called by GoogleCloudDataProtectionBuilderExtensions.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="bucketName"></param>
        /// <param name="objectName"></param>
        internal CloudStorageXmlRepository(StorageClient client, string bucketName, string objectName)
        {
            this._client = GaxPreconditions.CheckNotNull(client, nameof(client));
            this._bucketName = GaxPreconditions.CheckNotNull(bucketName, nameof(bucketName));
            this._objectName = GaxPreconditions.CheckNotNull(objectName, nameof(objectName));
        }
    
        /// <inheritdoc />
        public IReadOnlyCollection<XElement> GetAllElements()
        {
            LoadDocumentResult data = LoadLatest();
            // Note: we assume the caller won't mutate the elements.
            return data.Document?.Root.Elements().ToList() ?? (IReadOnlyCollection<XElement>) Array.Empty<XElement>();
        }

        /// <inheritdoc />
        public void StoreElement(XElement element, string friendlyName)
        {
            // It's okay to ignore the friendly name. We could include it ourselves, but only by wrapping the element within another element in which we could store the name.
            // https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.dataprotection.repositories.ixmlrepository.storeelement?view=aspnetcore-2.2

            // Note that we have a nested retry here: if there's a large number of collisions, we could end up retrying while fetching the latest document, and then
            // retry at the top level too. That's probably not *too* bad - and very unlikely.
            s_storeRetryHandler.ExecuteWithRetry(() =>
            {
                LoadDocumentResult latest = LoadLatest();
                if (latest.Generation != 0 && latest.Document == null)
                {
                    // This will throw immediately, with no retry.
                    throw new InvalidOperationException("Object does not contain XML.");
                }

                XDocument document = latest.Generation == 0 ? new XDocument(new XElement("root")) : new XDocument(latest.Document);
                document.Root.Add(element);

                long storedGeneration;
                using (var stream = new MemoryStream())
                {
                    document.Save(stream, SaveOptions.DisableFormatting);
                    stream.Position = 0;
                    Object uploaded = _client.UploadObject(_bucketName, _objectName, "text/xml", stream, new UploadObjectOptions { IfGenerationMatch = latest.Generation });
                    storedGeneration = uploaded.Generation.Value; // We should always know the new generation.
                }
                lock (_lock)
                {
                    _latestDocument = new LoadDocumentResult(storedGeneration, document);
                }
                return 0;
            });
        }

        /// <summary>
        /// Loads the latest document. This method never returns null.
        /// </summary>
        private LoadDocumentResult LoadLatest()
        {
            LoadDocumentResult previous;
            lock (_lock)
            {
                previous = _latestDocument;
            }
            var current = s_loadLatestRetryHandler.ExecuteWithRetry(() => LoadLatestNoRetry(previous));
            lock (_lock)
            {
                _latestDocument = current;
            }
            return current;
        }

        /// <summary>
        /// Loads the latest document, based on a possible "currently known latest" document.
        /// This method does not retry. Note that this could be a nested method, but it's simpler to
        /// separate it out.
        /// </summary>
        private LoadDocumentResult LoadLatestNoRetry(LoadDocumentResult previous)
        {
            using (var stream = new MemoryStream())
            {
                long nextGeneration;
                // Note: It would be nice to fetch metadata and the document at the same time, but we don't have a way of doing so.
                try
                {
                    Object metadata = _client.GetObject(_bucketName, _objectName, previous?.GetObjectOptions);
                    nextGeneration = metadata.Generation.Value; // Note that we should always receive a Generation.
                }
                catch (GoogleApiException e) when (e.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    return new LoadDocumentResult(0, null);
                }
                catch (GoogleApiException e) when (e.HttpStatusCode == HttpStatusCode.NotModified)
                {
                    // No change, so use existing cached result.
                    return previous;
                }

                // Download a new version. If this fails due to a precondition issue, that suggests the object has changed again -
                // let the calling code wait and retry (if this hasn't happened too often already).
                _client.DownloadObject(_bucketName, _objectName, stream, new DownloadObjectOptions { IfGenerationMatch = nextGeneration });

                stream.Position = 0;
                XDocument document = null;
                try
                {
                    document = XDocument.Load(stream);
                }
                catch (XmlException)
                {
                    // The document variable will still be null, which is fine.
                }
                return new LoadDocumentResult(nextGeneration, document);
            }
        }

        /// <summary>
        /// Information from the last time we loaded the document. There are three states:
        /// - Document doesn't exist (Generation is 0, Document is null)
        /// - Document exists but isn't valid XML (Generation is non-zero, Document is null)
        /// - Document exists and contains valid XML (Generation is non-zero, Document is non-null)
        /// </summary>
        private sealed class LoadDocumentResult
        {
            /// <summary>
            /// The generation of the document, or 0 if the document does not currently exist.
            /// </summary>
            internal long Generation { get; }

            /// <summary>
            /// The loaded XML document, or null if *either* the document doesn't exist, or it's not valid XML.
            /// This *must not be mutated*.
            /// </summary>
            internal XDocument Document { get; }

            /// <summary>
            /// Options to use when checking for changes. This must not be mutated.
            /// </summary>
            internal GetObjectOptions GetObjectOptions { get; }

            internal LoadDocumentResult(long generation, XDocument document)
            {
                Generation = generation;
                Document = document;
                GetObjectOptions = new GetObjectOptions { IfGenerationNotMatch = generation };
            }
        }
    }
}
