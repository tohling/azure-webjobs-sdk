// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Extensions;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Host
{
    internal class Tooling : ITooling
    {
        private readonly List<ExtensionBase> _extensionList = new List<ExtensionBase>();
        // Mapping from Attribute type to extension. 
        private readonly IDictionary<Type, ExtensionBase> _extensions = new Dictionary<Type, ExtensionBase>();

        // Map from binding types to their corresponding attribute. 
        private readonly IDictionary<string, Type> _attributeTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        // Map of assembly name to assembly.
        private readonly Dictionary<string, Assembly> _resolvedAssemblies = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        private readonly JobHostConfiguration _config;

        // preferred order of output bindings. 
        private static Type[] _outTypes = new Type[]
        {
            typeof(IAsyncCollector<JObject>),
            typeof(IAsyncCollector<byte[]>),
            typeof(IAsyncCollector<string>),
            typeof(Stream)
        };

        private static Type[] _inTypes = new Type[]
        {
            typeof(JObject),
            typeof(JArray),
            typeof(Stream),
            typeof(string)
        };

        private IBindingProvider _root;

        public Tooling(JobHostConfiguration config)
        {
            _config = config;
        }

        public IEnumerable<ExtensionBase> Extensions
        {
            get
            {
                return _extensionList;
            }
        }

        internal void Init(IBindingProvider root)
        {
            this._root = root;

            // Populate assembly resolution from converters.
            var converter = this._config.GetService<IConverterManager>() as ConverterManager;
            if (converter != null)
            {
                converter.AddAssemblies(this._resolvedAssemblies);
            }
        }

        public Assembly TryResolveAssembly(string assemblyName)
        {
            Assembly assembly;
            _resolvedAssemblies.TryGetValue(assemblyName, out assembly);
            return assembly;
        }

        /// <summary>
        /// </summary>
        /// <param name="extension"></param>
        /// <param name="hostMetadata"></param>
        public async Task AddExtensionAsync(ExtensionBase extension, JObject hostMetadata)
        {
            var attributeTypes = extension.ExposedAttributes;
            foreach (var attributeType in attributeTypes)
            {
                string bindingName = GetNameFromAttribute(attributeType);
                this._attributeTypes[bindingName] = attributeType;
                this._extensions[attributeType] = extension;
            }

            if (extension.ResolvedAssemblies != null)
            {
                foreach (var resolvedAssembly in extension.ResolvedAssemblies)
                {
                    string name = resolvedAssembly.GetName().Name;
                    _resolvedAssemblies[name] = resolvedAssembly;
                }
            }

            _extensionList.Add(extension);
            await extension.InitializeAsync(_config, hostMetadata);
        }

        // By convention, typeof(EventHubAttribute) --> "EventHub"
        private static string GetNameFromAttribute(Type attributeType)
        {
            string fullname = attributeType.Name; // no namespace
            const string Suffix = "Attribute";

            if (!fullname.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Attribute type '" + fullname + "' must end in 'Attribute'");
            }
            string name = fullname.Substring(0, fullname.Length - Suffix.Length);
            return name;
        }

        public Type GetAttributeTypeFromName(string name)
        {
            Type attrType;
            if (_attributeTypes.TryGetValue(name, out attrType))
            {
                return attrType;
            }
            //throw new InvalidOperationException("Unknown binding type: " + name);
            return null;
        }

        public Attribute[] GetAttributes(Type attributeType, JObject metadata)
        {
            var resolve = AttributeCloner.CreateDirect(attributeType, metadata, null);
            return new Attribute[] { resolve };
        }

        private bool CanBind(Attribute attribute, Type parameterType)
        {
            IBindingProvider root = this._root;

            bool result = Task.Run(() => ScriptHelpers.CanBindAsync(root, attribute, parameterType)).GetAwaiter().GetResult();

            return result;
        }
               
        // Get a better implementation 
        public Type GetDefaultType(
            Attribute attribute,
            FileAccess access, // direction In, Out, In/Out
            Type requestedType) // combination of Cardinality and DataType            
        {
            if (attribute == null)
            {
                throw new ArgumentNullException(nameof(attribute));
            }
            if (access == FileAccess.Write)
            {
                var outType = _outTypes.FirstOrDefault(type => this.CanBind(attribute, type));
                if (outType != null)
                {
                    return outType;
                }
                // Error! 
                throw new InvalidOperationException($"Can't bind {attribute.GetType().Name} to a script-compatible  output.");
            }
            else if (access == FileAccess.Read)
            {
                if (IsBinary(requestedType))
                {
                    return typeof(byte[]);
                }

                var array = IsCardinalityMany(requestedType);
                if (array != null)
                {
                    var inner = this.GetDefaultType(attribute, FileAccess.Read, array);
                    if (inner != null)
                    {
                        return inner.MakeArrayType();
                    }
                }

                var inType = _inTypes.FirstOrDefault(type => this.CanBind(attribute, type));
                if (inType != null)
                {
                    return inType;
                }
                throw new InvalidOperationException($"Can't bind {attribute.GetType().Name} to a script-compatible input.");
            }
            else
            {
                throw new NotImplementedException($"Can't bind {attribute.GetType().Name} to an In/Out parameter.");
            }
        }

        // Return null if cardinality is singular. 
        // Return the element type if cardinality is Many
        private static Type IsCardinalityMany(Type requestedType)
        {
            if (IsBinary(requestedType))
            {
                return null;
            }
            if (requestedType.IsArray)
            {
                return requestedType.GetElementType();
            }
            return null;
        }

        private static bool IsBinary(Type requestedType)
        {
            return requestedType == typeof(byte[]);
        }

        // Helpers for testing some Script Scenarios 
        public static class ScriptHelpers
        {
            public static async Task<bool> CanBindAsync(IBindingProvider provider, Attribute attribute, Type t)
            {
                ParameterInfo parameterInfo = new FakeParameterInfo(
                    t,
                    new FakeMemberInfo(),
                    attribute);

                BindingProviderContext bindingProviderContext = new BindingProviderContext(
                    parameterInfo, bindingDataContract: null, cancellationToken: CancellationToken.None);

                try
                {
                    var binding = await provider.TryCreateAsync(bindingProviderContext);
                    if (binding == null)
                    {
                        return false;
                    }
                }
                catch (Exception)
                {
                    return false;
                }
                return true;
            }

            // A non-reflection based implementation
            private class FakeParameterInfo : ParameterInfo
            {
                private readonly Collection<Attribute> _attributes = new Collection<Attribute>();

                public FakeParameterInfo(Type parameterType, MemberInfo memberInfo, Attribute attribute)
                {
                    ClassImpl = parameterType;
                    AttrsImpl = ParameterAttributes.In;
                    NameImpl = "?";
                    MemberImpl = memberInfo;

                    // union all the parameter attributes
                    _attributes.Add(attribute);
                }

                public override object[] GetCustomAttributes(Type attributeType, bool inherit)
                {
                    return _attributes.Where(p => p.GetType() == attributeType).ToArray();
                }
            } // end class FakeParameterInfo

            // Reflection requires the Parameter's member property is mocked out. 
            private class FakeMemberInfo : MemberInfo
            {
                public override Type DeclaringType
                {
                    get
                    {
                        throw new NotImplementedException();
                    }
                }

                public override MemberTypes MemberType
                {
                    get
                    {
                        return MemberTypes.All;
                    }
                }

                public override string Name
                {
                    get
                    {
                        throw new NotImplementedException();
                    }
                }

                public override Type ReflectedType
                {
                    get
                    {
                        throw new NotImplementedException();
                    }
                }

                public override object[] GetCustomAttributes(bool inherit)
                {
                    throw new NotImplementedException();
                }

                public override object[] GetCustomAttributes(Type attributeType, bool inherit)
                {
                    throw new NotImplementedException();
                }

                public override bool IsDefined(Type attributeType, bool inherit)
                {
                    throw new NotImplementedException();
                }
            }
        } // end ScriptHelpers
    }
}