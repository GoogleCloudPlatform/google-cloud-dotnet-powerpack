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

using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.DataProtection.Repositories;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Object = Google.Apis.Storage.v1.Data.Object;

namespace Google.AspNetCore.DataProtection.Storage
{
    internal sealed class CloudStorageXmlRepository : IXmlRepository
    {
        private readonly StorageClient _client;
        private readonly string _bucketName;
        private readonly string _objectName;

        internal CloudStorageXmlRepository(StorageClient client, string bucketName, string objectName)
        {
            this._client = client;
            this._bucketName = bucketName;
            this._objectName = objectName;
        }
    
        // TODO: All the concurrency management!

        public IReadOnlyCollection<XElement> GetAllElements()
        {
            CachedData data = LoadLatest();
            if (data == null)
            {
                return Array.Empty<XElement>();
            }
            return data.Element.Elements().ToList();
        }

        public void StoreElement(XElement element, string friendlyName)
        {
            CachedData data = LoadLatest();
            if (data == null)
            {
                data = new CachedData(0L, new XElement("root"));
            }
            data.Element.Add(element);
            using (var stream = new MemoryStream())
            {
                data.Element.Save(stream, SaveOptions.DisableFormatting);
                stream.Position = 0;
                _client.UploadObject(_bucketName, _objectName, "text/xml", stream,
                    new UploadObjectOptions { IfGenerationMatch = data.Generation });
            }
        }

        private CachedData LoadLatest()
        {
            // TODO: Retries etc.
            Object obj;
            try
            {
                obj = _client.GetObject(_bucketName, _objectName);
            }
            catch (GoogleApiException e) when (e.Error.Code == 404)
            {
                return null;
            }
            using (var stream = new MemoryStream())
            {
                _client.DownloadObject(_bucketName, _objectName, stream,
                    new DownloadObjectOptions { IfGenerationMatch = obj.Generation });
                stream.Position = 0;
                XElement element = XElement.Load(stream);
                return new CachedData(obj.Generation, element);
            }
        }

        private class CachedData
        {
            internal long? Generation { get; }
            internal XElement Element { get; }

            internal CachedData(long? generation, XElement element)
            {
                Generation = generation;
                Element = element;
            }
        }
    }
}
