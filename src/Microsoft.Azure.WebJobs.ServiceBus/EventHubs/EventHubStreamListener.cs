// Copyright (c) .NET Foundation. All rights reserved.
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
    /// <summary>
    /// The EventHubStreamListener class.
    /// </summary>
    internal class EventHubStreamListener : IEventProcessor, IDisposable
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly ITriggeredFunctionExecutor _executor;

        private readonly bool _singleDispatch;
        private readonly EventHubDispatcher _dispatcher;
        private readonly bool _noop;
        private readonly int _maxDegreeOfParallelism;
        private readonly int _boundedCapacity;
        private readonly TraceWriter _trace;

        /// <summary>
        /// The EventHubStreamListener.
        /// </summary>
        /// <param name="singleDispatch"></param>
        /// <param name="executor"></param>
        /// <param name="statusManager"></param>
        /// <param name="maxElapsedTime"></param>
        /// <param name="maxDop"></param>
        /// <param name="backlog"></param>
        /// <param name="trace"></param>
        public EventHubStreamListener(
            bool singleDispatch,
            ITriggeredFunctionExecutor executor,
            IMessageStatusManager statusManager,
            TimeSpan maxElapsedTime,
            int maxDop,
            int backlog,
            TraceWriter trace)
        {
            this._singleDispatch = singleDispatch;
            this._executor = executor;

            this._dispatcher = new EventHubDispatcher(
                executor: executor,
                statusManager: statusManager,
                maxElapsedTime: maxElapsedTime,
                maxDop: maxDop,
                capacity: backlog);

            _maxDegreeOfParallelism = maxDop;
            _boundedCapacity = backlog;
            _noop = false;
            _trace = trace;
            _trace.Info($"Event hub stream listener: Max degree of parallelism:{_maxDegreeOfParallelism}, bounded capacity:{_boundedCapacity}");
        }

        public async Task CloseAsync(PartitionContext context, CloseReason reason)
        {
            // Signal interuption to ProcessEventsAsync()
            this._cts.Cancel();

            // Finish listener
            if (reason == CloseReason.Shutdown)
            {
                await context.CheckpointAsync().ConfigureAwait(false);
            }
        }

        public Task OpenAsync(PartitionContext context)
        {
            return Task.FromResult(0);
        }

        public async Task ProcessEventsAsync(PartitionContext context,
            IEnumerable<EventData> messages)
        {
            // Event hub can return a null message set on timeout
            if (messages == null)
            {
                return;
            }

            EventData[] events = messages.ToArray();
            if (events.Length == 0)
            {
                return;
            }

            EventHubTriggerInput value = new EventHubTriggerInput
            {
                Events = messages.ToArray(),
                Content = GetContent(events),
                PartitionContext = context
            };

            int messageCount = value.Events.Length;

            // No-op
            if (_noop)
            {
                return;
            }

            // Single dispatch 
            if (_singleDispatch)
            {
                List<Task> dispatches = new List<Task>();
                for (int i = 0; i < events.Length; i++)
                {
                    // The entire batch of messages is passed to the dispatcher each 
                    // time, incrementing the selector index
                    var trigger = value.GetSingleEventTriggerInput(i);

                    var task = _dispatcher.SendAsync(new TriggeredFunctionData()
                    {
                        ParentId = null,
                        TriggerValue = trigger
                    });
                    dispatches.Add(task);
                }

                int dispatchCount = dispatches.Count;
                // Drain the whole batch before taking more work
                if (dispatches.Count > 0)
                {
                    await Task.WhenAll(dispatches).ConfigureAwait(false);
                }

                _trace.Info($"Event hub stream listener: Single dispatch: Dispatched {dispatchCount} messages.");
            }
            else
            {
                // Batch dispatch
                TriggeredFunctionData input = new TriggeredFunctionData
                {
                    ParentId = null,
                    TriggerValue = value
                };

                // TODO: Replace _executor with _dispatcher
                FunctionResult result = await _executor
                    .TryExecuteAsync(input, CancellationToken.None)
                    .ConfigureAwait(false);

                // Dispose all messages to help with memory pressure. If this is missed, the finalizer thread will still get them. 
            }

            await context.CheckpointAsync().ConfigureAwait(false);
             _trace.Info($"Event hub stream listener: Checkpointed {messageCount} messages.");

            foreach (var message in events)
            {
                message.Dispose();
            }

            foreach (var message in messages)
            {
                message.Dispose();
            }
        }

        private static byte[][] GetContent(EventData[] messages)
        {
            var bytes = new List<byte[]>();
            for (int i = 0; i < messages.Length; i++)
            {
                var content = messages[i].GetBytes();
                bytes.Add(content);
            }
            return bytes.ToArray();
        }

        public void Dispose()
        {
            _cts.Dispose();
            _dispatcher.Dispose();
        }
    }
}