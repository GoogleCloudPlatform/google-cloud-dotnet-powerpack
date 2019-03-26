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

using System.Xml.Linq;

namespace Google.Cloud.AspNetCore.DataProtection.Kms
{
    /// <summary>
    /// Constants used by both <see cref="KmsXmlDecryptor"/> and <see cref="KmsXmlEncryptor"/>.
    /// </summary>
    internal static class KmsXmlConstants
    {
        // XML format:
        // <encryptedData
        //   kmsKeyName="projects/abc/locations/def/keyRings/ghi/cryptoKey/jkl"
        //   localKeyData="XXXX">
        //   <encryptedValue>YYYY</encryptedValue>
        // </encryptedData>
        //
        // In the above, XXXX is the base64 representation of the KMS-encrypted local key material,
        // and YYYY is the base64 representation of the local-key-encrypted data we've been asked to encrypt.

        /// <summary>
        /// The name of the outer element.
        /// </summary>
        internal static XName EncryptedElement { get; } = "encryptedData";

        /// <summary>
        /// The name of the attribute within <see cref="EncryptedElement"/> specifying the KMS key name
        /// used to encrypt the local key data.
        /// </summary>
        internal static XName KmsKeyNameAttribute { get; } = "kmsKeyName";

        /// <summary>
        /// The name of the attribute within <see cref="EncryptedElement"/> specifying the encrypted
        /// form of the local key data.
        /// </summary>
        internal static XName LocalKeyDataAttribute { get; } = "localKeyData";

        /// <summary>
        /// The name of the element within <see cref="EncryptedElement"/> specifying the value that has
        /// been encrypted with the local key.
        /// </summary>
        internal static XName PayloadElement{ get; } = "encryptedValue";
    }
}
