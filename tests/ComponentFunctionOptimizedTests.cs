using System;
using System.Reflection;
using FluentAssertions;
using Wasmtime;
using Xunit;
using Xunit.Abstractions;

namespace Wasmtime.Tests
{
    public class ComponentFunctionOptimizedTests
    {
        private readonly ITestOutputHelper testOutputHelper;

        public ComponentFunctionOptimizedTests(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        private class ComponentContext : IDisposable
        {
            public Engine Engine { get; }
            public Store Store { get; }
            public ComponentLinker Linker { get; }
            public ComponentInstance Instance { get; }
            public Component Component { get; }
            public ComponentFunction Function { get; }
            
            public ComponentContext(string functionName)
            {
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("component.wasm");
                stream.Should().NotBeNull();

                Engine = new Engine();
                Component = Component.FromStream(Engine, "component.wasm", stream!);
                Linker = new ComponentLinker(Engine);
                
                var wasiConfig = new WasiConfiguration();
                Store = new Store(Engine, wasiConfig);
                
                Linker.AddWasiPreview2();
                Instance = Linker.Instantiate(Store, Component);
                Instance.Should().NotBeNull();
                
                // Find the business-rules interface export
                var found = Instance.TryGetExportIndex("dotnetcomp:plugin/business-rules@0.1.0", Store, null, out var businessRulesExport);
                
                if (!found)
                {
                    found = Instance.TryGetExportIndex("dotnetcomp:plugin/business-rules", Store, null, out businessRulesExport);
                }
                
                if (!found)
                {
                    found = Instance.TryGetExportIndex("business-rules", Store, null, out businessRulesExport);
                }
                
                found.Should().BeTrue("should find the business-rules interface export");
                
                ComponentFunction addFunc = null;
                
                using (businessRulesExport)
                {
                    var foundExport = Instance.TryGetExportIndex(functionName, Store, businessRulesExport, out var addExportIndex);
                    foundExport.Should().BeTrue($"should find {functionName} function");
                    
                    using (addExportIndex)
                    {
                        addFunc = Instance.GetFunctionFromExportIndex(Store, addExportIndex);
                        addFunc.Should().NotBeNull("should be able to get function from export index");
                    }
                }
                
                Function = addFunc!;
            }
            
            public void Dispose()
            {
                // ComponentInstance doesn't need explicit disposal
                Linker?.Dispose();
                Store?.Dispose();
                Component?.Dispose();
                Engine?.Dispose();
            }
        }

        [Fact]
        public void ItCanWrapAddS8FunctionAsFunc()
        {
            using var context = new ComponentContext("add-s8");
            var addFunc = context.Function;
            
            // Wrap the function with optimized invocation
            var wrappedFunc = addFunc.WrapFunc<sbyte, sbyte, sbyte>();
            wrappedFunc.Should().NotBeNull("should be able to wrap add-s8 function");
            
            // Test with positive values only
            var result = wrappedFunc!(10, 20);
            result.Should().Be(30);
            
            // Test with negative values
            result = wrappedFunc(-50, 30);
            result.Should().Be(-20);
            
            // Test multiple calls to ensure caching works
            for (int i = 0; i < 50; i++)
            {
                result = wrappedFunc((sbyte)i, (sbyte)(i + 1));
                result.Should().Be((sbyte)(i + i + 1));
            }
        }

        [Fact]
        public void ItCanWrapAddU8FunctionAsFunc()
        {
            using var context = new ComponentContext("add-u8");
            var addFunc = context.Function;
            
            var wrappedFunc = addFunc.WrapFunc<byte, byte, byte>();
            wrappedFunc.Should().NotBeNull();
            
            var result = wrappedFunc!(100, 150);
            result.Should().Be(250);
            
            // Test edge cases
            result = wrappedFunc(0, 0);
            result.Should().Be(0);
            
            result = wrappedFunc(255, 0);
            result.Should().Be(255);
        }

        [Fact]
        public void ItCanWrapAddS16FunctionAsFunc()
        {
            using var context = new ComponentContext("add-s16");
            var addFunc = context.Function;
            
            var wrappedFunc = addFunc.WrapFunc<short, short, short>();
            wrappedFunc.Should().NotBeNull();
            
            var result = wrappedFunc!(1000, 2000);
            result.Should().Be(3000);
            
            result = wrappedFunc(-5000, 3000);
            result.Should().Be(-2000);
        }

        [Fact]
        public void ItCanWrapAddU16FunctionAsFunc()
        {
            using var context = new ComponentContext("add-u16");
            var addFunc = context.Function;
            
            var wrappedFunc = addFunc.WrapFunc<ushort, ushort, ushort>();
            wrappedFunc.Should().NotBeNull();
            
            var result = wrappedFunc!(10000, 20000);
            result.Should().Be(30000);
        }

        [Fact]
        public void ItCanWrapAddS32FunctionAsFunc()
        {
            using var context = new ComponentContext("add-s32");
            var addFunc = context.Function;
            
            var wrappedFunc = addFunc.WrapFunc<int, int, int>();
            wrappedFunc.Should().NotBeNull();
            
            var result = wrappedFunc!(100000, 200000);
            result.Should().Be(300000);
            
            result = wrappedFunc(-1000000, 500000);
            result.Should().Be(-500000);
        }

        [Fact]
        public void ItCanWrapAddU32FunctionAsFunc()
        {
            using var context = new ComponentContext("add-u32");
            var addFunc = context.Function;
            
            var wrappedFunc = addFunc.WrapFunc<uint, uint, uint>();
            wrappedFunc.Should().NotBeNull();
            
            var result = wrappedFunc!(1000000, 2000000);
            result.Should().Be(3000000);
        }

        [Fact]
        public void ItCanWrapAddS64FunctionAsFunc()
        {
            using var context = new ComponentContext("add-s64");
            var addFunc = context.Function;
            
            var wrappedFunc = addFunc.WrapFunc<long, long, long>();
            wrappedFunc.Should().NotBeNull();
            
            var result = wrappedFunc!(1000000000L, 2000000000L);
            result.Should().Be(3000000000L);
            
            result = wrappedFunc(-5000000000L, 3000000000L);
            result.Should().Be(-2000000000L);
        }

        [Fact]
        public void ItCanWrapAddU64FunctionAsFunc()
        {
            using var context = new ComponentContext("add-u64");
            var addFunc = context.Function;
            
            var wrappedFunc = addFunc.WrapFunc<ulong, ulong, ulong>();
            wrappedFunc.Should().NotBeNull();
            
            var result = wrappedFunc!(10000000000UL, 20000000000UL);
            result.Should().Be(30000000000UL);
        }

        [Fact]
        public void ItCanWrapAddF32FunctionAsFunc()
        {
            using var context = new ComponentContext("add-f32");
            var addFunc = context.Function;
            
            var wrappedFunc = addFunc.WrapFunc<float, float, float>();
            wrappedFunc.Should().NotBeNull();
            
            var result = wrappedFunc!(1.5f, 2.5f);
            result.Should().Be(4.0f);
            
            result = wrappedFunc(-10.25f, 5.25f);
            result.Should().Be(-5.0f);
        }

        [Fact]
        public void ItCanWrapAddF64FunctionAsFunc()
        {
            using var context = new ComponentContext("add-f64");
            var addFunc = context.Function;
            
            var wrappedFunc = addFunc.WrapFunc<double, double, double>();
            wrappedFunc.Should().NotBeNull();
            
            var result = wrappedFunc!(1.5, 2.5);
            result.Should().Be(4.0);
            
            result = wrappedFunc(-10.25, 5.25);
            result.Should().Be(-5.0);
        }

        [Fact]
        public void WrapperCacheIsReused()
        {
            using var context = new ComponentContext("add-s32");
            var addFunc = context.Function;
            
            // First wrap
            var wrappedFunc1 = addFunc.WrapFunc<int, int, int>();
            wrappedFunc1.Should().NotBeNull();
            
            // Second wrap should return the same cached instance
            var wrappedFunc2 = addFunc.WrapFunc<int, int, int>();
            wrappedFunc2.Should().NotBeNull();
            
            // They should be the same reference
            wrappedFunc1.Should().BeSameAs(wrappedFunc2, "wrapper cache should be reused");
        }

        [Fact]
        public void PerformanceComparisonTest()
        {
            using var context = new ComponentContext("add-s32");
            var addFunc = context.Function;
            
            // Wrapped function
            var wrappedFunc = addFunc.WrapFunc<int, int, int>();
            wrappedFunc.Should().NotBeNull();
            
            const int iterations = 1000;
            
            // Measure wrapped function performance
            var wrappedStart = DateTime.UtcNow;
            for (int i = 0; i < iterations; i++)
            {
                var result = wrappedFunc!(i, i + 1);
            }
            var wrappedTime = DateTime.UtcNow - wrappedStart;
            
            // Measure regular invoke performance
            var invokeStart = DateTime.UtcNow;
            for (int i = 0; i < iterations; i++)
            {
                var result = addFunc.Invoke(new ComponentValueBox[] { i, i + 1 });
            }
            var invokeTime = DateTime.UtcNow - invokeStart;
            
            // Wrapped should be significantly faster
            // Note: In a real test, we'd use proper benchmarking tools
            testOutputHelper.WriteLine($"Wrapped time: {wrappedTime.TotalMilliseconds}ms");
            testOutputHelper.WriteLine($"Invoke time: {invokeTime.TotalMilliseconds}ms");
            testOutputHelper.WriteLine($"Speedup: {invokeTime.TotalMilliseconds / wrappedTime.TotalMilliseconds:F2}x");
            
            // The wrapped version should be faster (though this might be flaky in CI)
            wrappedTime.Should().BeLessThan(invokeTime);
        }
    }
}