using System;
using System.Reflection;
using System.Text;
using FluentAssertions;
using Wasmtime;
using Xunit;

namespace Wasmtime.Tests
{
    /// <summary>
    /// Tests for WebAssembly Component Model resource support.
    /// Note: As of the current version, resource support in Wasmtime's C API is not yet fully implemented.
    /// These tests document the expected behavior once support is available.
    /// </summary>
    public class ComponentResourceTests
    {
        private ComponentFunction GetComponentFunction(string functionName)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("component.wasm");
            stream.Should().NotBeNull();

            var engine = new Engine();
            var component = Component.FromStream(engine, "component.wasm", stream!);
            var linker = new ComponentLinker(engine);
            
            var wasiConfig = new WasiConfiguration();
            var store = new Store(engine, wasiConfig);
            
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
            
            ComponentFunction func = null;
            
            using (businessRulesExport)
            {
                var foundExport = instance.TryGetExportIndex(functionName, store, businessRulesExport, out var funcExportIndex);
                foundExport.Should().BeTrue($"should find {functionName} function");
                
                using (funcExportIndex)
                {
                    func = instance.GetFunctionFromExportIndex(store, funcExportIndex);
                    func.Should().NotBeNull("should be able to get function from export index");
                }
            }
            
            // Need to keep these alive since function holds references
            _ = engine;
            _ = component; 
            _ = linker;
            _ = store;
            _ = instance;
            
            return func;
        }

        [Fact(Skip = "Resource support is not yet implemented in Wasmtime C API")]
        public void ItCanCreateAndDestroyResource()
        {
            // This test documents the expected behavior for resource support
            // Currently fails with: "not yet implemented" at crates/c-api/src/component/val.rs:309:45
            
            var openFunc = GetComponentFunction("open-resource");
            openFunc.Should().NotBeNull();
            
            var closeFunc = GetComponentFunction("close-resource");
            closeFunc.Should().NotBeNull();
            
            // Expected: Call open-resource to create a blob resource
            // Resources in the Component Model are opaque handles, typically represented as integers
            var resource = openFunc!.Invoke();
            resource.Should().NotBeNull("open-resource should return a resource handle");
            
            // The resource would be a handle (likely u32) wrapped in a ComponentValueBox
            var resourceBox = (ComponentValueBox)resource;
            var resourceHandle = resourceBox.AsU32();
            resourceHandle.Should().BeGreaterThan(0, "resource handle should be valid");
            
            // Call close-resource with the resource handle
            var closeResult = closeFunc!.Invoke((uint)resourceHandle);
            closeResult.Should().NotBeNull();
            
            // close-resource returns a bool indicating success
            var success = ((ComponentValueBox)closeResult!).AsBool();
            success.Should().BeTrue("close-resource should succeed");
        }

        [Fact(Skip = "Resource support is not yet implemented in Wasmtime C API")]
        public void ItCanUseResourceMethods()
        {
            // This test documents how resource methods would be accessed once support is available
            // In the Component Model, resource methods are exposed as regular functions
            // that take the resource handle as their first parameter
            
            var openFunc = GetComponentFunction("open-resource");
            openFunc.Should().NotBeNull();
            
            var closeFunc = GetComponentFunction("close-resource");
            closeFunc.Should().NotBeNull();
            
            // Create a blob resource
            var resource = openFunc!.Invoke();
            resource.Should().NotBeNull();
            
            var resourceBox = (ComponentValueBox)resource;
            var resourceHandle = resourceBox.AsU32();
            resourceHandle.Should().BeGreaterThan(0, "resource handle should be valid");
            
            // In a fully implemented system, we would look for functions like:
            // - [method]blob.write (taking resource handle and bytes)
            // - [method]blob.read (taking resource handle and count)
            // - [static]blob.merge (taking two resource handles)
            // These would be exposed as regular component functions
            
            // Clean up the resource
            var closeResult = closeFunc!.Invoke((uint)resourceHandle);
            var success = ((ComponentValueBox)closeResult!).AsBool();
            success.Should().BeTrue();
        }

        [Fact]
        public void ItCanFindResourceFunctions()
        {
            // This test verifies that the resource-related functions exist in the component
            // even though we can't call them yet due to C API limitations
            
            var openFunc = GetComponentFunction("open-resource");
            openFunc.Should().NotBeNull("open-resource function should exist");
            
            var closeFunc = GetComponentFunction("close-resource");
            closeFunc.Should().NotBeNull("close-resource function should exist");
            
            // The functions exist and can be retrieved, but invoking them
            // currently fails due to unimplemented resource support in the C API
        }
    }
}