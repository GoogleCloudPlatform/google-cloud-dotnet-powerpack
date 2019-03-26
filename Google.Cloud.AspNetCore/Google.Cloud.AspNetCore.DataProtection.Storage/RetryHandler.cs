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

using System;
using System.Threading;

namespace Google.Cloud.AspNetCore.DataProtection.Storage
{
    /// <summary>
    /// Simple means of executing retry with exponential backoff and proportional jitter,
    /// retrying on any GoogleApiException.
    /// </summary>
    internal sealed class RetryHandler
    {
        private readonly int _maxAttempts;
        private readonly TimeSpan _initialBackoff;
        private readonly double _backoffMultiplier;
        private readonly TimeSpan _maxBackoff;
        private readonly object _lock = new object();
        private readonly Random _random = new Random(); // Use for backoff.

        internal RetryHandler(int maxAttempts, TimeSpan initialBackoff, double backoffMultiplier, TimeSpan maxBackoff)
        {
            _maxAttempts = maxAttempts;
            _initialBackoff = initialBackoff;
            _backoffMultiplier = backoffMultiplier;
            _maxBackoff = maxBackoff;
        }

        /// <summary>
        /// Applies simple retry until the given function returns successfully (or a non-GoogleApiException is thrown).
        /// </summary>
        internal T ExecuteWithRetry<T>(Func<T> func)
        {
            int attempt = 0;
            TimeSpan nextBackoff = _initialBackoff;
            while (true)
            {
                try
                {
                    return func();
                }
                catch (GoogleApiException) when (attempt < _maxAttempts)
                {
                    attempt++;
                    int millisecondsToSleep;
                    lock (_lock)
                    {
                        int nextBackoffMillis = (int)nextBackoff.TotalMilliseconds;
                        // Apply jitter to the backoff, but only within the range of 50%-100% of the "theoretical" backoff.
                        millisecondsToSleep = nextBackoffMillis / 2 + _random.Next(nextBackoffMillis / 2);
                    }
                    Thread.Sleep(millisecondsToSleep);
                    nextBackoff = TimeSpan.FromSeconds(nextBackoff.TotalSeconds * _backoffMultiplier);
                    if (nextBackoff > _maxBackoff)
                    {
                        nextBackoff = _maxBackoff;
                    }
                }
            }
        }
    }
}
