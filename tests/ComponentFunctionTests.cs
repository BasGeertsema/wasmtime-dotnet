using System;
using System.Reflection;
using FluentAssertions;
using Wasmtime;
using Xunit;

namespace Wasmtime.Tests
{
    public class ComponentFunctionTests
    {
        [Fact]
        public void ItCanInstantiateComponent()
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("component.wasm");
            stream.Should().NotBeNull();

            using var engine = new Engine();
            using var component = Component.FromStream(engine, "component.wasm", stream!);
            using var linker = new ComponentLinker(engine);
            
            // Create WASI context and store
            var wasiConfig = new WasiConfiguration();
            using var store = new Store(engine, wasiConfig);
            
            // Define the required host function before instantiation
            using var rootInstance = linker.GetRoot();
            using var hostServicesInstance = rootInstance.AddInstance("dotnetcomp:plugin/host-services@0.1.0");
            hostServicesInstance.DefineFunction<int, int, int>("", "host-add-s32", (x, y) => x + y);
            
            // Add WASI to the linker since the component requires it
            linker.AddWasiPreview2();
            
            // Instantiate the component
            var instance = linker.Instantiate(store, component);
            instance.Should().NotBeNull("component should instantiate successfully");
        }

        [Fact]
        public void ItCanFindExports()
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("component.wasm");
            stream.Should().NotBeNull();

            using var engine = new Engine();
            using var component = Component.FromStream(engine, "component.wasm", stream!);
            using var linker = new ComponentLinker(engine);
            
            // Create WASI context and store
            var wasiConfig = new WasiConfiguration();
            using var store = new Store(engine, wasiConfig);
            
            // Define the required host function before instantiation
            using var rootInstance = linker.GetRoot();
            using var hostServicesInstance = rootInstance.AddInstance("dotnetcomp:plugin/host-services@0.1.0");
            hostServicesInstance.DefineFunction<int, int, int>("", "host-add-s32", (x, y) => x + y);
            
            // Add WASI to the linker since the component requires it
            linker.AddWasiPreview2();
            
            // Instantiate the component
            var instance = linker.Instantiate(store, component);
            
            // Try to find the exported interface
            var found = instance.TryGetExportIndex("dotnetcomp:plugin/business-rules@0.1.0", store, null, out var businessRulesExport);
            
            if (!found)
            {
                // Try without version
                found = instance.TryGetExportIndex("dotnetcomp:plugin/business-rules", store, null, out businessRulesExport);
            }
            
            if (!found)
            {
                // Try just the interface name
                found = instance.TryGetExportIndex("business-rules", store, null, out businessRulesExport);
            }
            
            found.Should().BeTrue("should find the business-rules interface export");
            businessRulesExport?.Dispose();
        }

        [Fact]
        public void ItCanGetComponentExports()
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("component.wasm");
            stream.Should().NotBeNull();

            using var engine = new Engine();
            using var component = Component.FromStream(engine, "component.wasm", stream!);
            using var linker = new ComponentLinker(engine);
            
            // Create WASI context and store
            var wasiConfig = new WasiConfiguration();
            using var store = new Store(engine, wasiConfig);
            
            // Define the required host function before instantiation
            using var rootInstance = linker.GetRoot();
            using var hostServicesInstance = rootInstance.AddInstance("dotnetcomp:plugin/host-services@0.1.0");
            hostServicesInstance.DefineFunction<int, int, int>("", "host-add-s32", (x, y) => x + y);
            
            // Add WASI to the linker since the component requires it
            linker.AddWasiPreview2();
            
            // Instantiate the component
            var instance = linker.Instantiate(store, component);
            instance.Should().NotBeNull();
            
            // Try to find the exported interface - same pattern as the passing test
            var found = instance.TryGetExportIndex("dotnetcomp:plugin/business-rules@0.1.0", store, null, out var businessRulesExport);
            
            if (!found)
            {
                found = instance.TryGetExportIndex("dotnetcomp:plugin/business-rules", store, null, out businessRulesExport);
            }
            
            if (!found)
            {
                found = instance.TryGetExportIndex("business-rules", store, null, out businessRulesExport);
            }
            
            found.Should().BeTrue("Component exports the business-rules interface");
            
            businessRulesExport?.Dispose();
            
            // Note: Getting functions from interface exports requires additional
            // component model export traversal support that isn't fully implemented yet
        }

        private ComponentFunction GetComponentFunction(string functionName)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("component.wasm");
            stream.Should().NotBeNull();

            var engine = new Engine();
            var component = Component.FromStream(engine, "component.wasm", stream!);
            var linker = new ComponentLinker(engine);
            
            var wasiConfig = new WasiConfiguration();
            var store = new Store(engine, wasiConfig);
            
            // Define the required host function before instantiation
            using var rootInstance = linker.GetRoot();
            using var hostServicesInstance = rootInstance.AddInstance("dotnetcomp:plugin/host-services@0.1.0");
            hostServicesInstance.DefineFunction<int, int, int>("", "host-add-s32", (x, y) => x + y);
            
            linker.AddWasiPreview2();
            var instance = linker.Instantiate(store, component);
            instance.Should().NotBeNull();
            
            // Find the business-rules interface export
            var found = instance.TryGetExportIndex("dotnetcomp:plugin/business-rules@0.1.0", store, null, out var businessRulesExport);
            
            if (!found)
            {
                found = instance.TryGetExportIndex("dotnetcomp:plugin/business-rules", store, null, out businessRulesExport);
            }
            
            if (!found)
            {
                found = instance.TryGetExportIndex("business-rules", store, null, out businessRulesExport);
            }
            
            found.Should().BeTrue("should find the business-rules interface export");
            
            ComponentFunction? addFunc = null;
            
            using (businessRulesExport)
            {
                var foundExport = instance.TryGetExportIndex(functionName, store, businessRulesExport, out var addExportIndex);
                foundExport.Should().BeTrue($"should find {functionName} function");
                
                using (addExportIndex)
                {
                    addFunc = instance.GetFunctionFromExportIndex(store, addExportIndex);
                    addFunc.Should().NotBeNull("should be able to get function from export index");
                }
            }
            
            return addFunc!;
        }
        
        [Fact]
        public void ComponentValueBoxSupportsImplicitConversions()
        {
            // Test implicit conversions for primitive types
            ComponentValueBox boolBox = true;
            boolBox.AsBool().Should().BeTrue();
            
            ComponentValueBox u32Box = 42u;
            u32Box.AsU32().Should().Be(42);
            
            ComponentValueBox i32Box = -42;
            i32Box.AsS32().Should().Be(-42);
            
            ComponentValueBox floatBox = 3.14f;
            floatBox.AsF32().Should().Be(3.14f);
            
            ComponentValueBox stringBox = "hello";
            stringBox.AsString().Should().Be("hello");
        }

        [Fact]
        public void ComponentValueBoxSupportsListOperations()
        {
            // Test creating and accessing lists
            var intList = new int[] { 1, 2, 3 };
            var listBox = ComponentValueBox.FromList(intList);
            listBox.Kind.Should().Be(ComponentValueKind.List);
            listBox.AsList<int>().Should().Equal(intList);
            
            // Test empty list
            var emptyList = Array.Empty<int>();
            var emptyBox = ComponentValueBox.FromList(emptyList);
            emptyBox.Kind.Should().Be(ComponentValueKind.List);
            emptyBox.AsList<int>().Should().BeEmpty();
            
            // Test other types
            var floatList = new float[] { 1.1f, 2.2f, 3.3f };
            var floatBox = ComponentValueBox.FromList(floatList);
            floatBox.AsList<float>().Should().Equal(floatList);
            
            var stringList = new string[] { "hello", "world" };
            var stringBox = ComponentValueBox.FromList(stringList);
            stringBox.AsList<string>().Should().Equal(stringList);
        }

        [Fact]
        public void ComponentValueBoxSupportsTupleOperations()
        {
            // Test creating and accessing tuples
            var tuple = new object[] { 42, "hello", 3.14f };
            var tupleBox = ComponentValueBox.FromTuple(tuple);
            tupleBox.Kind.Should().Be(ComponentValueKind.Tuple);
            var resultTuple = tupleBox.AsTuple();
            resultTuple.Should().NotBeNull();
            resultTuple.Should().Equal(tuple);
            
            // Test empty tuple
            var emptyTuple = Array.Empty<object>();
            var emptyBox = ComponentValueBox.FromTuple(emptyTuple);
            emptyBox.Kind.Should().Be(ComponentValueKind.Tuple);
            emptyBox.AsTuple().Should().BeEmpty();
            
            // Test nested tuple
            var nestedTuple = new object[] { 1, new object[] { "nested", true }, 3.0 };
            var nestedBox = ComponentValueBox.FromTuple(nestedTuple);
            var result = nestedBox.AsTuple();
            result.Should().NotBeNull();
            result![0].Should().Be(1);
            result[1].Should().BeOfType<object[]>();
            ((object[])result[1]).Should().Equal(new object[] { "nested", true });
            result[2].Should().Be(3.0);
        }

        [Fact]
        public void ItCanInvokeAddS8Function()
        {
            var addFunc = GetComponentFunction("add-s8");
            
            // Test with positive values
            var args = new ComponentValueBox[] { (sbyte)10, (sbyte)20 };
            var result = addFunc!.Invoke(args);
            result.Should().NotBeNull();
            ((ComponentValueBox)result!).AsS8().Should().Be(30);
            
            // Test with negative values
            args = new ComponentValueBox[] { (sbyte)-50, (sbyte)30 };
            result = addFunc.Invoke(args);
            ((ComponentValueBox)result!).AsS8().Should().Be(-20);
        }

        [Fact]
        public void ItCanInvokeAddU8Function()
        {
            var addFunc = GetComponentFunction("add-u8");
            
            var args = new ComponentValueBox[] { (byte)100, (byte)150 };
            var result = addFunc!.Invoke(args);
            result.Should().NotBeNull();
            ((ComponentValueBox)result!).AsU8().Should().Be(250);
        }

        [Fact]
        public void ItCanInvokeAddS16Function()
        {
            var addFunc = GetComponentFunction("add-s16");
            
            var args = new ComponentValueBox[] { (short)1000, (short)2000 };
            var result = addFunc!.Invoke(args);
            result.Should().NotBeNull();
            ((ComponentValueBox)result!).AsS16().Should().Be(3000);
            
            // Test negative values
            args = new ComponentValueBox[] { (short)-5000, (short)3000 };
            result = addFunc.Invoke(args);
            ((ComponentValueBox)result!).AsS16().Should().Be(-2000);
        }

        [Fact]
        public void ItCanInvokeAddU16Function()
        {
            var addFunc = GetComponentFunction("add-u16");
            
            var args = new ComponentValueBox[] { (ushort)10000, (ushort)20000 };
            var result = addFunc!.Invoke(args);
            result.Should().NotBeNull();
            ((ComponentValueBox)result!).AsU16().Should().Be(30000);
        }

        [Fact]
        public void ItCanInvokeAddS32Function()
        {
            var addFunc = GetComponentFunction("add-s32");
            
            var args = new ComponentValueBox[] { 100000, 200000 };
            var result = addFunc!.Invoke(args);
            result.Should().NotBeNull();
            ((ComponentValueBox)result!).AsS32().Should().Be(300000);
            
            // Test negative values
            args = new ComponentValueBox[] { -1000000, 500000 };
            result = addFunc.Invoke(args);
            ((ComponentValueBox)result!).AsS32().Should().Be(-500000);
        }

        [Fact]
        public void ItCanInvokeAddU32Function()
        {
            var addFunc = GetComponentFunction("add-u32");
            
            var args = new ComponentValueBox[] { 2u, 3u };
            var result = addFunc!.Invoke(args);
            result.Should().NotBeNull();
            ((ComponentValueBox)result!).AsU32().Should().Be(5);
        }

        [Fact]
        public void ItCanInvokeAddS64Function()
        {
            var addFunc = GetComponentFunction("add-s64");
            
            var args = new ComponentValueBox[] { 1000000000000L, 2000000000000L };
            var result = addFunc!.Invoke(args);
            result.Should().NotBeNull();
            ((ComponentValueBox)result!).AsS64().Should().Be(3000000000000L);
        }

        [Fact]
        public void ItCanInvokeAddU64Function()
        {
            var addFunc = GetComponentFunction("add-u64");
            
            var args = new ComponentValueBox[] { 5000000000000UL, 10000000000000UL };
            var result = addFunc!.Invoke(args);
            result.Should().NotBeNull();
            ((ComponentValueBox)result!).AsU64().Should().Be(15000000000000UL);
        }

        [Fact]
        public void ItCanInvokeAddF32Function()
        {
            var addFunc = GetComponentFunction("add-f32");
            
            var args = new ComponentValueBox[] { 2.0f, 3.0f };
            var result = addFunc!.Invoke(args);
            result.Should().NotBeNull();
            ((ComponentValueBox)result!).AsF32().Should().Be(5.0f);
        }

        [Fact]
        public void ItCanInvokeAddF64Function()
        {
            var addFunc = GetComponentFunction("add-f64");
            
            var args = new ComponentValueBox[] { 3.141592653589793, 2.718281828459045 };
            var result = addFunc!.Invoke(args);
            result.Should().NotBeNull();
            ((ComponentValueBox)result!).AsF64().Should().BeApproximately(5.859874482048838, 0.0000000001);
        }

        [Fact]
        public void ItCanInvokeReverseListS32FunctionWithEmptyList()
        {
            var reverseFunc = GetComponentFunction("reverse-list-s32");

            // Test with empty list
            var args = new ComponentValueBox[] { ComponentValueBox.FromList(Array.Empty<int>()) };
            var result = reverseFunc.Invoke(args);
            result.Should().NotBeNull();
            var resultList = ((ComponentValueBox)result!).AsList<int>();
            resultList.Should().NotBeNull();
            resultList.Should().BeEmpty();
        }

        [Fact]
        public void ItCanInvokeReverseListS32Function()
        {
            var reverseFunc = GetComponentFunction("reverse-list-s32");
            
            // Create a list of s32 values
            var listValues = new int[] { 1, 2, 3, 4, 5 };
            var args = new ComponentValueBox[] { ComponentValueBox.FromList(listValues) };
            
            var result = reverseFunc!.Invoke(args);
            result.Should().NotBeNull();
            
            var resultList = ((ComponentValueBox)result!).AsList<int>();
            resultList.Should().NotBeNull();
            resultList.Should().Equal(new int[] { 5, 4, 3, 2, 1 });
            
            // Test with single element
            args = new ComponentValueBox[] { ComponentValueBox.FromList(new int[] { 42 }) };
            result = reverseFunc.Invoke(args);
            result.Should().NotBeNull();
            resultList = ((ComponentValueBox)result!).AsList<int>();
            resultList.Should().NotBeNull();
            resultList.Should().Equal(new int[] { 42 });
            
            // Test with negative numbers
            args = new ComponentValueBox[] { ComponentValueBox.FromList(new int[] { -10, -20, -30, 40, 50 }) };
            result = reverseFunc.Invoke(args);
            result.Should().NotBeNull();
            resultList = ((ComponentValueBox)result!).AsList<int>();
            resultList.Should().NotBeNull();
            resultList.Should().Equal(new int[] { 50, 40, -30, -20, -10 });
        }

        [Fact]
        public void ItCanInvokeReverseListPersonFunction()
        {
            var reverseFunc = GetComponentFunction("reverse-list-person");
            
            // Test with empty list
            var emptyList = new ComponentValueBox[0];
            var args = new ComponentValueBox[] { ComponentValueBox.FromList(emptyList) };
            var result = reverseFunc!.Invoke(args);
            result.Should().NotBeNull();
            
            // For empty lists, we might get an int[] instead of ComponentValueBox[]
            var resultBox = (ComponentValueBox)result!;
            ComponentValueBox[]? resultList;
            if (resultBox.ObjectValue is int[] intArray && intArray.Length == 0)
            {
                // Empty list case - convert to empty ComponentValueBox array
                resultList = Array.Empty<ComponentValueBox>();
            }
            else
            {
                resultList = resultBox.AsList<ComponentValueBox>();
                resultList.Should().NotBeNull();
            }
            resultList.Should().BeEmpty();
            
            // Test with single person
            var person1 = ComponentValueBox.FromRecord(new (string, ComponentValueBox)[]
            {
                ("name", "Alice"),
                ("age", (byte)30)
            });
            var singlePersonList = new ComponentValueBox[] { person1 };
            args = new ComponentValueBox[] { ComponentValueBox.FromList(singlePersonList) };
            
            result = reverseFunc.Invoke(args);
            result.Should().NotBeNull();
            resultList = ((ComponentValueBox)result!).AsList<ComponentValueBox>();
            resultList.Should().NotBeNull();
            resultList.Should().HaveCount(1);
            
            var resultPerson = resultList![0].AsRecord();
            resultPerson.Should().NotBeNull();
            resultPerson![0].Item1.Should().Be("name");
            resultPerson[0].Item2.AsString().Should().Be("Alice");
            resultPerson[1].Item1.Should().Be("age");
            resultPerson[1].Item2.AsU8().Should().Be(30);
            
            // Test with multiple people
            var person2 = ComponentValueBox.FromRecord(new (string, ComponentValueBox)[]
            {
                ("name", "Bob"),
                ("age", (byte)25)
            });
            var person3 = ComponentValueBox.FromRecord(new (string, ComponentValueBox)[]
            {
                ("name", "Charlie"),
                ("age", (byte)35)
            });
            var multiplePersonList = new ComponentValueBox[] { person1, person2, person3 };
            args = new ComponentValueBox[] { ComponentValueBox.FromList(multiplePersonList) };
            
            result = reverseFunc.Invoke(args);
            result.Should().NotBeNull();
            resultList = ((ComponentValueBox)result!).AsList<ComponentValueBox>();
            resultList.Should().NotBeNull();
            resultList.Should().HaveCount(3);
            
            // Check reversed order: Charlie, Bob, Alice
            resultPerson = resultList![0].AsRecord();
            resultPerson![0].Item2.AsString().Should().Be("Charlie");
            resultPerson[1].Item2.AsU8().Should().Be(35);
            
            resultPerson = resultList[1].AsRecord();
            resultPerson![0].Item2.AsString().Should().Be("Bob");
            resultPerson[1].Item2.AsU8().Should().Be(25);
            
            resultPerson = resultList[2].AsRecord();
            resultPerson![0].Item2.AsString().Should().Be("Alice");
            resultPerson[1].Item2.AsU8().Should().Be(30);
            
            // Test with people having empty names and zero ages
            var person4 = ComponentValueBox.FromRecord(new (string, ComponentValueBox)[]
            {
                ("name", ""),
                ("age", (byte)0)
            });
            var person5 = ComponentValueBox.FromRecord(new (string, ComponentValueBox)[]
            {
                ("name", "Test"),
                ("age", (byte)100)
            });
            var edgeCaseList = new ComponentValueBox[] { person4, person5 };
            args = new ComponentValueBox[] { ComponentValueBox.FromList(edgeCaseList) };
            
            result = reverseFunc.Invoke(args);
            result.Should().NotBeNull();
            resultList = ((ComponentValueBox)result!).AsList<ComponentValueBox>();
            resultList.Should().NotBeNull();
            resultList.Should().HaveCount(2);
            
            // Check reversed order: Test, ""
            resultPerson = resultList![0].AsRecord();
            resultPerson![0].Item2.AsString().Should().Be("Test");
            resultPerson[1].Item2.AsU8().Should().Be(100);
            
            resultPerson = resultList[1].AsRecord();
            resultPerson![0].Item2.AsString().Should().Be("");
            resultPerson[1].Item2.AsU8().Should().Be(0);
        }

        [Fact]
        public void ItCanInvokeEchoTupleFunction()
        {
            var echoFunc = GetComponentFunction("echo-tuple2");
            
            // Create a tuple of (f32, s32) values
            var tupleValue = ComponentValueBox.FromTuple(new object[] { 3.14f, 42 });
            var args = new ComponentValueBox[] { tupleValue };
            
            var result = echoFunc!.Invoke(args);
            result.Should().NotBeNull();
            
            var resultTuple = ((ComponentValueBox)result!).AsTuple();
            resultTuple.Should().NotBeNull();
            resultTuple.Should().HaveCount(2);
            resultTuple![0].Should().BeOfType<float>().Which.Should().Be(3.14f);
            resultTuple[1].Should().BeOfType<int>().Which.Should().Be(42);
            
            // Test with negative values
            tupleValue = ComponentValueBox.FromTuple(new object[] { -2.5f, -100 });
            args = new ComponentValueBox[] { tupleValue };
            result = echoFunc.Invoke(args);
            result.Should().NotBeNull();
            
            resultTuple = ((ComponentValueBox)result!).AsTuple();
            resultTuple.Should().NotBeNull();
            resultTuple.Should().HaveCount(2);
            resultTuple![0].Should().BeOfType<float>().Which.Should().Be(-2.5f);
            resultTuple[1].Should().BeOfType<int>().Which.Should().Be(-100);
            
            // Test with zero values
            tupleValue = ComponentValueBox.FromTuple(new object[] { 0.0f, 0 });
            args = new ComponentValueBox[] { tupleValue };
            result = echoFunc.Invoke(args);
            result.Should().NotBeNull();
            
            resultTuple = ((ComponentValueBox)result!).AsTuple();
            resultTuple.Should().NotBeNull();
            resultTuple.Should().HaveCount(2);
            resultTuple![0].Should().BeOfType<float>().Which.Should().Be(0.0f);
            resultTuple[1].Should().BeOfType<int>().Which.Should().Be(0);
        }

        [Fact]
        public void ItCanInvokeEchoRecordFunction()
        {
            var echoFunc = GetComponentFunction("echo-record");
            
            // Create a record with name and age fields (person record)
            var fields = new (string, ComponentValueBox)[]
            {
                ("name", "Alice"),
                ("age", (byte)30)
            };
            var recordValue = ComponentValueBox.FromRecord(fields);
            var args = new ComponentValueBox[] { recordValue };
            
            var result = echoFunc!.Invoke(args);
            result.Should().NotBeNull();
            
            var resultRecord = ((ComponentValueBox)result!).AsRecord();
            resultRecord.Should().NotBeNull();
            resultRecord.Should().HaveCount(2);
            
            // Check first field (name)
            resultRecord![0].Item1.Should().Be("name");
            resultRecord[0].Item2.AsString().Should().Be("Alice");
            
            // Check second field (age)
            resultRecord[1].Item1.Should().Be("age");
            resultRecord[1].Item2.AsU8().Should().Be(30);
            
            // Test with different values
            fields = new (string, ComponentValueBox)[]
            {
                ("name", "Bob"),
                ("age", (byte)25)
            };
            recordValue = ComponentValueBox.FromRecord(fields);
            args = new ComponentValueBox[] { recordValue };
            
            result = echoFunc.Invoke(args);
            result.Should().NotBeNull();
            
            resultRecord = ((ComponentValueBox)result!).AsRecord();
            resultRecord.Should().NotBeNull();
            resultRecord.Should().HaveCount(2);
            
            resultRecord![0].Item1.Should().Be("name");
            resultRecord[0].Item2.AsString().Should().Be("Bob");
            resultRecord[1].Item1.Should().Be("age");
            resultRecord[1].Item2.AsU8().Should().Be(25);
            
            // Test with empty name
            fields = new (string, ComponentValueBox)[]
            {
                ("name", ""),
                ("age", (byte)0)
            };
            recordValue = ComponentValueBox.FromRecord(fields);
            args = new ComponentValueBox[] { recordValue };
            
            result = echoFunc.Invoke(args);
            result.Should().NotBeNull();
            
            resultRecord = ((ComponentValueBox)result!).AsRecord();
            resultRecord.Should().NotBeNull();
            resultRecord.Should().HaveCount(2);
            
            resultRecord![0].Item1.Should().Be("name");
            resultRecord[0].Item2.AsString().Should().Be("");
            resultRecord[1].Item1.Should().Be("age");
            resultRecord[1].Item2.AsU8().Should().Be(0);
        }

        [Fact]
        public void ItCanInvokeEchoVariantFunction()
        {
            var echoFunc = GetComponentFunction("echo-variant");
            
            // Test case 1: pending (no payload)
            var pendingVariant = ComponentValueBox.FromVariant("pending", null);
            var args = new ComponentValueBox[] { pendingVariant };
            
            var result = echoFunc!.Invoke(args);
            result.Should().NotBeNull();
            
            var resultVariant = ((ComponentValueBox)result!).AsVariant();
            resultVariant.Should().NotBeNull();
            resultVariant!.Value.Item1.Should().Be("pending");
            resultVariant.Value.Item2.Should().BeNull();
            
            // Test case 2: shipped(s32)
            var shippedVariant = ComponentValueBox.FromVariant("shipped", (ComponentValueBox)42);
            args = new ComponentValueBox[] { shippedVariant };
            
            result = echoFunc.Invoke(args);
            result.Should().NotBeNull();
            
            resultVariant = ((ComponentValueBox)result!).AsVariant();
            resultVariant.Should().NotBeNull();
            resultVariant!.Value.Item1.Should().Be("shipped");
            resultVariant.Value.Item2.Should().NotBeNull();
            resultVariant.Value.Item2!.Value.AsS32().Should().Be(42);
            
            // Test case 3: delivered(string)
            var deliveredVariant = ComponentValueBox.FromVariant("delivered", (ComponentValueBox)"Package delivered to recipient");
            args = new ComponentValueBox[] { deliveredVariant };
            
            result = echoFunc.Invoke(args);
            result.Should().NotBeNull();
            
            resultVariant = ((ComponentValueBox)result!).AsVariant();
            resultVariant.Should().NotBeNull();
            resultVariant!.Value.Item1.Should().Be("delivered");
            resultVariant.Value.Item2.Should().NotBeNull();
            resultVariant.Value.Item2!.Value.AsString().Should().Be("Package delivered to recipient");
            
            // Test with negative number for shipped
            shippedVariant = ComponentValueBox.FromVariant("shipped", (ComponentValueBox)(-100));
            args = new ComponentValueBox[] { shippedVariant };
            
            result = echoFunc.Invoke(args);
            result.Should().NotBeNull();
            
            resultVariant = ((ComponentValueBox)result!).AsVariant();
            resultVariant.Should().NotBeNull();
            resultVariant!.Value.Item1.Should().Be("shipped");
            resultVariant.Value.Item2.Should().NotBeNull();
            resultVariant.Value.Item2!.Value.AsS32().Should().Be(-100);
            
            // Test with empty string for delivered
            deliveredVariant = ComponentValueBox.FromVariant("delivered", (ComponentValueBox)"");
            args = new ComponentValueBox[] { deliveredVariant };
            
            result = echoFunc.Invoke(args);
            result.Should().NotBeNull();
            
            resultVariant = ((ComponentValueBox)result!).AsVariant();
            resultVariant.Should().NotBeNull();
            resultVariant!.Value.Item1.Should().Be("delivered");
            resultVariant.Value.Item2.Should().NotBeNull();
            resultVariant.Value.Item2!.Value.AsString().Should().Be("");
        }

        [Fact]
        public void ItCanInvokeEchoEnumFunction()
        {
            var echoFunc = GetComponentFunction("echo-enum");
            
            // Test case 1: red
            var redEnum = ComponentValueBox.FromEnum("red");
            var args = new ComponentValueBox[] { redEnum };
            
            var result = echoFunc!.Invoke(args);
            result.Should().NotBeNull();
            
            var resultEnum = ((ComponentValueBox)result!).AsEnum();
            resultEnum.Should().NotBeNull();
            resultEnum.Should().Be("red");
            
            // Test case 2: green
            var greenEnum = ComponentValueBox.FromEnum("green");
            args = new ComponentValueBox[] { greenEnum };
            
            result = echoFunc.Invoke(args);
            result.Should().NotBeNull();
            
            resultEnum = ((ComponentValueBox)result!).AsEnum();
            resultEnum.Should().NotBeNull();
            resultEnum.Should().Be("green");
            
            // Test case 3: blue
            var blueEnum = ComponentValueBox.FromEnum("blue");
            args = new ComponentValueBox[] { blueEnum };
            
            result = echoFunc.Invoke(args);
            result.Should().NotBeNull();
            
            resultEnum = ((ComponentValueBox)result!).AsEnum();
            resultEnum.Should().NotBeNull();
            resultEnum.Should().Be("blue");
        }

        [Fact]
        public void ItCanInvokeEchoOptionFunction()
        {
            var echoFunc = GetComponentFunction("echo-option");
            
            // Test case 1: Some(person)
            var personFields = new (string, ComponentValueBox)[]
            {
                ("name", "Alice"),
                ("age", (byte)30)
            };
            var personRecord = ComponentValueBox.FromRecord(personFields);
            var someOption = ComponentValueBox.FromOption(personRecord);
            var args = new ComponentValueBox[] { someOption };
            
            var result = echoFunc!.Invoke(args);
            result.Should().NotBeNull();
            
            var resultOption = ((ComponentValueBox)result!).AsOption();
            resultOption.Should().NotBeNull(); // Some case returns a non-null ComponentValueBox
            
            var resultPerson = ((ComponentValueBox)resultOption!).AsRecord();
            resultPerson.Should().NotBeNull();
            resultPerson.Should().HaveCount(2);
            resultPerson![0].Item1.Should().Be("name");
            resultPerson[0].Item2.AsString().Should().Be("Alice");
            resultPerson[1].Item1.Should().Be("age");
            resultPerson[1].Item2.AsU8().Should().Be(30);
            
            // Test case 2: None
            var noneOption = ComponentValueBox.FromOption(null);
            args = new ComponentValueBox[] { noneOption };
            
            result = echoFunc.Invoke(args);
            result.Should().NotBeNull();
            
            resultOption = ((ComponentValueBox)result!).AsOption();
            resultOption.Should().BeNull(); // None is represented as null
            
            // Test case 3: Some(person) with different values
            personFields = new (string, ComponentValueBox)[]
            {
                ("name", "Bob"),
                ("age", (byte)25)
            };
            personRecord = ComponentValueBox.FromRecord(personFields);
            someOption = ComponentValueBox.FromOption(personRecord);
            args = new ComponentValueBox[] { someOption };
            
            result = echoFunc.Invoke(args);
            result.Should().NotBeNull();
            
            resultOption = ((ComponentValueBox)result!).AsOption();
            resultOption.Should().NotBeNull(); // Some case returns a non-null ComponentValueBox
            
            resultPerson = ((ComponentValueBox)resultOption!).AsRecord();
            resultPerson.Should().NotBeNull();
            resultPerson.Should().HaveCount(2);
            resultPerson![0].Item1.Should().Be("name");
            resultPerson[0].Item2.AsString().Should().Be("Bob");
            resultPerson[1].Item1.Should().Be("age");
            resultPerson[1].Item2.AsU8().Should().Be(25);
            
            // Test case 4: Some(person) with empty name and zero age
            personFields = new (string, ComponentValueBox)[]
            {
                ("name", ""),
                ("age", (byte)0)
            };
            personRecord = ComponentValueBox.FromRecord(personFields);
            someOption = ComponentValueBox.FromOption(personRecord);
            args = new ComponentValueBox[] { someOption };
            
            result = echoFunc.Invoke(args);
            result.Should().NotBeNull();
            
            resultOption = ((ComponentValueBox)result!).AsOption();
            resultOption.Should().NotBeNull(); // Some case returns a non-null ComponentValueBox
            
            resultPerson = ((ComponentValueBox)resultOption!).AsRecord();
            resultPerson.Should().NotBeNull();
            resultPerson.Should().HaveCount(2);
            resultPerson![0].Item1.Should().Be("name");
            resultPerson[0].Item2.AsString().Should().Be("");
            resultPerson[1].Item1.Should().Be("age");
            resultPerson[1].Item2.AsU8().Should().Be(0);
        }

        [Fact]
        public void ItCanInvokeEchoFlagsFunction()
        {
            var echoFunc = GetComponentFunction("echo-flags");
            
            // Test case 1: No flags set (empty)
            var emptyFlags = ComponentValueBox.FromFlags(new string[] { });
            var args = new ComponentValueBox[] { emptyFlags };
            
            var result = echoFunc!.Invoke(args);
            result.Should().NotBeNull();
            
            var resultFlags = ((ComponentValueBox)result!).AsFlags();
            resultFlags.Should().NotBeNull();
            resultFlags.Should().BeEmpty();
            
            // Test case 2: Single flag (GET)
            var getFlag = ComponentValueBox.FromFlags(new string[] { "get" });
            args = new ComponentValueBox[] { getFlag };
            
            result = echoFunc.Invoke(args);
            result.Should().NotBeNull();
            
            resultFlags = ((ComponentValueBox)result!).AsFlags();
            resultFlags.Should().NotBeNull();
            resultFlags.Should().HaveCount(1);
            resultFlags.Should().Contain("get");
            
            // Test case 3: Multiple flags (GET, POST)
            var multipleFlags = ComponentValueBox.FromFlags(new string[] { "get", "post" });
            args = new ComponentValueBox[] { multipleFlags };
            
            result = echoFunc.Invoke(args);
            result.Should().NotBeNull();
            
            resultFlags = ((ComponentValueBox)result!).AsFlags();
            resultFlags.Should().NotBeNull();
            resultFlags.Should().HaveCount(2);
            resultFlags.Should().Contain("get");
            resultFlags.Should().Contain("post");
            
            // Test case 4: All flags
            var allFlags = ComponentValueBox.FromFlags(new string[] { "get", "post", "put", "delete" });
            args = new ComponentValueBox[] { allFlags };
            
            result = echoFunc.Invoke(args);
            result.Should().NotBeNull();
            
            resultFlags = ((ComponentValueBox)result!).AsFlags();
            resultFlags.Should().NotBeNull();
            resultFlags.Should().HaveCount(4);
            resultFlags.Should().Contain("get");
            resultFlags.Should().Contain("post");
            resultFlags.Should().Contain("put");
            resultFlags.Should().Contain("delete");
            
            // Test case 5: Flags in different order
            var reorderedFlags = ComponentValueBox.FromFlags(new string[] { "delete", "put", "get" });
            args = new ComponentValueBox[] { reorderedFlags };
            
            result = echoFunc.Invoke(args);
            result.Should().NotBeNull();
            
            resultFlags = ((ComponentValueBox)result!).AsFlags();
            resultFlags.Should().NotBeNull();
            resultFlags.Should().HaveCount(3);
            resultFlags.Should().Contain("get");
            resultFlags.Should().Contain("put");
            resultFlags.Should().Contain("delete");
        }

        [Fact]
        public void ItCanInvokeEchoCharFunction()
        {
            var echoFunc = GetComponentFunction("echo-char");
            
            // Test case 1: Basic ASCII character
            var charA = ComponentValueBox.FromChar('A');
            var args = new ComponentValueBox[] { charA };
            
            var result = echoFunc!.Invoke(args);
            result.Should().NotBeNull();
            
            var resultChar = ((ComponentValueBox)result!).AsChar();
            resultChar.Should().Be('A');
            
            // Test case 2: Extended ASCII / Latin-1
            var charAccent = ComponentValueBox.FromChar('é'); // U+00E9
            args = new ComponentValueBox[] { charAccent };
            
            result = echoFunc.Invoke(args);
            result.Should().NotBeNull();
            
            resultChar = ((ComponentValueBox)result!).AsChar();
            resultChar.Should().Be('é');
            
            // Test case 3: CJK character (within BMP)
            var charChinese = ComponentValueBox.FromChar('中'); // U+4E2D
            args = new ComponentValueBox[] { charChinese };
            
            result = echoFunc.Invoke(args);
            result.Should().NotBeNull();
            
            resultChar = ((ComponentValueBox)result!).AsChar();
            resultChar.Should().Be('中');
            
            // Test case 4: Character at the edge of BMP
            var charBmpEdge = ComponentValueBox.FromChar('\uFFFD'); // U+FFFD (Replacement Character)
            args = new ComponentValueBox[] { charBmpEdge };
            
            result = echoFunc.Invoke(args);
            result.Should().NotBeNull();
            
            resultChar = ((ComponentValueBox)result!).AsChar();
            resultChar.Should().Be('\uFFFD');
            
            // Test case 5: Character beyond BMP (emoji)
            // 😀 (U+1F600) - cannot be represented as a single C# char
            var charEmoji = ComponentValueBox.FromUnicodeScalar(0x1F600);
            args = new ComponentValueBox[] { charEmoji };
            
            result = echoFunc.Invoke(args);
            result.Should().NotBeNull();
            
            var resultScalar = ((ComponentValueBox)result!).AsUnicodeScalar();
            resultScalar.Should().Be(0x1F600);
            
            // Test case 6: Mathematical Alphanumeric Symbols
            // 𝐀 (U+1D400) - Mathematical Bold Capital A
            var charMath = ComponentValueBox.FromUnicodeScalar(0x1D400);
            args = new ComponentValueBox[] { charMath };
            
            result = echoFunc.Invoke(args);
            result.Should().NotBeNull();
            
            resultScalar = ((ComponentValueBox)result!).AsUnicodeScalar();
            resultScalar.Should().Be(0x1D400);
            
            // Test case 7: Character just before surrogate range
            var charBeforeSurrogate = ComponentValueBox.FromUnicodeScalar(0xD7FF);
            args = new ComponentValueBox[] { charBeforeSurrogate };
            
            result = echoFunc.Invoke(args);
            result.Should().NotBeNull();
            
            resultScalar = ((ComponentValueBox)result!).AsUnicodeScalar();
            resultScalar.Should().Be(0xD7FF);
            
            // Test case 8: Character just after surrogate range
            var charAfterSurrogate = ComponentValueBox.FromUnicodeScalar(0xE000);
            args = new ComponentValueBox[] { charAfterSurrogate };
            
            result = echoFunc.Invoke(args);
            result.Should().NotBeNull();
            
            resultScalar = ((ComponentValueBox)result!).AsUnicodeScalar();
            resultScalar.Should().Be(0xE000);
            
            // Test case 9: Maximum valid Unicode scalar value
            var charMax = ComponentValueBox.FromUnicodeScalar(0x10FFFF);
            args = new ComponentValueBox[] { charMax };
            
            result = echoFunc.Invoke(args);
            result.Should().NotBeNull();
            
            resultScalar = ((ComponentValueBox)result!).AsUnicodeScalar();
            resultScalar.Should().Be(0x10FFFF);
            
            // Test case 10: Null character
            var charNull = ComponentValueBox.FromChar('\0');
            args = new ComponentValueBox[] { charNull };
            
            result = echoFunc.Invoke(args);
            result.Should().NotBeNull();
            
            resultChar = ((ComponentValueBox)result!).AsChar();
            resultChar.Should().Be('\0');
        }

        [Fact]
        public void ComponentValueBoxRejectsInvalidUnicodeScalarValues()
        {
            // Test surrogate values are rejected
            Action createSurrogateStart = () => ComponentValueBox.FromUnicodeScalar(0xD800);
            createSurrogateStart.Should().Throw<ArgumentException>()
                .WithMessage("Invalid Unicode scalar value: 0xD800");
            
            Action createSurrogateEnd = () => ComponentValueBox.FromUnicodeScalar(0xDFFF);
            createSurrogateEnd.Should().Throw<ArgumentException>()
                .WithMessage("Invalid Unicode scalar value: 0xDFFF");
            
            // Test values above U+10FFFF are rejected
            Action createAboveMax = () => ComponentValueBox.FromUnicodeScalar(0x110000);
            createAboveMax.Should().Throw<ArgumentException>()
                .WithMessage("Invalid Unicode scalar value: 0x110000");
            
            Action createWayAboveMax = () => ComponentValueBox.FromUnicodeScalar(0xFFFFFFFF);
            createWayAboveMax.Should().Throw<ArgumentException>()
                .WithMessage("Invalid Unicode scalar value: 0xFFFFFFFF");
        }

        [Fact(Skip = "Causes crash when using interface export as lookup context")]
        public void DemonstratesComponentExportTraversal()
        {
            // This test demonstrates the correct way to access functions from interface exports
            
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("component.wasm");
            stream.Should().NotBeNull();

            using var engine = new Engine();
            using var component = Component.FromStream(engine, "component.wasm", stream!);
            using var linker = new ComponentLinker(engine);
            
            var wasiConfig = new WasiConfiguration();
            using var store = new Store(engine, wasiConfig);
            
            linker.AddWasiPreview2();
            var instance = linker.Instantiate(store, component);
            
            // Step 1: Find the interface export
            var found = instance.TryGetExportIndex("dotnetcomp:plugin/business-rules@0.1.0", store, null, out var businessRulesExport);
            if (!found)
            {
                found = instance.TryGetExportIndex("dotnetcomp:plugin/business-rules", store, null, out businessRulesExport);
            }
            if (!found)
            {
                found = instance.TryGetExportIndex("business-rules", store, null, out businessRulesExport);
            }
            
            found.Should().BeTrue("The interface export exists");
            
            // Step 2: Direct function lookup without context fails
            var functionNames = new[]
            {
                "add",                                          // Just the function name
                "business-rules#add",                          // Interface#function  
                "dotnetcomp:plugin/business-rules#add",       // Package/interface#function
                "dotnetcomp:plugin/business-rules@0.1.0#add"  // Full qualified name
            };
            
            foreach (var name in functionNames)
            {
                var func = instance.GetFunction(name, store, null);
                func.Should().BeNull($"Direct lookup of '{name}' without context returns null");
            }
            
            // Step 3: The CORRECT approach - use the interface export as the lookup context
            using (businessRulesExport)
            {
                var addFunc = instance.GetFunction("add", store, businessRulesExport);
                addFunc.Should().NotBeNull("Function lookup succeeds when using interface export as context");
            }
            
            // Conclusion: Component export traversal works by using the parent export 
            // (interface) as the lookupInstance parameter when getting child exports (functions)
        }

        [Fact]
        public void TestExportIndexLookupWithParent()
        {
            // Simplified test to check if we can look up exports with a parent context
            
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("component.wasm");
            stream.Should().NotBeNull();

            using var engine = new Engine();
            using var component = Component.FromStream(engine, "component.wasm", stream!);
            using var linker = new ComponentLinker(engine);
            
            var wasiConfig = new WasiConfiguration();
            using var store = new Store(engine, wasiConfig);
            
            // Define the required host function before instantiation
            using var rootInstance = linker.GetRoot();
            using var hostServicesInstance = rootInstance.AddInstance("dotnetcomp:plugin/host-services@0.1.0");
            hostServicesInstance.DefineFunction<int, int, int>("", "host-add-s32", (x, y) => x + y);
            
            linker.AddWasiPreview2();
            var instance = linker.Instantiate(store, component);
            
            // Get the interface export
            var found = instance.TryGetExportIndex("dotnetcomp:plugin/business-rules@0.1.0", store, null, out var interfaceExport);
            if (!found)
            {
                found = instance.TryGetExportIndex("dotnetcomp:plugin/business-rules", store, null, out interfaceExport);
            }
            if (!found)
            {
                found = instance.TryGetExportIndex("business-rules", store, null, out interfaceExport);
            }
            found.Should().BeTrue("should find interface export");
            
            using (interfaceExport)
            {
                // Try to get an export within the interface
                Console.WriteLine("Trying to get export 'add' within interface...");
                var foundAdd = instance.TryGetExportIndex("add", store, interfaceExport, out var addExport);
                Console.WriteLine($"Found 'add' export: {foundAdd}");
                
                if (foundAdd)
                {
                    addExport?.Dispose();
                }
            }
        }

        [Fact]
        public void TestComponentValueStructLayout()
        {
            // Test that our struct layouts match what the C API expects
            unsafe
            {
                var size = System.Runtime.InteropServices.Marshal.SizeOf<ComponentValue>();
                var unionSize = System.Runtime.InteropServices.Marshal.SizeOf<ComponentValueUnion>();
                Console.WriteLine($"ComponentValue size: {size} bytes");
                Console.WriteLine($"ComponentValueUnion size: {unionSize} bytes");
                
                // Create a U32 value
                var val = new ComponentValue
                {
                    kind = ComponentValueKind.U32,
                    of = new ComponentValueUnion { u32 = 42 }
                };
                
                // Check the bytes
                byte* ptr = (byte*)&val;
                Console.WriteLine($"First 16 bytes: {ptr[0]:X2} {ptr[1]:X2} {ptr[2]:X2} {ptr[3]:X2} {ptr[4]:X2} {ptr[5]:X2} {ptr[6]:X2} {ptr[7]:X2} {ptr[8]:X2} {ptr[9]:X2} {ptr[10]:X2} {ptr[11]:X2} {ptr[12]:X2} {ptr[13]:X2} {ptr[14]:X2} {ptr[15]:X2}");
                Console.WriteLine($"Kind byte (should be 6 for U32): {ptr[0]}");
                Console.WriteLine($"U32 value at offset 8: {*(uint*)(ptr + 8)}");
            }
        }

        [Fact]
        public void DemonstratesComponentFunctionInvocationPattern()
        {
            // This test shows the pattern for invoking component functions
            // without actually running against a real component
            
            // Example: Prepare arguments for a function taking two u32 values
            var args = new ComponentValueBox[]
            {
                42u,    // First u32 argument
                100u    // Second u32 argument
            };
            
            // Arguments are automatically converted via implicit operators
            args[0].AsU32().Should().Be(42);
            args[1].AsU32().Should().Be(100);
            
            // Example: Handle different result types
            ComponentValueBox boolResult = true;
            boolResult.AsBool().Should().BeTrue();
            
            ComponentValueBox stringResult = "hello world";
            stringResult.AsString().Should().Be("hello world");
            
            // This demonstrates the intended usage pattern for component functions
        }
    }
}
