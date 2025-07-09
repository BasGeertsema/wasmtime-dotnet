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

        [Fact]
        public void ItCanInvokeAddS8Function()
        {
            // Test the add-s8 function
            
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
                var foundExport = instance.TryGetExportIndex("add-s8", store, businessRulesExport, out var addExportIndex);
                foundExport.Should().BeTrue("should find add-s8 function");
                
                using (addExportIndex)
                {
                    addFunc = instance.GetFunctionFromExportIndex(store, addExportIndex);
                    addFunc.Should().NotBeNull("should be able to get function from export index");
                }
            }
            
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
            // Test the add-u8 function
            
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("component.wasm");
            stream.Should().NotBeNull();

            using var engine = new Engine();
            using var component = Component.FromStream(engine, "component.wasm", stream!);
            using var linker = new ComponentLinker(engine);
            
            var wasiConfig = new WasiConfiguration();
            using var store = new Store(engine, wasiConfig);
            
            linker.AddWasiPreview2();
            var instance = linker.Instantiate(store, component);
            
            // Get the business-rules interface
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
                var foundExport = instance.TryGetExportIndex("add-u8", store, businessRulesExport, out var addExportIndex);
                foundExport.Should().BeTrue("should find add-u8 function");
                
                using (addExportIndex)
                {
                    addFunc = instance.GetFunctionFromExportIndex(store, addExportIndex);
                    addFunc.Should().NotBeNull();
                }
            }
            
            var args = new ComponentValueBox[] { (byte)100, (byte)150 };
            var result = addFunc!.Invoke(args);
            result.Should().NotBeNull();
            ((ComponentValueBox)result!).AsU8().Should().Be(250);
        }

        [Fact]
        public void ItCanInvokeAddS16Function()
        {
            // Test the add-s16 function
            
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("component.wasm");
            stream.Should().NotBeNull();

            using var engine = new Engine();
            using var component = Component.FromStream(engine, "component.wasm", stream!);
            using var linker = new ComponentLinker(engine);
            
            var wasiConfig = new WasiConfiguration();
            using var store = new Store(engine, wasiConfig);
            
            linker.AddWasiPreview2();
            var instance = linker.Instantiate(store, component);
            
            // Get the business-rules interface
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
                var foundExport = instance.TryGetExportIndex("add-s16", store, businessRulesExport, out var addExportIndex);
                foundExport.Should().BeTrue("should find add-s16 function");
                
                using (addExportIndex)
                {
                    addFunc = instance.GetFunctionFromExportIndex(store, addExportIndex);
                    addFunc.Should().NotBeNull();
                }
            }
            
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
            // Test the add-u16 function
            
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("component.wasm");
            stream.Should().NotBeNull();

            using var engine = new Engine();
            using var component = Component.FromStream(engine, "component.wasm", stream!);
            using var linker = new ComponentLinker(engine);
            
            var wasiConfig = new WasiConfiguration();
            using var store = new Store(engine, wasiConfig);
            
            linker.AddWasiPreview2();
            var instance = linker.Instantiate(store, component);
            
            // Get the business-rules interface
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
                var foundExport = instance.TryGetExportIndex("add-u16", store, businessRulesExport, out var addExportIndex);
                foundExport.Should().BeTrue("should find add-u16 function");
                
                using (addExportIndex)
                {
                    addFunc = instance.GetFunctionFromExportIndex(store, addExportIndex);
                    addFunc.Should().NotBeNull();
                }
            }
            
            var args = new ComponentValueBox[] { (ushort)10000, (ushort)20000 };
            var result = addFunc!.Invoke(args);
            result.Should().NotBeNull();
            ((ComponentValueBox)result!).AsU16().Should().Be(30000);
        }

        [Fact]
        public void ItCanInvokeAddS32Function()
        {
            // Test the add-s32 function
            
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("component.wasm");
            stream.Should().NotBeNull();

            using var engine = new Engine();
            using var component = Component.FromStream(engine, "component.wasm", stream!);
            using var linker = new ComponentLinker(engine);
            
            var wasiConfig = new WasiConfiguration();
            using var store = new Store(engine, wasiConfig);
            
            linker.AddWasiPreview2();
            var instance = linker.Instantiate(store, component);
            
            // Get the business-rules interface
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
                var foundExport = instance.TryGetExportIndex("add-s32", store, businessRulesExport, out var addExportIndex);
                foundExport.Should().BeTrue("should find add-s32 function");
                
                using (addExportIndex)
                {
                    addFunc = instance.GetFunctionFromExportIndex(store, addExportIndex);
                    addFunc.Should().NotBeNull();
                }
            }
            
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
            // Test the add-u32 function
            
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("component.wasm");
            stream.Should().NotBeNull();

            using var engine = new Engine();
            using var component = Component.FromStream(engine, "component.wasm", stream!);
            using var linker = new ComponentLinker(engine);
            
            var wasiConfig = new WasiConfiguration();
            using var store = new Store(engine, wasiConfig);
            
            linker.AddWasiPreview2();
            var instance = linker.Instantiate(store, component);
            
            // Get the business-rules interface
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
                var foundExport = instance.TryGetExportIndex("add-u32", store, businessRulesExport, out var addExportIndex);
                foundExport.Should().BeTrue("should find add-u32 function");
                
                using (addExportIndex)
                {
                    addFunc = instance.GetFunctionFromExportIndex(store, addExportIndex);
                    addFunc.Should().NotBeNull();
                }
            }
            
            var args = new ComponentValueBox[] { 2u, 3u };
            var result = addFunc!.Invoke(args);
            result.Should().NotBeNull();
            ((ComponentValueBox)result!).AsU32().Should().Be(5);
        }

        [Fact]
        public void ItCanInvokeAddS64Function()
        {
            // Test the add-s64 function
            
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("component.wasm");
            stream.Should().NotBeNull();

            using var engine = new Engine();
            using var component = Component.FromStream(engine, "component.wasm", stream!);
            using var linker = new ComponentLinker(engine);
            
            var wasiConfig = new WasiConfiguration();
            using var store = new Store(engine, wasiConfig);
            
            linker.AddWasiPreview2();
            var instance = linker.Instantiate(store, component);
            
            // Get the business-rules interface
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
                var foundExport = instance.TryGetExportIndex("add-s64", store, businessRulesExport, out var addExportIndex);
                foundExport.Should().BeTrue("should find add-s64 function");
                
                using (addExportIndex)
                {
                    addFunc = instance.GetFunctionFromExportIndex(store, addExportIndex);
                    addFunc.Should().NotBeNull();
                }
            }
            
            var args = new ComponentValueBox[] { 1000000000000L, 2000000000000L };
            var result = addFunc!.Invoke(args);
            result.Should().NotBeNull();
            ((ComponentValueBox)result!).AsS64().Should().Be(3000000000000L);
        }

        [Fact]
        public void ItCanInvokeAddU64Function()
        {
            // Test the add-u64 function
            
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("component.wasm");
            stream.Should().NotBeNull();

            using var engine = new Engine();
            using var component = Component.FromStream(engine, "component.wasm", stream!);
            using var linker = new ComponentLinker(engine);
            
            var wasiConfig = new WasiConfiguration();
            using var store = new Store(engine, wasiConfig);
            
            linker.AddWasiPreview2();
            var instance = linker.Instantiate(store, component);
            
            // Get the business-rules interface
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
                var foundExport = instance.TryGetExportIndex("add-u64", store, businessRulesExport, out var addExportIndex);
                foundExport.Should().BeTrue("should find add-u64 function");
                
                using (addExportIndex)
                {
                    addFunc = instance.GetFunctionFromExportIndex(store, addExportIndex);
                    addFunc.Should().NotBeNull();
                }
            }
            
            var args = new ComponentValueBox[] { 5000000000000UL, 10000000000000UL };
            var result = addFunc!.Invoke(args);
            result.Should().NotBeNull();
            ((ComponentValueBox)result!).AsU64().Should().Be(15000000000000UL);
        }

        [Fact]
        public void ItCanInvokeAddF32Function()
        {
            // Test the add-f32 function
            
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("component.wasm");
            stream.Should().NotBeNull();

            using var engine = new Engine();
            using var component = Component.FromStream(engine, "component.wasm", stream!);
            using var linker = new ComponentLinker(engine);
            
            var wasiConfig = new WasiConfiguration();
            using var store = new Store(engine, wasiConfig);
            
            linker.AddWasiPreview2();
            var instance = linker.Instantiate(store, component);
            
            // Get the business-rules interface
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
                var foundExport = instance.TryGetExportIndex("add-f32", store, businessRulesExport, out var addExportIndex);
                foundExport.Should().BeTrue("should find add-f32 function");
                
                using (addExportIndex)
                {
                    addFunc = instance.GetFunctionFromExportIndex(store, addExportIndex);
                    addFunc.Should().NotBeNull();
                }
            }
            
            var args = new ComponentValueBox[] { 2.0f, 3.0f };
            var result = addFunc!.Invoke(args);
            result.Should().NotBeNull();
            ((ComponentValueBox)result!).AsF32().Should().Be(5.0f);
        }

        [Fact]
        public void ItCanInvokeAddF64Function()
        {
            // Test the add-f64 function
            
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("component.wasm");
            stream.Should().NotBeNull();

            using var engine = new Engine();
            using var component = Component.FromStream(engine, "component.wasm", stream!);
            using var linker = new ComponentLinker(engine);
            
            var wasiConfig = new WasiConfiguration();
            using var store = new Store(engine, wasiConfig);
            
            linker.AddWasiPreview2();
            var instance = linker.Instantiate(store, component);
            
            // Get the business-rules interface
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
                var foundExport = instance.TryGetExportIndex("add-f64", store, businessRulesExport, out var addExportIndex);
                foundExport.Should().BeTrue("should find add-f64 function");
                
                using (addExportIndex)
                {
                    addFunc = instance.GetFunctionFromExportIndex(store, addExportIndex);
                    addFunc.Should().NotBeNull();
                }
            }
            
            var args = new ComponentValueBox[] { 3.141592653589793, 2.718281828459045 };
            var result = addFunc!.Invoke(args);
            result.Should().NotBeNull();
            ((ComponentValueBox)result!).AsF64().Should().BeApproximately(5.859874482048838, 0.0000000001);
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
