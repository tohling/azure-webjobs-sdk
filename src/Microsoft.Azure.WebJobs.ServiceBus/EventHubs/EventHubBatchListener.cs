// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.ServiceBus.Messaging;
using NLog;

namespace Microsoft.Azure.WebJobs.ServiceBus.EventHubs
{
    internal class EventHubBatchListener : IEventProcessor, IDisposable
    {
        private readonly ILogger _logger;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly bool _singleDispatch;

        private int _messagesExecuted = 0;
        private int _messagesComplete = 0;
        private int _messagesTimeout = 0;
        // private long _messagesRunning = 0;
        private string _partitionId;

        public EventHubBatchListener(bool singleDispatch,
            ITriggeredFunctionExecutor executor)
        {
            this._singleDispatch = singleDispatch;
            this._executor = executor;

            _logger = EventHubLogger.Instance;
        }

        public async Task CloseAsync(PartitionContext context, CloseReason reason)
        {
            this._cts.Cancel(); // Signal interuption to ProcessEventsAsync()

            // Finish listener
            if (reason == CloseReason.Shutdown)
            {
                await context.CheckpointAsync();
            }
        }

        public Task OpenAsync(PartitionContext context)
        {
            Interlocked.Exchange(ref _messagesTimeout, -1);
            if (context != null)
            {
                Interlocked.Exchange(ref _partitionId, context.Lease.PartitionId);

                _logger.Info("{eventType} {method} {eventHubName} {partitionId} {sequence}",
                    "Transition", "OpenAsync",
                    context.EventHubPath, context.Lease.PartitionId,
                    context.Lease.SequenceNumber);
            }

            return Task.FromResult(0);
        }

        /*
        private void UpdateStats(object state)
        {
            var exec = Interlocked.Exchange(ref _messagesExecuted, 0);
            var comp = Interlocked.Exchange(ref _messagesComplete, 0);
            var time = Interlocked.Exchange(ref _messagesTimeout, 0);
            var running = Interlocked.Read(ref _messagesRunning);
            var partitionId = Interlocked.Exchange(ref _partitionId, _partitionId);


            if (time > -1)
            {
                _logger.Info("{eventType} method {method} path {path}  exec {exec} comp {comp} timeout {time} running {running} pending {pending} partitionid {partitionid} state {state}",
                    "EventHubDispatcherStats", "EventHubDispatcher", _eventHubName, exec, comp, time, running, 0, partitionId, state);
            }
        }
        */

        public async Task ProcessEventsAsync(PartitionContext context,
            IEnumerable<EventData> messages)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                _logger.Info("{eventType} {method} {count} {eventHubName} {partitionId} {sequence}",
                    "BeginEventHubProcessing", "ProcessEventsAsync", messages.Count(),
                    context.EventHubPath, context.Lease.PartitionId, context.Lease.SequenceNumber);

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

                            // TODO - make a wrap a timer around it with time logging
                            Task task = _executor.TryExecuteAsync(input, _cts.Token)
                                .ContinueWith(t => Interlocked.Increment(ref _messagesExecuted));
                            dispatches.Add(task);
                        }
                    }

                    // Drain the whole batch before taking more work
                    if (dispatches.Count > 0)
                    {
                        await Task.WhenAll(dispatches);
                        Interlocked.Add(ref _messagesComplete, dispatches.Count);
                    }
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
                }

                // Dispose all messages to help with memory pressure. If this is missed, the finalizer thread will still get them. 
                foreach (var message in messages)
                {
                    message.Dispose();
                }

                // There are lots of reasons this could fail. That just means that events will get double-processed, which is inevitable
                // with event hubs anyways. 
                // For example, it could fail if we lost the lease. That could happen if we failed to renew it due to CPU starvation or an inability 
                // to make the outbound network calls to renew. 
                await context.CheckpointAsync();
            }
            finally
            {
                sw.Stop();
            }
        }

        public void Dispose()
        {
            _cts.Dispose();
        }
    }
}
