// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.ServiceBus.EventHubs
{
    /// <summary>
    /// The log type
    /// </summary>
    public class LogMessage
    {
        /// <summary>
        /// The log message.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="source"></param>
        /// <param name="logType"></param>
        /// <param name="timestamp"></param>
        /// <param name="message"></param>
        public LogMessage(Guid id, string source, LogType logType, DateTime timestamp, string message)
        {
            this.Id = id;
            this.Source = source;
            this.LogType = logType.ToString();
            this.Timestamp = timestamp;
            this.Message = message;
        }

        /// <summary>
        /// The id
        /// </summary>
        [JsonProperty("id")]
        public Guid Id { get; private set; }

        /// <summary>
        /// The source
        /// </summary>
        [JsonProperty("source")]
        public string Source { get; private set; }

        /// <summary>
        /// The log level
        /// </summary>
        [JsonProperty("loglevel")]
        public string LogType { get; private set; }

        /// <summary>
        /// The time stamp
        /// </summary>
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; private set; }

        /// <summary>
        /// The message
        /// </summary>
        [JsonProperty("message")]
        public string Message { get; private set; }
    }
}
