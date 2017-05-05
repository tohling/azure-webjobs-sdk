// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        private readonly int _maxDegreeOfParallelism;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        private readonly int _boundedCapacity;

        /// <summary>
        /// The EventHubStreamListener.
        /// </summary>
        /// <param name="singleDispatch"></param>
        /// <param name="executor"></param>
        /// <param name="statusManager"></param>
        /// <param name="maxElapsedTime"></param>
        /// <param name="maxDop"></param>
        /// <param name="backlog"></param>
        public EventHubStreamListener(
            bool singleDispatch,
            ITriggeredFunctionExecutor executor,
            IMessageStatusManager statusManager,
            TimeSpan maxElapsedTime,
            int maxDop,
            int backlog)
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
            // LogEventHubListenerType(_maxDegreeOfParallelism, _boundedCapacity);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        private static void LogEventHubListenerType(int maxDop, int boundedCapacity)
        {
            string dispatcherLogDir = Environment.GetEnvironmentVariable("EVENTHUB_LOG_DIR");
            FileStream fileStream = null;

            if (!string.IsNullOrEmpty(dispatcherLogDir))
            {
                string logFilePath = Path.Combine(dispatcherLogDir, "eventhub_dispatcher.log");
                try
                {
                    fileStream = new FileStream(logFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                        FileShare.Read);
                    using (StreamWriter file = new StreamWriter(fileStream))
                    {
                        file.WriteLine($"[{DateTime.UtcNow}]: EventHubStreamListener");
                        file.WriteLine($"[{DateTime.UtcNow}]: Max degree of Parallellism: {maxDop}, Bounded capacity: {boundedCapacity}");
                    }
                }
                finally
                {
                    fileStream?.Dispose();
                }
            }
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
            /*
            string msg =
                $"Method: CloseAsync, EventHubPath: {context.EventHubPath}, PartitionId: {context.Lease.PartitionId}, LeaseSeqNum: {context.Lease.SequenceNumber}";
            EventHubLogger.LoggerInstance.LogMessage("EventHubStreamListener", LogType.Info, msg);
            */
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "OpenAsync")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "PartitionId")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "LeaseSeqNum")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "EventHubPath")]
        public Task OpenAsync(PartitionContext context)
        {
            if (context != null)
            {
                /*
                string msg = string.Format("Method:OpenAsync, EventHubPath: {0}, PartitionId: {1}, LeaseSeqNum: {2}", context.EventHubPath, context.Lease.PartitionId, context.Lease.SequenceNumber);
                EventHubLogger.LoggerInstance.LogMessage("EventHubStreamListener", LogType.Info, msg);
                */
            }

            return Task.FromResult(0);
        }

        public async Task ProcessEventsAsync(PartitionContext context,
            IEnumerable<EventData> msgEnum)
        {
            var sw = Stopwatch.StartNew();
            int messageCount = 0;
            // string msg = null;

            try
            {
                // Event hub can return a null message set on timeout
                if (msgEnum == null)
                {
                    return;
                }
                var messages = msgEnum.ToArray();
                if (messages.Length == 0)
                {
                    return;
                }
                messageCount = messages.Length;

                var xy = new EventData();

                EventHubTriggerInput value = new EventHubTriggerInput
                {
                    Events = messages,
                    Content = this.GetContent(messages),
                    PartitionContext = context
                };

                // No-op
                if (_noop)
                {
                    return;
                }

                // Single dispatch 
                if (_singleDispatch)
                {
                    List<Task> dispatches = new List<Task>();
                    for (int i = 0; i < messages.Length; i++)
                    {
                        // The entire batch of messages is passed to the dispatcher each 
                        // time, incrementing the selector index
                        var trigger = value.GetSingleEventTriggerInput(i);

                        // TODO - enable cancellation and timeout on this iteration
                        var task = _dispatcher.SendAsync(new TriggeredFunctionData()
                        {
                            ParentId = null,
                            TriggerValue = trigger
                        });
                        dispatches.Add(task);
                    }

                    await Task.WhenAll(dispatches).ConfigureAwait(false);
                    /*
                    msg = $"Method: ProcessEventsAsync-SingleDispatch, EventHubPath: {context.EventHubPath}, PartitionId: {context.Lease.PartitionId}, LeaseSeqNum: {context.Lease.SequenceNumber}";
                    EventHubLogger.LoggerInstance.LogMessage("EventHubStreamListener", LogType.Info, msg);
                    */
                }
                else
                {
                    // Batch dispatch
                    TriggeredFunctionData input = new TriggeredFunctionData
                    {
                        ParentId = null,
                        TriggerValue = value
                    };

                    FunctionResult result = await _executor
                        .TryExecuteAsync(input, CancellationToken.None)
                        .ConfigureAwait(false);

                    // Dispose all messages to help with memory pressure. If this is missed, the finalizer thread will still get them. 
                    foreach (var message in messages)
                    {
                        message.Dispose();
                    }

                    /*
                    msg = $"Method: ProcessEventsAsync-BatchDispatch, EventHubPath: {context.EventHubPath}, PartitionId: {context.Lease.PartitionId}, LeaseSeqNum: {context.Lease.SequenceNumber}";
                    EventHubLogger.LoggerInstance.LogMessage("EventHubStreamListener", LogType.Info, msg);
                    */
                }

                // [masimms] TODO - update the checkpoint periodically, not on every batch of messages
                await context.CheckpointAsync().ConfigureAwait(false);
                /*
                msg = $"Method: ProcessEventsAsync-Checkpoint, EventHubPath: {context.EventHubPath}, PartitionId:'{context.Lease.PartitionId}, LeaseSeqNum: {context.Lease.SequenceNumber}";
                EventHubLogger.LoggerInstance.LogMessage("EventHubStreamListener", LogType.Info, msg);
                */
            }
            finally
            {
                sw.Stop();
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        private byte[][] GetContent(EventData[] messages)
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
