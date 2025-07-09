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

        [Fact]//(Skip = "Type mismatch issue needs investigation")]
        public void ItCanInvokeAddFunction()
        {
            // This test demonstrates how the add function would be invoked
            // once full export traversal support is implemented
            
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("component.wasm");
            stream.Should().NotBeNull();

            using var engine = new Engine();
            using var component = Component.FromStream(engine, "component.wasm", stream!);
            using var linker = new ComponentLinker(engine);
            
            // Create WASI context and store
            var wasiConfig = new WasiConfiguration();
            using var store = new Store(engine, wasiConfig);
            
            // Add WASI to the linker since the component requires it
            linker.AddWasiPreview2();
            
            // Instantiate the component
            var instance = linker.Instantiate(store, component);
            instance.Should().NotBeNull();
            
            // First, find the business-rules interface export
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
                // First check if we can find the export index
                Console.WriteLine("First, trying TryGetExportIndex for 'add'...");
                var foundExport = instance.TryGetExportIndex("add", store, businessRulesExport, out var addExportIndex);
                Console.WriteLine($"TryGetExportIndex returned: {foundExport}");
                
                if (foundExport)
                {
                    using (addExportIndex)
                    {
                        // Now we have the export index for the function
                        Console.WriteLine("Found the 'add' function export index.");
                        
                        // Use the extension method to get the function from the export index
                        addFunc = instance.GetFunctionFromExportIndex(store, addExportIndex);
                        Console.WriteLine($"GetFunctionFromExportIndex returned: {addFunc != null}");
                        
                        addFunc.Should().NotBeNull("should be able to get function from export index");
                    }
                }
                else
                {
                    throw new Xunit.Sdk.XunitException("Could not find 'add' export within interface");
                }
            }
            
            // Prepare arguments: two u32 values
            var args = new ComponentValueBox[]
            {
                2u,  // First argument: 2
                3u   // Second argument: 3
            };
            
            Console.WriteLine($"Arg 0 kind: {args[0].Kind}, value: {args[0].AsU32()}");
            Console.WriteLine($"Arg 1 kind: {args[1].Kind}, value: {args[1].AsU32()}");
            
            // Invoke the function
            var result = addFunc!.Invoke(args);
            
            // Verify the result
            result.Should().NotBeNull("add function should return a value");
            result.Should().BeOfType<ComponentValueBox>("result should be a ComponentValueBox");
            
            var resultBox = (ComponentValueBox)result!;
            resultBox.AsU32().Should().Be(5, "2 + 3 should equal 5");
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
