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

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Google.Cloud.AspNetCore.IntegrationTests.DataProtection.Kms
{
    /// <summary>
    /// Extension methods to configure <see cref="InMemoryXmlRepository"/>.
    /// </summary>
    public static class DataProtectionBuilderExtensions
    {
        public static IDataProtectionBuilder PersistKeysToMemory(this IDataProtectionBuilder builder, XDocument document)
        {
            builder.Services.Configure<KeyManagementOptions>(options => options.XmlRepository = new InMemoryXmlRepository(document));
            return builder;
        }
    }

    /// <summary>
    /// Extremely simple <see cref="IXmlRepository"/> implementation using an in-memory XML document,
    /// for simplicity of writing data protection tests.
    /// </summary>
    public class InMemoryXmlRepository : IXmlRepository
    {
        private readonly XDocument _document;

        public InMemoryXmlRepository(XDocument document) => _document = document;

        public IReadOnlyCollection<XElement> GetAllElements() =>
            _document.Root.Elements().ToList();

        public void StoreElement(XElement element, string friendlyName) =>
            _document.Root.Add(element);
    }
}
