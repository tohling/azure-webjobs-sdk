// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Microsoft.Azure.WebJobs.ServiceBus.EventHubs
{
    internal static class EventHubLogger
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        static EventHubLogger()
        {
            string logConfigFilePath = Environment.GetEnvironmentVariable("EVENTHUB_LOGCONFIG_PATH");
            LogManager.Configuration = new XmlLoggingConfiguration(logConfigFilePath);

#if DEBUG
            // Setup the logging view for Sentinel - http://sentinel.codeplex.com
            var sentinalTarget = new NLogViewerTarget()
            {
                Name = "sentinal",
                Address = "udp://127.0.0.1:9999",
                IncludeNLogData = false
            };
            var sentinalRule = new LoggingRule("*", LogLevel.Trace, sentinalTarget);
            LogManager.Configuration.AddTarget("sentinal", sentinalTarget);
            LogManager.Configuration.LoggingRules.Add(sentinalRule);

            // Setup the logging view for Harvester - http://harvester.codeplex.com
            var harvesterTarget = new OutputDebugStringTarget()
            {
                Name = "harvester",
                Layout = "${log4jxmlevent:includeNLogData=false}"
            };
            var harvesterRule = new LoggingRule("*", LogLevel.Trace, harvesterTarget);
            LogManager.Configuration.AddTarget("harvester", harvesterTarget);
            LogManager.Configuration.LoggingRules.Add(harvesterRule);
#endif

            string logDirectory = Environment.GetEnvironmentVariable("EVENTHUB_LOG_DIR");
            if (!string.IsNullOrEmpty(logDirectory))
            {
                string logFilePath = Path.Combine(logDirectory, "Debug.log");
                var fileTarget = new FileTarget
                {
                    FileName = logFilePath,
                    Layout =
                        "${longdate} - ${level:uppercase=true}: ${message}${onexception:${newline}EXCEPTION\\: ${exception:format=ToString}}",

                    KeepFileOpen = false,
                    ArchiveFileName = Path.Combine(logFilePath,
                        "Debug_${shortdate}.{##}.log"),
                    ArchiveNumbering = ArchiveNumberingMode.Sequence,
                    ArchiveEvery = FileArchivePeriod.Day,
                    MaxArchiveFiles = 30
                };

                var fileLoggingRUle = new LoggingRule("*", LogLevel.Trace, fileTarget);
                LogManager.Configuration.AddTarget("file", fileTarget);
                LogManager.Configuration.LoggingRules.Add(fileLoggingRUle);
            }

            LogManager.ReconfigExistingLoggers();

            Instance = LogManager.GetCurrentClassLogger();
        }
        public static Logger Instance { get; private set; }
    }
}
