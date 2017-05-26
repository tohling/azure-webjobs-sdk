// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.ServiceBus.Listeners;
using Microsoft.ServiceBus.Messaging;
using Moq;
using Xunit;
using System;

namespace Microsoft.Azure.WebJobs.ServiceBus.UnitTests.Listeners
{
    public class EventHubListenerTests
    {
        [Theory]
        [InlineData(1, 100)]
        [InlineData(4, 25)]
        [InlineData(8, 12)]
        [InlineData(32, 3)]
        [InlineData(128, 0)]
        async Task EventHubBatchListener_CreatesCheckpointStrategy(int batchCheckpointFrequency, int expected)
        {
            var iterations = 100;
            var strategy = EventHubBatchListener.CreateCheckpointStrategy(batchCheckpointFrequency);

            var checkpoints = 0;
            Func<Task> checkpoint = () =>
            {
                checkpoints++;
                return Task.CompletedTask;
            };

            for (int i = 0; i < iterations; i++)
            {
                await strategy(checkpoint);
            }

            Assert.Equal(expected, checkpoints);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-2)]
        void EventHubBatchListener_Throws_IfInvalidCheckpointStrategy(int batchCheckpointFrequency)
        {
            var exc = Assert.Throws<InvalidOperationException>(() => EventHubBatchListener.CreateCheckpointStrategy(batchCheckpointFrequency));
            Assert.Equal("Batch listener checkpoint frequency must be larger than 0.", exc.Message);
        }

        [Theory]
        [InlineData(1, 100)]
        [InlineData(4, 25)]
        [InlineData(8, 12)]
        [InlineData(32, 3)]
        [InlineData(128, 0)]
        async Task EventHubStreamListener_CreatesCheckpointStrategy(int batchCheckpointFrequency, int expected)
        {
            var iterations = 100;
            var strategy = EventHubStreamListener.CreateCheckpointStrategy(batchCheckpointFrequency);

            var checkpoints = 0;
            Func<Task> checkpoint = () =>
            {
                checkpoints++;
                return Task.CompletedTask;
            };

            for (int i = 0; i < iterations; i++)
            {
                await strategy(checkpoint);
            }

            Assert.Equal(expected, checkpoints);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-2)]
        void EventHubStreamListener_Throws_IfInvalidCheckpointStrategy(int batchCheckpointFrequency)
        {
            var exc = Assert.Throws<InvalidOperationException>(() => EventHubStreamListener.CreateCheckpointStrategy(batchCheckpointFrequency));
            Assert.Equal("Stream listener checkpoint frequency must be larger than 0.", exc.Message);
        }
    }
}
