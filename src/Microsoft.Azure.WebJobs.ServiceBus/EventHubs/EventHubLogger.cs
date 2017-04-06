// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using NLog;

namespace Microsoft.Azure.WebJobs.ServiceBus.EventHubs
{
    internal static class EventHubLogger
    {
        static EventHubLogger()
        {
            LogManager.ReconfigExistingLoggers();

            Instance = LogManager.GetCurrentClassLogger();
        }
        public static Logger Instance { get; private set; }
    }
}
