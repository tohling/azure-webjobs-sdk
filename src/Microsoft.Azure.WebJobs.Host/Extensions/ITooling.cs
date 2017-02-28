// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Azure.WebJobs.Host.Extensions;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Tooling interface for script 
    /// </summary>
    public interface ITooling
    {
        /// <summary>
        /// GEt the list of registered extensions 
        /// </summary>
        IEnumerable<ExtensionBase> Extensions { get; }

        /// <summary>
        /// "Blob" --> typeof(BlobAttribute)
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        Type GetAttributeTypeFromName(string name);

        /// <summary>
        /// Create an attribute from the metadata.
        /// (using AttributeCloner). 
        /// </summary>
        /// <param name="attributeType"></param>
        /// <param name="metadata"></param>
        /// <returns></returns>
        Attribute[] GetAttributes(Type attributeType, JObject metadata);

        /// <summary>
        /// Get a 'default type' that can be used in scripting scenarios.
        /// This is biased to returning JObject, Streams, and BCL types 
        /// which can be converted to a loosely typed object in script languages. 
        /// </summary>
        /// <param name="attribute"></param>
        /// <param name="access"></param>
        /// <param name="requestedType"></param>
        /// <returns></returns>
        Type GetDefaultType(
                Attribute attribute,
                FileAccess access, // direction In, Out, In/Out
                Type requestedType); // combination of Cardinality and DataType            
    }
}
