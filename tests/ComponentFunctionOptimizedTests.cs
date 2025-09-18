using System;
using System.Diagnostics;
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
                
                // Define the required host function before instantiation
                using var rootInstance = Linker.GetRoot();
                using var hostServicesInstance = rootInstance.AddInstance("dotnetcomp:plugin/host-services@0.1.0");
                hostServicesInstance.DefineFunction<int, int, int>("", "host-add-s32", (x, y) => x + y);
                
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
        public void ItCanWrapReverseListS32FunctionAsFuncWithEmptyList()
        {
            using var context = new ComponentContext("reverse-list-s32");
            var reverseFunc = context.Function;
            
            // Wrap the function with optimized invocation
            var wrappedFunc = reverseFunc.WrapFunc<int[], int[]>();
            wrappedFunc.Should().NotBeNull("should be able to wrap reverse-list-s32 function");
            
            // Test with empty list
            // TODO: This crashes with a segfault. The issue appears to be related to how
            // empty lists are handled in the optimized invocation path. The regular
            // invocation path works fine with empty lists.
            var result = wrappedFunc(Array.Empty<int>());
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public void ItCanWrapReverseListS32FunctionAsFunc()
        {
            using var context = new ComponentContext("reverse-list-s32");
            var reverseFunc = context.Function;
            
            // Wrap the function with optimized invocation
            var wrappedFunc = reverseFunc.WrapFunc<int[], int[]>();
            wrappedFunc.Should().NotBeNull("should be able to wrap reverse-list-s32 function");
            
            // Test with non-empty list
            var input = new int[] { 1, 2, 3, 4, 5 };
            var result = wrappedFunc!(input);
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(new int[] { 5, 4, 3, 2, 1 }, options => options.WithStrictOrdering());
            
            // Test with empty list
            // TODO: Fix empty array handling in optimized path
            // result = wrappedFunc(Array.Empty<int>());
            // result.Should().NotBeNull();
            // result.Should().BeEmpty();
            
            // Test with single element
            result = wrappedFunc(new int[] { 42 });
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(new int[] { 42 });
            
            // Test multiple calls to ensure caching works
            for (int i = 0; i < 10; i++)
            {
                var testInput = new int[] { i, i + 1, i + 2 };
                result = wrappedFunc(testInput);
                result.Should().BeEquivalentTo(new int[] { i + 2, i + 1, i }, options => options.WithStrictOrdering());
            }
        }

        [Fact]
        public void ItCanWrapEchoOptionS32FunctionAsFunc()
        {
            using var context = new ComponentContext("echo-option-s32");
            var echoFunc = context.Function;
            
            // Wrap the function with optimized invocation for Option<int> (nullable int)
            var wrappedFunc = echoFunc.WrapFunc<int?, int?>();
            wrappedFunc.Should().NotBeNull("should be able to wrap echo-option-s32 function");
            
            // Test with None (null)
            var result = wrappedFunc!(null);
            result.Should().BeNull("None should return None");
            
            // Test with Some value
            result = wrappedFunc(42);
            result.Should().NotBeNull("Some should return Some");
            result.Should().Be(42);
            
            // Test with negative value
            result = wrappedFunc(-100);
            result.Should().Be(-100);
            
            // Test with zero
            result = wrappedFunc(0);
            result.Should().Be(0);
        }

        [Fact]
        public void ItCanWrapAddTuple2FunctionAsFunc()
        {
            using var context = new ComponentContext("add-tuple2");
            var addFunc = context.Function;
            
            // Wrap the function with optimized invocation for tuple<s32, s32> -> s32
            var wrappedFunc = addFunc.WrapFunc<(int, int), int>();
            wrappedFunc.Should().NotBeNull("should be able to wrap add-tuple2 function");
            
            // Test with basic values
            var result = wrappedFunc!((10, 20));
            result.Should().Be(30);
            
            // Test with negative values
            result = wrappedFunc((-50, 30));
            result.Should().Be(-20);
            
            // Test with zero values
            result = wrappedFunc((0, 0));
            result.Should().Be(0);
            
            // Test with large values
            result = wrappedFunc((1000000, 2000000));
            result.Should().Be(3000000);
            
            // Test with mixed signs
            result = wrappedFunc((-100, 200));
            result.Should().Be(100);
        }

        [Fact]
        public void ItCanWrapAddTuple3FunctionAsFunc()
        {
            using var context = new ComponentContext("add-tuple3");
            var addFunc = context.Function;
            
            // Wrap the function with optimized invocation for tuple<s32, s32, s32> -> s32
            var wrappedFunc = addFunc.WrapFunc<(int, int, int), int>();
            wrappedFunc.Should().NotBeNull("should be able to wrap add-tuple3 function");
            
            // Test with basic values
            var result = wrappedFunc!((10, 20, 30));
            result.Should().Be(60);
            
            // Test with negative values
            result = wrappedFunc((-10, -20, -30));
            result.Should().Be(-60);
            
            // Test with mixed values
            result = wrappedFunc((100, -50, 25));
            result.Should().Be(75);
            
            // Test with zero values
            result = wrappedFunc((0, 0, 0));
            result.Should().Be(0);
            
            // Test with large values
            result = wrappedFunc((1000000, 2000000, 3000000));
            result.Should().Be(6000000);
        }

        public enum Color
        {
            Red,
            Green,
            Blue
        }

        [Flags]
        public enum AllowedMethods
        {
            None = 0,
            Get = 1,
            Post = 2,
            Put = 4,
            Delete = 8
        }

        public struct Person
        {
            public string Name { get; set; }
            public byte Age { get; set; }

            public Person(string name, byte age)
            {
                Name = name;
                Age = age;
            }
        }

        // Variant type representing order-status from WIT
        public abstract class OrderStatus
        {
            // Private constructor to ensure only our cases can be created
            private OrderStatus() { }

            public sealed class Pending : OrderStatus
            {
                public static readonly Pending Instance = new Pending();
                private Pending() { }
            }

            public sealed class Shipped : OrderStatus
            {
                public int TrackingNumber { get; }
                public Shipped(int trackingNumber)
                {
                    TrackingNumber = trackingNumber;
                }
            }

            public sealed class Delivered : OrderStatus
            {
                public string Details { get; }
                public Delivered(string details)
                {
                    Details = details;
                }
            }

            // Helper methods to create instances
            public static OrderStatus CreatePending() => Pending.Instance;
            public static OrderStatus CreateShipped(int trackingNumber) => new Shipped(trackingNumber);
            public static OrderStatus CreateDelivered(string details) => new Delivered(details);
        }

        [Fact]
        public void ItCanWrapEchoEnumFunctionAsFunc()
        {
            using var context = new ComponentContext("echo-enum");
            var echoFunc = context.Function;

            // Wrap the function with optimized invocation for Color enum
            var wrappedFunc = echoFunc.WrapFunc<Color, Color>();
            wrappedFunc.Should().NotBeNull("should be able to wrap echo-enum function");

            // Test with Red
            var result = wrappedFunc!(Color.Red);
            result.Should().Be(Color.Red);

            // Test with Green
            result = wrappedFunc(Color.Green);
            result.Should().Be(Color.Green);

            // Test with Blue
            result = wrappedFunc(Color.Blue);
            result.Should().Be(Color.Blue);

            // Test multiple calls to ensure caching works
            for (int i = 0; i < 10; i++)
            {
                var testColor = (Color)(i % 3);
                result = wrappedFunc(testColor);
                result.Should().Be(testColor);
            }

            // Verify wrapper cache is reused
            var wrappedFunc2 = echoFunc.WrapFunc<Color, Color>();
            wrappedFunc2.Should().BeSameAs(wrappedFunc, "wrapper cache should be reused for enum functions");
        }
        
        [Fact]
        public void ItCanWrapEchoFlagsFunctionAsFunc()
        {
            using var context = new ComponentContext("echo-flags");
            var echoFunc = context.Function;
        
            // Wrap the function with optimized invocation for AllowedMethods flags
            var wrappedFunc = echoFunc.WrapFunc<AllowedMethods, AllowedMethods>();
            wrappedFunc.Should().NotBeNull("should be able to wrap echo-flags function");
        
            // Test with no flags (None) - start with simplest case
            var result = wrappedFunc!(AllowedMethods.None);
            result.Should().Be(AllowedMethods.None);
        
            // Test with single flag (Get)
            result = wrappedFunc(AllowedMethods.Get);
            result.Should().Be(AllowedMethods.Get);
            
            // Test with multiple flags (Get | Post)
            result = wrappedFunc(AllowedMethods.Get | AllowedMethods.Post);
            result.Should().Be(AllowedMethods.Get | AllowedMethods.Post);
            
            // Test with all flags
            result = wrappedFunc(AllowedMethods.Get | AllowedMethods.Post | AllowedMethods.Put | AllowedMethods.Delete);
            result.Should().Be(AllowedMethods.Get | AllowedMethods.Post | AllowedMethods.Put | AllowedMethods.Delete);
            
            // Test with different combinations
            result = wrappedFunc(AllowedMethods.Put | AllowedMethods.Delete);
            result.Should().Be(AllowedMethods.Put | AllowedMethods.Delete);
            
            // Test multiple calls to ensure caching works
            for (int i = 0; i < 10; i++)
            {
                var testFlags = (AllowedMethods)(i % 16); // Test various flag combinations
                result = wrappedFunc(testFlags);
                result.Should().Be(testFlags);
            }
            
            // Verify wrapper cache is reused
            var wrappedFunc2 = echoFunc.WrapFunc<AllowedMethods, AllowedMethods>();
            wrappedFunc2.Should().BeSameAs(wrappedFunc, "wrapper cache should be reused for flags functions");
        }

        
        [Fact]
        public void ItCanWrapEchoRecordFunctionAsFunc()
        {
            using var context = new ComponentContext("echo-record");
            var echoFunc = context.Function;

            // Wrap the function with optimized invocation for Person record
            var wrappedFunc = echoFunc.WrapFunc<Person, Person>();
            wrappedFunc.Should().NotBeNull("should be able to wrap echo-record function");

            // Test with typical person
            var alice = new Person("Alice", 30);
            var result = wrappedFunc!(alice);

            // Debug output to understand the issue
            testOutputHelper.WriteLine($"Result Name: '{result.Name ?? "null"}'");
            testOutputHelper.WriteLine($"Result Age: {result.Age}");

            result.Name.Should().Be("Alice");
            result.Age.Should().Be(30);

            // Test with different values
            var bob = new Person("Bob", 25);
            result = wrappedFunc(bob);
            result.Name.Should().Be("Bob");
            result.Age.Should().Be(25);

            // Test with empty name and zero age
            var emptyPerson = new Person("", 0);
            result = wrappedFunc(emptyPerson);
            result.Name.Should().Be("");
            result.Age.Should().Be(0);

            // Test with max age
            var oldPerson = new Person("Old Timer", 255);
            result = wrappedFunc(oldPerson);
            result.Name.Should().Be("Old Timer");
            result.Age.Should().Be(255);

            // Test multiple calls to ensure caching works
            for (int i = 0; i < 10; i++)
            {
                var person = new Person($"Person{i}", (byte)(20 + i));
                result = wrappedFunc(person);
                result.Name.Should().Be($"Person{i}");
                result.Age.Should().Be((byte)(20 + i));
            }

            // Verify wrapper cache is reused
            var wrappedFunc2 = echoFunc.WrapFunc<Person, Person>();
            wrappedFunc2.Should().BeSameAs(wrappedFunc, "wrapper cache should be reused for record functions");
        }

        [Fact]
        public void ItCanWrapEchoVariantFunctionAsFunc()
        {
            using var context = new ComponentContext("echo-variant");
            var echoFunc = context.Function;

            // Wrap the function with optimized invocation for OrderStatus variant
            var wrappedFunc = echoFunc.WrapFunc<OrderStatus, OrderStatus>();
            wrappedFunc.Should().NotBeNull("should be able to wrap echo-variant function");

            // Test with pending (no payload)
            var pending = OrderStatus.CreatePending();
            var result = wrappedFunc!(pending);
            result.Should().BeOfType<OrderStatus.Pending>();

            // Test with shipped(s32)
            var shipped = OrderStatus.CreateShipped(42);
            result = wrappedFunc(shipped);
            result.Should().BeOfType<OrderStatus.Shipped>();
            var shippedResult = result as OrderStatus.Shipped;
            shippedResult!.TrackingNumber.Should().Be(42);

            // Test with delivered(string)
            var delivered = OrderStatus.CreateDelivered("Package delivered to recipient");
            result = wrappedFunc(delivered);
            result.Should().BeOfType<OrderStatus.Delivered>();
            var deliveredResult = result as OrderStatus.Delivered;
            deliveredResult!.Details.Should().Be("Package delivered to recipient");

            // Test with negative tracking number
            shipped = OrderStatus.CreateShipped(-100);
            result = wrappedFunc(shipped);
            result.Should().BeOfType<OrderStatus.Shipped>();
            shippedResult = result as OrderStatus.Shipped;
            shippedResult!.TrackingNumber.Should().Be(-100);

            // Test with empty string
            delivered = OrderStatus.CreateDelivered("");
            result = wrappedFunc(delivered);
            result.Should().BeOfType<OrderStatus.Delivered>();
            deliveredResult = result as OrderStatus.Delivered;
            deliveredResult!.Details.Should().Be("");

            // Test multiple calls to ensure caching works
            for (int i = 0; i < 10; i++)
            {
                var order = (i % 3) switch
                {
                    0 => OrderStatus.CreatePending(),
                    1 => OrderStatus.CreateShipped(i * 10),
                    _ => OrderStatus.CreateDelivered($"Delivery {i}")
                };
                result = wrappedFunc(order);
                result.Should().NotBeNull();
            }

            // Verify wrapper cache is reused
            var wrappedFunc2 = echoFunc.WrapFunc<OrderStatus, OrderStatus>();
            wrappedFunc2.Should().BeSameAs(wrappedFunc, "wrapper cache should be reused for variant functions");
        }
        
        [Fact]
        public void ItCanWrapDivideNumbersFunctionAsFunc()
        {
            using var context = new ComponentContext("divide-numbers");
            var divideFunc = context.Function;

            // Wrap the function with optimized invocation for Result<int, string>
            var wrappedFunc = divideFunc.WrapFunc<int, int, Result<int, string>>();
            wrappedFunc.Should().NotBeNull("should be able to wrap divide-numbers function");

            // Test successful division
            var result = wrappedFunc!(10, 2);
            result.IsOk.Should().BeTrue();
            result.OkValue.Should().Be(5);
            result.ErrValue.Should().BeNull();

            // Test division by zero
            result = wrappedFunc(10, 0);
            result.IsOk.Should().BeFalse();
            result.OkValue.Should().Be(0); // Default value
            result.ErrValue.Should().Be("division by zero");

            // Test negative division
            result = wrappedFunc(-20, 4);
            result.IsOk.Should().BeTrue();
            result.OkValue.Should().Be(-5);

            // Test division with remainder (integer division)
            result = wrappedFunc(7, 3);
            result.IsOk.Should().BeTrue();
            result.OkValue.Should().Be(2); // Integer division

            // Test multiple calls to ensure caching works
            for (int i = 1; i <= 10; i++)
            {
                result = wrappedFunc(i * 10, i);
                result.IsOk.Should().BeTrue();
                result.OkValue.Should().Be(10);
            }

            // Verify wrapper cache is reused
            var wrappedFunc2 = divideFunc.WrapFunc<int, int, Result<int, string>>();
            wrappedFunc2.Should().BeSameAs(wrappedFunc, "wrapper cache should be reused for result functions");
        }

        // Alternative test using an echo-result function if available
        [Fact]
        public void ItCanWrapEchoResultFunctionAsFunc()
        {
            using var context = new ComponentContext("echo-result");
            var echoFunc = context.Function;

            // Wrap the function with optimized invocation
            var wrappedFunc = echoFunc.WrapFunc<Result<int, string>, Result<int, string>>();
            wrappedFunc.Should().NotBeNull("should be able to wrap echo-result function");

            // Test with Ok value
            var okResult = Result<int, string>.Ok(42);
            var result = wrappedFunc!(okResult);
            result.IsOk.Should().BeTrue();
            result.OkValue.Should().Be(42);

            // Test with Err value
            var errResult = Result<int, string>.Err("error message");
            result = wrappedFunc(errResult);
            result.IsOk.Should().BeFalse();
            result.ErrValue.Should().Be("error message");

            // Test with different Ok values
            for (int i = -10; i <= 10; i++)
            {
                okResult = Result<int, string>.Ok(i);
                result = wrappedFunc(okResult);
                result.IsOk.Should().BeTrue();
                result.OkValue.Should().Be(i);
            }

            // Test with different Err values
            string[] errors = { "error1", "error2", "", "longer error message" };
            foreach (var error in errors)
            {
                errResult = Result<int, string>.Err(error);
                result = wrappedFunc(errResult);
                result.IsOk.Should().BeFalse();
                result.ErrValue.Should().Be(error);
            }
        }

        [Fact]
        public void ResultTypeCanBeCreatedAndUsed()
        {
            // This test verifies that Result<T,E> type works correctly
            // without needing an actual WASM function

            // Test Ok result
            var okResult = Result<int, string>.Ok(42);
            okResult.IsOk.Should().BeTrue();
            okResult.OkValue.Should().Be(42);
            okResult.ErrValue.Should().BeNull();

            // Test Err result
            var errResult = Result<int, string>.Err("error message");
            errResult.IsOk.Should().BeFalse();
            errResult.ErrValue.Should().Be("error message");
            errResult.OkValue.Should().Be(0); // default(int)

            // Test with different types
            var okBool = Result<bool, int>.Ok(true);
            okBool.IsOk.Should().BeTrue();
            okBool.OkValue.Should().Be(true);

            var errInt = Result<bool, int>.Err(404);
            errInt.IsOk.Should().BeFalse();
            errInt.ErrValue.Should().Be(404);
        }

        [Fact]
        public void PerformanceComparisonTest()
        {
            using var context = new ComponentContext("add-s32");
            var addFunc = context.Function;

            // Wrapped function
            var wrappedFunc = addFunc.WrapFunc<int, int, int>();
            wrappedFunc.Should().NotBeNull();

            const int iterations = 50_000;

            // Measure wrapped function performance
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var result = wrappedFunc!(i, i + 1);
            }
            stopwatch.Stop();
            var wrappedTime = stopwatch.Elapsed;

            // Measure regular invoke performance
            stopwatch.Restart();
            for (int i = 0; i < iterations; i++)
            {
                var result = addFunc.Invoke(new ComponentValueBox[] { i, i + 1 });
            }
            stopwatch.Stop();
            var invokeTime = stopwatch.Elapsed;

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