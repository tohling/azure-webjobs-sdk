// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    internal class AttributeCloner
    {
        // $$$ Merge this with other methods on this class. 
        // This is separate from AttributeCloner<T>  since this method is not generic. 
        public static Attribute CreateDirect(Type attributeType, JObject properties, INameResolver resolver)
        {
            Type t = attributeType;

            ConstructorInfo bestCtor = null;
            int longestMatch = -1;
            object[] ctorArgs = null;

            // Pick the ctor with the longest parameter list where all parameters are matched.
            var ctors = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            foreach (var ctor in ctors)
            {
                var ps = ctor.GetParameters();
                int len = ps.Length;

                List<object> possibleCtorArgs = new List<object>();

                bool hasAllParameters = true;
                for (int i = 0; i < len; i++)
                {
                    var p = ps[i];

                    JToken token;
                    if (!properties.TryGetValue(p.Name, StringComparison.OrdinalIgnoreCase, out token))
                    {
                        // Missing a parameter for this ctor; try the next one. 
                        hasAllParameters = false;
                        break;
                    }
                    else
                    {
                        var attr = p.GetCustomAttribute<AutoResolveAttribute>();
                        var obj = ApplyNameResolver(resolver, null, attr, token, p.ParameterType);
                        possibleCtorArgs.Add(obj);
                    }
                }

                if (hasAllParameters)
                {
                    if (len > longestMatch)
                    {
                        bestCtor = ctor;
                        ctorArgs = possibleCtorArgs.ToArray();
                        longestMatch = len;
                    }
                }
            }

            if (bestCtor == null)
            {
                // error!!!
                throw new InvalidOperationException("Can't figure out which ctor to call.");
            }

            // Apply writeable properties. 
            var newAttr = (Attribute)bestCtor.Invoke(ctorArgs);

            foreach (var prop in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (prop.CanWrite)
                {
                    JToken token;
                    if (properties.TryGetValue(prop.Name, StringComparison.OrdinalIgnoreCase, out token))
                    {
                        var attr = prop.GetCustomAttribute<AutoResolveAttribute>();
                        var obj = ApplyNameResolver(resolver, null, attr, token, prop.PropertyType);
                        prop.SetValue(newAttr, obj);
                    }
                }
            }

            return newAttr;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "propInfo")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "nameResolver")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "attr")]
        private static object ApplyNameResolver(
            INameResolver nameResolver, 
            PropertyInfo propInfo, 
            AutoResolveAttribute attr, 
            JToken originalValue, 
            Type type)
        {
            var obj = originalValue.ToObject(type);
            /*
            if (type == typeof(string))
            {
                return ApplyNameResolver(nameResolver, propInfo, attr, obj.ToString());
            }
            else
            */
            {
                return obj;
            }
        }
    }
}