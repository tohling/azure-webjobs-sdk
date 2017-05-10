﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.ServiceBus.Messaging;

namespace Microsoft.Azure.WebJobs.ServiceBus.EventHubs
{
    // We get a new instance each time Start() is called. 
    // We'll get a listener per partition - so they can potentialy run in parallel even on a single machine.
    internal class EventHubBatchListener : IEventProcessor, IDisposable
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly bool _singleDispatch;
        private readonly TraceWriter _trace;

        public EventHubBatchListener(bool singleDispatch,
            ITriggeredFunctionExecutor executor, TraceWriter trace)
        {
            this._singleDispatch = singleDispatch;
            this._executor = executor;
            this._trace = trace;
        }

        public async Task CloseAsync(PartitionContext context, CloseReason reason)
        {
            this._cts.Cancel(); // Signal interuption to ProcessEventsAsync()

            if (reason == CloseReason.Shutdown)
            {
                await context.CheckpointAsync();
            }
        }

        public Task OpenAsync(PartitionContext context)
        {
            return Task.FromResult(0);
        }

        public async Task ProcessEventsAsync(PartitionContext context,
            IEnumerable<EventData> messages)
        {
            EventHubTriggerInput value = new EventHubTriggerInput
            {
                Events = messages.ToArray(),
                PartitionContext = context
            };

            // Single dispatch 
            if (_singleDispatch)
            {
                int len = value.Events.Length;

                List<Task> dispatches = new List<Task>();
                for (int i = 0; i < len; i++)
                {
                    if (_cts.IsCancellationRequested)
                    {
                        // If we stopped the listener, then we may lose the lease and be unable to checkpoint. 
                        // So skip running the rest of the batch. The new listener will pick it up. 
                        continue;
                    }
                    else
                    {
                        TriggeredFunctionData input = new TriggeredFunctionData
                        {
                            ParentId = null,
                            TriggerValue = value.GetSingleEventTriggerInput(i)
                        };

                        Task task = _executor.TryExecuteAsync(input, _cts.Token);
                        dispatches.Add(task);
                    }
                }

                int dispatchCount = dispatches.Count;
                // Drain the whole batch before taking more work
                if (dispatches.Count > 0)
                {
                    await Task.WhenAll(dispatches);
                }

                _trace.Info($"Event hub batch listener: Single dispatch: Dispatched {dispatchCount} messages.");
            }
            else
            {
                // Batch dispatch

                TriggeredFunctionData input = new TriggeredFunctionData
                {
                    ParentId = null,
                    TriggerValue = value
                };

                FunctionResult result = await _executor.TryExecuteAsync(input, CancellationToken.None);
                _trace.Info($"Event hub batch listener: Batch dispatch: Dispatched {value.Events.Length} messages.");
            }

            bool hasEvents = false;
            int messageCount = messages.Count();

            // Dispose all messages to help with memory pressure. If this is missed, the finalizer thread will still get them. 
            foreach (var message in messages)
            {
                hasEvents = true;
                message.Dispose();
            }

            // Don't checkpoint if no events. This can reset the sequence counter to 0. 
            if (hasEvents)
            {
                // There are lots of reasons this could fail. That just means that events will get double-processed, which is inevitable
                // with event hubs anyways. 
                // For example, it could fail if we lost the lease. That could happen if we failed to renew it due to CPU starvation or an inability 
                // to make the outbound network calls to renew. 
                await context.CheckpointAsync();
            }

            _trace.Info($"Event hub batch listener: Checkpointed {messageCount} messages.");
        } // end class EventHubBatchListener

        public void Dispose()
        {
            _cts.Dispose();
        }
    }
}