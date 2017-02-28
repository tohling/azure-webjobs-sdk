// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Host.Extensions
{
    /// <summary>
    /// Base class for extensions 
    /// </summary>
    public abstract class ExtensionBase
    {
        /// <summary>
        /// Get the set of attributes exposed from this extension. 
        /// </summary>
        public abstract IEnumerable<Type> ExposedAttributes { get; }

        /// <summary>
        /// Set of resolved assemblies. 
        /// </summary>
        public IEnumerable<Assembly> ResolvedAssemblies { get; set; }

        /// <summary>
        /// Initialize this extension. This can add binding rules to the configuration.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="hostMetadata"></param>
        /// <returns></returns>
        protected internal abstract Task InitializeAsync(JobHostConfiguration config, JObject hostMetadata);
    }
}
