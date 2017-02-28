// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Extensions;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.IO;
using System.Reflection;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class Class1
    {
        [Fact]
        public async Task Test()
        {
            JobHostConfiguration config = TestHelpers.NewConfig();

            var ext = new TestExtension();
            await config.AddExtensionAsync(ext, null);

            var tooling = await config.GetToolingAsync();

            // Fact that we registered a Widget converter is enough to add the assembly 
            Assembly asm = tooling.TryResolveAssembly(typeof(Widget).Assembly.GetName().Name);
            Assert.Same(asm, typeof(Widget).Assembly);

            var extensions = tooling.Extensions.ToArray();
            Assert.Equal(1, extensions.Length);
            Assert.Equal(ext, extensions[0]);

            var attrType = tooling.GetAttributeTypeFromName("Test");
            Assert.Equal(typeof(TestAttribute), attrType);

            // JObject --> Attribute 
            var attrs = tooling.GetAttributes(attrType, JObject.FromObject(new { Flag = "xyz" }));
            TestAttribute attr = (TestAttribute)attrs[0];
            Assert.Equal("xyz", attr.Flag);

            // Getting default type. 
            var defaultType = tooling.GetDefaultType(attr, FileAccess.Read, typeof(object));
            Assert.Equal(typeof(JObject), defaultType);

            Assert.Throws<InvalidOperationException>(() => tooling.GetDefaultType(attr, FileAccess.Write, typeof(object)));
        }

        static T GetAttr<T>(ITooling tooling, object obj) where T : Attribute
        {
            var attributes = tooling.GetAttributes(typeof(T), JObject.FromObject(obj));
            var attr = (T)attributes[0];

            return attr;
        }

        [Fact]
        public async Task AttrBuilder()
        {
            JobHostConfiguration config = TestHelpers.NewConfig();
            var tooling = await config.GetToolingAsync();

            var queueAttr = GetAttr<QueueAttribute>(tooling, new { QueueName = "q1" });
            Assert.Equal("q1", queueAttr.QueueName);

            var tableAttr = GetAttr<TableAttribute>(tooling, new { TableName = "t1" });
            Assert.Equal("t1", tableAttr.TableName);

            tableAttr = GetAttr<TableAttribute>(tooling, new { TableName = "t1", partitionKey ="pk", Filter="f1" });
            Assert.Equal("t1", tableAttr.TableName);
            Assert.Equal("pk", tableAttr.PartitionKey);
            Assert.Equal(null, tableAttr.RowKey);
            Assert.Equal("f1", tableAttr.Filter);
        }


        public class TestAttribute : Attribute
        {
            public TestAttribute(string flag)
            {
                this.Flag = flag;
            }
            public string Flag { get; set; }
        }

        class Widget
        {
            public string Value;
        }

        public class TestExtension : ExtensionBase, 
            IConverter<TestAttribute, Widget>
        {
            public override IEnumerable<Type> ExposedAttributes
            {
                get
                {
                    return new Type[] { typeof(TestAttribute) };
                }
            }

            protected internal override Task InitializeAsync(JobHostConfiguration config, JObject hostMetadata)
            {
                var bf = config.BindingFactory;
                bf.BindToInput<TestAttribute, Widget>(this);

                var cm = config.ConverterManager;
                cm.AddConverter<Widget, JObject>(widget => JObject.FromObject(widget));

                return Task.FromResult(0);
            }

            Widget IConverter<TestAttribute, Widget>.Convert(TestAttribute input)
            {
                return new Widget { Value = input.Flag };
            }
        }
    }
}
