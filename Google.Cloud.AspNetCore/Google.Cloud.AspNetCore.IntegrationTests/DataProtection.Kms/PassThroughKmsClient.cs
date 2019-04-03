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

using Google.Api.Gax.Grpc;
using Google.Cloud.Kms.V1;
using Google.Protobuf;

namespace Google.Cloud.AspNetCore.IntegrationTests.DataProtection.Kms
{
    /// <summary>
    /// KMS client which doesn't encrypt at all, but counts calls. (This is slightly simpler to use than setting up a mock.)
    /// </summary>
    internal class PassThroughKmsClient : KeyManagementServiceClient
    {
        public int EncryptCalls { get; private set; }
        public int DecryptCalls { get; private set; }

        public override EncryptResponse Encrypt(CryptoKeyPathName name, ByteString plaintext, CallSettings callSettings = null)
        {
            EncryptCalls++;
            return new EncryptResponse { Ciphertext = plaintext };
        }

        public override DecryptResponse Decrypt(CryptoKeyName name, ByteString ciphertext, CallSettings callSettings = null)
        {
            DecryptCalls++;
            return new DecryptResponse { Plaintext = ciphertext };
        }
    }
}
