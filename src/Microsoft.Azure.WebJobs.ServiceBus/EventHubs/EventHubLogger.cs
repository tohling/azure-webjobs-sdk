// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.ServiceBus.EventHubs
{
    /// <summary>
    /// The EventHub logger
    /// </summary>
    public sealed class EventHubLogger
    {
        private static readonly EventHubLogger Instance = new EventHubLogger();
        /*
        private static string _endpointUrl;
        private static string _primaryKey;
        private static string _databaseName;
        private static string _collectionName;
        */

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        private EventHubLogger()
        {
            /*
            _endpointUrl = Environment.GetEnvironmentVariable("AzureDocumentDbEndpoint");
            _primaryKey = Environment.GetEnvironmentVariable("AzureDocumentDbPrimaryKey");
            _databaseName = Environment.GetEnvironmentVariable("AzureDocumentDbLogDatabase");
            _collectionName = Environment.GetEnvironmentVariable("AzureDocumentDbLogCollection");

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
            */
        }

        /// <summary>
        /// The EventHub logger instance
        /// </summary>
        public static EventHubLogger LoggerInstance
        {
            get { return Instance; }
        }

        /// <summary>
        /// Logs the message.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="logType"></param>
        /// <param name="message"></param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "message")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "source")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "logType")]
        public void LogMessage(string source, LogType logType, string message)
        {
            // CreateDocumentAsync(source, logType, message).Wait();
        }

        /*
        private static async Task CreateDocumentAsync(string source, LogType logType, string message)
        {
            using (DocumentClient client = new DocumentClient(new Uri(_endpointUrl), _primaryKey))
            {
                var database =
                    client.CreateDatabaseQuery("SELECT * FROM c WHERE c.id ='EventHubLogDb'").AsEnumerable().First();
                LogMessage logMessage = new LogMessage(Guid.NewGuid(), source, logType, DateTime.UtcNow, message);
                await CreateLogDocumentAsync(client, _databaseName, _collectionName, logMessage);
                Console.WriteLine("Created document {0} from dynamic object", logMessage.Id);
                Console.WriteLine();
            }
        }

        private static async Task CreateLogDocumentAsync(DocumentClient client, string databaseName, string collectionName, LogMessage message)
        {
            await
                client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseName, collectionName), message);
        }
        */
    }
}
