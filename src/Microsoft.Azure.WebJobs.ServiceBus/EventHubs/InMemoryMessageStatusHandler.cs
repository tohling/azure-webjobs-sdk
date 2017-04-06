// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace Microsoft.Azure.WebJobs.ServiceBus.EventHubs
{
    internal struct MessageData
    {
        public DateTimeOffset Ttl { get; set; }
        public Guid Id { get; set; }
        public State State { get; set; }
        public byte[] Context { get; set; }
    }

    /// <summary>
    /// The in memory message status handler.
    /// </summary>
    internal class InMemoryMessageStatusHandler : IMessageStatusManager
    {
        private readonly ConcurrentDictionary<Guid, MessageData> _info;
        private readonly ILogger _logger;
        private long _activeTasks = 0;

        /// <summary>
        /// The in memory status handler
        /// </summary>
        public InMemoryMessageStatusHandler()
        {
            _info = new ConcurrentDictionary<Guid, MessageData>();
            _logger = EventHubLogger.Instance;
        }

        /// <summary>
        /// The active task count.
        /// </summary>
        public long ActiveTaskCount
        {
            get
            {
                return Interlocked.Read(ref _activeTasks);
            }
        }

        /// <summary>
        /// Update the status of the task.
        /// </summary>
        /// <param name="messageId"></param>
        /// <param name="state"></param>
        /// <param name="timeToLive"></param>
        /// <param name="elapsed"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        protected Task UpdateStatus(Guid messageId, State state,
            TimeSpan timeToLive, TimeSpan elapsed, byte[] context = null)
        {
            MessageData data = new MessageData()
            {
                Id = messageId,
                State = state,
                Context = context,
                Ttl = (timeToLive == TimeSpan.MinValue)
                    ? DateTimeOffset.UtcNow
                    : DateTimeOffset.UtcNow.Add(timeToLive)
            };

            _info.AddOrUpdate(messageId, data, (k, v) =>
            {
                v.State = state;
                if (timeToLive != TimeSpan.MinValue)
                {
                    v.Ttl = DateTimeOffset.UtcNow.Add(timeToLive);
                }
                if (context != null)
                {
                    v.Context = context;
                }
                return v;
            });

            if (state == State.Complete || state == State.Faulted)
            {
                Interlocked.Decrement(ref _activeTasks);
            }
            else
            {
                Interlocked.Increment(ref _activeTasks);
            }

            _logger.Debug("{eventType} {messageId} {elapsed} {timeToLive} {running} ",
                "InMemory_UpdateStatus", messageId,
                elapsed,
                timeToLive,
                Interlocked.Read(ref _activeTasks));
            return Task.FromResult(0);
        }

        /// <summary>
        /// Set the task as running.
        /// </summary>
        /// <param name="messageId"></param>
        /// <param name="timeToLive"></param>
        /// <param name="elapsed"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public Task SetRunning(Guid messageId, TimeSpan timeToLive,
            TimeSpan elapsed, byte[] context)
        {
            MessageData data = new MessageData()
            {
                Id = messageId,
                State = State.Running,
                Context = context,
                Ttl = (timeToLive == TimeSpan.MinValue) ?
                 DateTimeOffset.UtcNow :
                 DateTimeOffset.UtcNow.Add(timeToLive)
            };

            if (_info.TryAdd(messageId, data))
            {
                Interlocked.Increment(ref _activeTasks);
                // TODO log
            }
            else
            {
                // TODO log
            }
            return Task.FromResult(0);
        }

        /// <summary>
        /// Set the task as complete.
        /// </summary>
        /// <param name="messageId"></param>
        /// <param name="elapsed"></param>
        /// <returns></returns>
        public Task SetComplete(Guid messageId, TimeSpan elapsed)
        {
            MessageData md;
            if (_info.TryRemove(messageId, out md))
            {
                Interlocked.Decrement(ref _activeTasks);

                _logger.Debug("{eventType} {method} {messageId} {state} {elapsed}",
                    "EndEvent", "InMemory::SetComplete", messageId,
                    State.Complete,
                    elapsed);
            }

            return Task.FromResult(0);
        }
    }
}
