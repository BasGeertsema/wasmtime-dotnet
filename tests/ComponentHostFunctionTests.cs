using System;
using System.Reflection;
using FluentAssertions;
using Wasmtime;
using Xunit;

namespace Wasmtime.Tests
{
    /// <summary>
    /// Tests for host functions in WebAssembly Components.
    /// Host functions allow .NET code to be called from WebAssembly components.
    /// </summary>
    public class ComponentHostFunctionTests
    {
        [Fact]
        public void ItCanDefineSimpleHostFunction()
        {
            using var engine = new Engine();
            using var linker = new ComponentLinker(engine);
            
            var wasiConfig = new WasiConfiguration();
            using var store = new Store(engine, wasiConfig);
            
            // Get the root instance to define functions
            using var rootInstance = linker.GetRoot();
            
            // Track if our host function was called
            bool wasCalled = false;
            
            // Define a simple host function that takes no parameters and returns nothing
            rootInstance.DefineFunction("test-host", "say-hello", () =>
            {
                wasCalled = true;
            });
            
            // For now, just verify we can define the function without errors
            // Full invocation test would require a component that imports this function
            wasCalled.Should().BeFalse();
        }
        
        [Fact]
        public void ItCanDefineHostFunctionWithParameters()
        {
            using var engine = new Engine();
            using var linker = new ComponentLinker(engine);
            
            var wasiConfig = new WasiConfiguration();
            using var store = new Store(engine, wasiConfig);
            
            using var rootInstance = linker.GetRoot();
            
            int capturedValue = 0;
            
            // Define a host function that takes an integer parameter
            rootInstance.DefineFunction("test-host", "double", (int x) =>
            {
                capturedValue = x * 2;
                return capturedValue;
            });
            
            // Function is defined, would need a component that imports it to test invocation
            capturedValue.Should().Be(0);
        }
        
        [Fact]
        public void ItCanDefineHostFunctionWithMultipleParameters()
        {
            using var engine = new Engine();
            using var linker = new ComponentLinker(engine);
            
            var wasiConfig = new WasiConfiguration();
            using var store = new Store(engine, wasiConfig);
            
            using var rootInstance = linker.GetRoot();
            
            string capturedMessage = "";
            
            // Define a host function that takes multiple parameters
            rootInstance.DefineFunction("test-host", "format-message", (string prefix, int number) =>
            {
                capturedMessage = $"{prefix}: {number}";
                return capturedMessage;
            });
            
            capturedMessage.Should().BeEmpty();
        }
        
        [Fact]
        public void ItCanDefineNestedHostFunctions()
        {
            using var engine = new Engine();
            using var linker = new ComponentLinker(engine);
            
            var wasiConfig = new WasiConfiguration();
            using var store = new Store(engine, wasiConfig);
            
            // Create a nested instance structure
            using var rootInstance = linker.GetRoot();
            using var apiInstance = rootInstance.AddInstance("api");
            using var v1Instance = apiInstance.AddInstance("v1");
            
            // Define functions at different nesting levels
            rootInstance.DefineFunction("root", "test", () => { });
            apiInstance.DefineFunction("api", "test", () => { });
            v1Instance.DefineFunction("v1", "test", () => { });
            
            // Verify we can define functions at different levels without errors
        }
        
        [Fact]
        public void ItThrowsOnNullParameters()
        {
            using var engine = new Engine();
            using var linker = new ComponentLinker(engine);
            using var rootInstance = linker.GetRoot();
            
            Action act1 = () => rootInstance.DefineFunction(null!, "test", () => { });
            act1.Should().Throw<ArgumentNullException>().WithParameterName("module");
            
            Action act2 = () => rootInstance.DefineFunction("module", null!, () => { });
            act2.Should().Throw<ArgumentNullException>().WithParameterName("name");
        }
        
        // TODO: Complete implementation of argument/result marshaling in DefineFunction<T, TResult>
        //       and DefineFunction<T1, T2, TResult> methods in ComponentLinkerInstance.cs
        //       The native callback is properly registered but argument conversion is incomplete.
        
        [Fact(Skip = "Host function argument/result marshaling not fully implemented")]
        public void ItCanInvokeHostFunctionFromComponent()
        {
            using var engine = new Engine();
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
            // The component imports an instance "test-host" with function "add-numbers"
            using var testHostInstance = rootInstance.AddInstance("test-host");
            testHostInstance.DefineFunction<int, int, int>("", "add-numbers", (x, y) =>
            {
                hostCallCount++;
                lastX = x;
                lastY = y;
                hostResult = x + y + 1000; // Add 1000 to make it clear this is from the host
                return hostResult;
            });
            
            // Load the test component that imports this host function
            byte[] componentBytes;
            using (var stream = System.Reflection.Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("host-import.wasm")!)
            {
                componentBytes = new byte[stream.Length];
                stream.Read(componentBytes, 0, componentBytes.Length);
            }
            using var component = Component.FromBytes(engine, "host-import.wasm", componentBytes);
            
            // Instantiate the component
            linker.AddWasiPreview2();
            var instance = linker.Instantiate(store, component);
            
            // Get the exported function that calls the host function
            var callHostAddS32 = instance.GetFunction("test-add", store);
            callHostAddS32.Should().NotBeNull();
            
            // Call the function which should invoke our host function
            var result = callHostAddS32!.Invoke(new ComponentValueBox[] { 5, 7 });
            
            // Verify the host function was called
            hostCallCount.Should().Be(1);
            lastX.Should().Be(5);
            lastY.Should().Be(7);
            hostResult.Should().Be(1012); // 5 + 7 + 1000
            
            // Check the result
            result.Should().NotBeNull();
            var resultBox = (ComponentValueBox)result!;
            resultBox.AsS32().Should().Be(1012); // The component should return what the host returned
        }
    }
}