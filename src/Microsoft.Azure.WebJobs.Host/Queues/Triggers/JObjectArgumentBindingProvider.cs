// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Storage.Queue;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Host.Queues.Triggers
{
    internal class JObjectArgumentBindingProvider : IQueueTriggerArgumentBindingProvider
    {
        public ITriggerDataArgumentBinding<IStorageQueueMessage> TryCreate(ParameterInfo parameter)
        {
            return new JObjectArgumentBinding();
        }

        private class JObjectArgumentBinding : ITriggerDataArgumentBinding<IStorageQueueMessage>
        {
            public JObjectArgumentBinding()
            {
            }

            public Type ValueType
            {
                get { return typeof(JObject); }
            }

            public IReadOnlyDictionary<string, Type> BindingDataContract
            {
                // For JObject bindings, we only know the contract at runtime, not at index time
                get { return null; }
            }

            public Task<ITriggerData> BindAsync(IStorageQueueMessage value, ValueBindingContext context)
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }

                JObject convertedValue;
                try
                {
                    convertedValue = JObject.Parse(value.AsString);
                }
                catch (JsonException e)
                {
                    // Easy to have the queue payload not deserialize properly. So give a useful error. 
                    string msg = String.Format(CultureInfo.CurrentCulture,
@"Binding parameters to complex objects (such as JObject) uses Json.NET serialization. 
1. Bind the parameter type as 'string' instead of 'JObject' to get the raw values and avoid JSON deserialization, or
2. Change the queue payload to be valid json. The JSON parser failed: {0}
", e.Message);
                    throw new InvalidOperationException(msg);
                }

                IValueProvider provider = new QueueMessageValueProvider(value, convertedValue, ValueType);

                Dictionary<string, object> bindingData = new Dictionary<string, object>();
                foreach (var property in convertedValue.Properties())
                {
                    JValue propertyValue = (JValue)property.Value;
                    bindingData.Add(property.Name, propertyValue.Value);
                }

                return Task.FromResult<ITriggerData>(new TriggerData(provider, bindingData));
            }
        }
    }
}
