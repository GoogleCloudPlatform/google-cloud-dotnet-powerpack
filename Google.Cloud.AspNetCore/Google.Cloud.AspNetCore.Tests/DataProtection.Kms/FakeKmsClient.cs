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
using System.Linq;

namespace Google.Cloud.AspNetCore.DataProtection.Kms.Tests
{
    /// <summary>
    /// Fake KMS client that "encrypts" and "decrypts" using a simple XOR and addition/subtraction
    /// operation on each byte, where the XOR operand is the bottom 8 bits of the hash of the key name.
    /// </summary>
    internal class FakeKmsClient : KeyManagementServiceClient
    {
        public override EncryptResponse Encrypt(EncryptRequest request, CallSettings callSettings = null)
        {
            var key = request.CryptoKeyPathName;
            var keyVersionName = new CryptoKeyVersionName(key.ProjectId, key.LocationId, key.KeyRingId, key.CryptoKeyPathId, "1");
            byte xorOperand = (byte) request.Name.GetHashCode();
            return new EncryptResponse
            {
                Ciphertext = ByteString.CopyFrom(request.Plaintext.Select(x => (byte) (x ^ xorOperand)).Select(x => (byte) (x + 1)).ToArray()),
                Name = keyVersionName.ToString()
            };
        }

        public override DecryptResponse Decrypt(DecryptRequest request, CallSettings callSettings = null)
        {
            byte xorOperand = (byte) request.Name.GetHashCode();
            return new DecryptResponse
            {                
                Plaintext = ByteString.CopyFrom(request.Ciphertext.Select(x => (byte) (x - 1)).Select(x => (byte) (x ^ xorOperand)).ToArray())                
            };
        }
    }
}
