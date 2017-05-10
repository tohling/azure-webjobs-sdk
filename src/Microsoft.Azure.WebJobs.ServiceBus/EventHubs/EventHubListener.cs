﻿// Copyright (c) .NET Foundation. All rights reserved.
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

        private readonly TraceWriter _trace;

        public EventHubListener(ITriggeredFunctionExecutor executor, IMessageStatusManager statusManager, EventProcessorHost eventListener, EventProcessorOptions options, bool single, TraceWriter trace)
        {
            this._executor = executor;
            this._eventListener = eventListener;
            this._singleDispatch = single;
            this._options = options;
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
            string streamDispatcherEnabledSetting = Environment.GetEnvironmentVariable(StreamDispatcherEnabledAppSettingsKey);

            bool streamDispatcherEnabled = !string.IsNullOrEmpty(streamDispatcherEnabledSetting) && string.Equals(streamDispatcherEnabledSetting, "TRUE", StringComparison.OrdinalIgnoreCase);

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