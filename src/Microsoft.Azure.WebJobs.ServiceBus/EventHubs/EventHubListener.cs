// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.ServiceBus.EventHubs;
using Microsoft.ServiceBus.Messaging;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    // Created from the EventHubTrigger attribute to listen on the EventHub. 
    internal sealed class EventHubListener : IListener, IEventProcessorFactory
    {
        private const string StreamDispatcherEnabledAppSettingsKey = "STREAM_DISPATCHER_ENABLED";
        private const string StreamDispatcherMaxDopAppSettingsKey = "STREAM_DISPATCHER_MAXDOP";
        private const string StreamDispatcherBoundedCapacityAppSettingsKey = "STREAM_DISPATCHER_BOUNDED_CAPACITY";
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly EventProcessorHost _eventListener;
        private readonly bool _singleDispatch;
        private readonly EventProcessorOptions _options;
        private readonly IMessageStatusManager _statusManager;
        private readonly EventHubConfiguration _config;

        public EventHubListener(ITriggeredFunctionExecutor executor, EventProcessorHost eventListener, bool single, EventHubConfiguration config, TraceWriter trace)
        {
            this._executor = executor;
            this._eventListener = eventListener;
            this._singleDispatch = single;
            this._options = config.GetOptions();
            this._statusManager = statusManager;
            this._trace = trace;
        }

        void IListener.Cancel()
        {
            this.StopAsync(CancellationToken.None).Wait();
        }

        void IDisposable.Dispose() // via IListener
        {
            // nothing to do. 
        }

        // This will get called once when starting the JobHost. 
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return _eventListener.RegisterEventProcessorFactoryAsync(this, _options);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _eventListener.UnregisterEventProcessorAsync();
        }
        
        // This will get called per-partition. 
        IEventProcessor IEventProcessorFactory.CreateEventProcessor(PartitionContext context)
        {
            return new Listener(this);
        }

        internal static Func<Func<Task>, Task> CreateCheckpointStrategy(int batchCheckpointFrequency)
        {
            if (batchCheckpointFrequency <= 0)
            {
                throw new InvalidOperationException("Batch checkpoint frequency must be larger than 0.");
            }
            else if (batchCheckpointFrequency == 1)
            {
                return (checkpoint) => checkpoint();
            }
            else
            {
                int batchCounter = 0;
                return async (checkpoint) =>
                {
                    batchCounter++;
                    if (batchCounter >= batchCheckpointFrequency)
                    {
                        batchCounter = 0;
                        await checkpoint();
                    }
                };
            }
        }

        // We get a new instance each time Start() is called. 
        // We'll get a listener per partition - so they can potentialy run in parallel even on a single machine.
        private class Listener : IEventProcessor
        {
            private readonly EventHubListener _parent;
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();
            private readonly Func<PartitionContext, Task> _checkpoint;

            if (streamDispatcherEnabled)
            {
                int streamDispatcherMaxDop = 64;
                int streamDispatcherBoundedCapacity = 64;
                int maxDop;
                int boundedCapacity;

                string streamDispatcherMaxDopSetting = Environment.GetEnvironmentVariable(StreamDispatcherMaxDopAppSettingsKey);
                string streamDispatcherBoundedCapacitySetting =
                    Environment.GetEnvironmentVariable(StreamDispatcherBoundedCapacityAppSettingsKey);

                if (!string.IsNullOrEmpty(streamDispatcherMaxDopSetting) &&
                    int.TryParse(streamDispatcherMaxDopSetting, out maxDop))
                {
                    streamDispatcherMaxDop = maxDop;
                }

                if (!string.IsNullOrEmpty(streamDispatcherBoundedCapacitySetting) &&
                    int.TryParse(streamDispatcherBoundedCapacitySetting, out boundedCapacity))
                {
                    streamDispatcherBoundedCapacity = boundedCapacity;
                }

                return new EventHubStreamListener(_singleDispatch,
                    this._executor, _statusManager,
                    TimeSpan.FromSeconds(1),
                    streamDispatcherMaxDop,
                    streamDispatcherBoundedCapacity, _trace);
            }

            return new EventHubBatchListener(this._singleDispatch, this._executor, _trace);
        }
    }
}