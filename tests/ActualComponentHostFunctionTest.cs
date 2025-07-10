using System;
using System.IO;
using System.Reflection;
using FluentAssertions;
using Wasmtime;
using Xunit;

namespace Wasmtime.Tests
{
    /// <summary>
    /// Test that uses the actual dotnetcomp component with host functions
    /// </summary>
    public class ActualComponentHostFunctionTest
    {
        [Fact]
        public void ItCanCallHostAddS32WithActualComponent()
        {
            // Load the component from embedded resource
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("component.wasm");
            stream.Should().NotBeNull("The component.wasm embedded resource should exist");

            using var engine = new Engine();
            using var component = Component.FromStream(engine, "component.wasm", stream!);
            using var linker = new ComponentLinker(engine);
            
            var wasiConfig = new WasiConfiguration();
            using var store = new Store(engine, wasiConfig);
            
            // Get the root instance to define functions
            using var rootInstance = linker.GetRoot();
            
            // Track host function invocation
            int hostCallCount = 0;
            int lastX = 0;
            int lastY = 0;
            int hostResult = 0;
            
            // Define the host function that the component imports
            // According to plugin.wit, it's in the "dotnetcomp:plugin/host-services@0.1.0" interface
            using var hostServicesInstance = rootInstance.AddInstance("dotnetcomp:plugin/host-services@0.1.0");
            hostServicesInstance.DefineFunction<int, int, int>("", "host-add-s32", (x, y) =>
            {
                hostCallCount++;
                lastX = x;
                lastY = y;
                hostResult = x + y;
                return hostResult;
            });
            
            // Instantiate the component
            linker.AddWasiPreview2();
            var instance = linker.Instantiate(store, component);
            
            // Try to find the function - it might be nested in an interface
            if (!instance.TryGetExportIndex("dotnetcomp:plugin/business-rules@0.1.0", store, null, out var businessRulesExport))
            {
                // Component loaded but we can't find the export
                Assert.Fail("Could not find the business-rules interface in the component.");
            }
            
            using (businessRulesExport)
            {
                // Try to get the call-host-add-s32 function from the business-rules interface
                if (!instance.TryGetExportIndex("call-host-add-s32", store, businessRulesExport, out var functionExport))
                {
                    Assert.Fail("Could not find the call-host-add-s32 function");
                }
                
                using (functionExport)
                {
                    var callHostAddS32 = instance.GetFunctionFromExportIndex(store, functionExport);
                    
                    callHostAddS32.Should().NotBeNull("The call-host-add-s32 function should exist");
                    
                    // Call the function which should invoke our host function
                    var result = callHostAddS32!.Invoke(new ComponentValueBox[] { 42, 58 });
                    
                    // Verify the host function was called
                    hostCallCount.Should().Be(1, "The host function should have been called once");
                    lastX.Should().Be(42, "The first parameter should be 42");
                    lastY.Should().Be(58, "The second parameter should be 58");
                    hostResult.Should().Be(100, "The host function should return 42 + 58 = 100");
                    
                    // Check the result
                    result.Should().NotBeNull("The function should return a result");
                    result.Should().BeOfType<ComponentValueBox>("The result should be a ComponentValueBox");
                    var resultBox = (ComponentValueBox)result!;
                    resultBox.AsS32().Should().Be(100, "The component should return the same value as the host function");
                }
            }
        }
    }
}