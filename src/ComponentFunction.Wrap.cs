#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Wasmtime
{
    public partial class ComponentFunction
    {
        /// <summary>
        /// Check if a ComponentValueKind needs cleanup after use
        /// </summary>
        private static bool NeedsCleanup(ComponentValueKind kind)
        {
            return kind switch
            {
                ComponentValueKind.String => true,
                ComponentValueKind.List => true,
                ComponentValueKind.Record => true,
                ComponentValueKind.Tuple => true,
                ComponentValueKind.Variant => true,
                ComponentValueKind.Option => true,
                ComponentValueKind.Result => true,
                ComponentValueKind.Flags => true,
                _ => false
            };
        }

        /// <summary>
        /// Check if a ComponentValue has allocated data that needs cleanup
        /// </summary>
        private static unsafe bool HasAllocatedData(in ComponentValue value)
        {
            return value.kind switch
            {
                ComponentValueKind.String => value.of.@string.data != null,
                ComponentValueKind.List => value.of.list.data != null,
                ComponentValueKind.Record => value.of.record.data != null,
                ComponentValueKind.Tuple => false, //value.of.tuple.data != null,
                ComponentValueKind.Variant => value.of.variant.value != null || value.of.variant.discriminant.data != null,
                ComponentValueKind.Option => value.of.option != null,
                ComponentValueKind.Result => value.of.result.value != null,
                // we need to check .size > 0 here because data is 0x08 sometimes when we set it to null initially?!
                ComponentValueKind.Flags => value.of.flags.data != null && value.of.flags.size > 0,
                _ => false
            };
        }

        /// <summary>
        /// Get the number of ComponentValue slots required for a type
        /// </summary>
        private static int GetComponentValueSlotCount(Type type)
        {
            if (type.IsTupleType())
            {
                return type.GetGenericArguments().Length;
            }
            return 1;
        }

        /// <summary>
        /// Box a tuple value into a single ComponentValue
        /// </summary>
        private static unsafe ComponentValue BoxTupleToComponentValue<T>(StoreContext storeContext, Store store, T value, ComponentValue* data)
        {
            var type = typeof(T);
            
            // For ValueTuple<int, int>, we can cast directly
            if (type == typeof(ValueTuple<int, int>))
            {
                var tuple = (ValueTuple<int, int>)(object)value!;
                var converter1 = ComponentValueRaw.Converter<int>();
                var converter2 = ComponentValueRaw.Converter<int>();
                
                // Allocate space for tuple elements
                //var data = (ComponentValue*)System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(ComponentValue) * 2);
                
                var raw1 = default(ComponentValueRaw);
                converter1.Box(storeContext, store, ref raw1, tuple.Item1);
                data[0] = raw1.ToComponentValue(converter1.Kind);
                
                var raw2 = default(ComponentValueRaw);
                converter2.Box(storeContext, store, ref raw2, tuple.Item2);
                data[1] = raw2.ToComponentValue(converter2.Kind);
                
                var result = default(ComponentValue);
                result.kind = ComponentValueKind.Tuple;
                result.of.tuple.data = data;
                result.of.tuple.size = 2;
                
                return result;
            }
            else if (type == typeof(ValueTuple<int, int, int>))
            {
                var tuple = (ValueTuple<int, int, int>)(object)value!;
                var converter = ComponentValueRaw.Converter<int>();
                
                // Allocate space for tuple elements
                //var data = (ComponentValue*)System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(ComponentValue) * 3);
                
                var raw1 = default(ComponentValueRaw);
                converter.Box(storeContext, store, ref raw1, tuple.Item1);
                data[0] = raw1.ToComponentValue(converter.Kind);
                
                var raw2 = default(ComponentValueRaw);
                converter.Box(storeContext, store, ref raw2, tuple.Item2);
                data[1] = raw2.ToComponentValue(converter.Kind);
                
                var raw3 = default(ComponentValueRaw);
                converter.Box(storeContext, store, ref raw3, tuple.Item3);
                data[2] = raw3.ToComponentValue(converter.Kind);
                
                var result = default(ComponentValue);
                result.kind = ComponentValueKind.Tuple;
                result.of.tuple.data = data;
                result.of.tuple.size = 3;
                
                return result;
            }
            else
            {
                throw new NotSupportedException($"Tuple type {type} is not yet supported in BoxTupleToComponentValue");
            }
        }
        /// <summary>
        /// Attempt to wrap this function as an Action. Wrapped Action is faster than a normal Invoke call.
        /// </summary>
        /// <returns>An Action to invoke this function, or null if the type signature is incompatible.</returns>
        public Action? WrapAction()
        {
            if (store is null || IsNull)
            {
                throw new InvalidOperationException("Cannot wrap a null function reference.");
            }

            // Try to retrieve it from the cache
            if (_wrapperCache?.GetType() == typeof(Action))
            {
                return (Action)_wrapperCache;
            }

            // For now, assume no parameters and no results
            Action result = () =>
            {
                var storeContext = store.Context;
                InvokeWithoutReturn(Span<ComponentValue>.Empty, storeContext);
            };

            _wrapperCache = result;
            return result;
        }

        /// <summary>
        /// Attempt to wrap this function as an Action&lt;T&gt;. Wrapped Action is faster than a normal Invoke call.
        /// </summary>
        /// <returns>An Action to invoke this function, or null if the type signature is incompatible.</returns>
        public Action<T>? WrapAction<T>()
        {
            if (store is null || IsNull)
            {
                throw new InvalidOperationException("Cannot wrap a null function reference.");
            }

            // Try to retrieve it from the cache
            if (_wrapperCache?.GetType() == typeof(Action<T>))
            {
                return (Action<T>)_wrapperCache;
            }

            // Get converter for the parameter type
            var converter = ComponentValueRaw.Converter<T>();

            Action<T> result = (p0) =>
            {
                unsafe
                {
                    Span<ComponentValue> args = stackalloc ComponentValue[1];
                    var storeContext = store.Context;

                    // Convert parameter to ComponentValue
                    var raw = default(ComponentValueRaw);
                    converter.Box(storeContext, store, ref raw, p0);
                    args[0] = raw.ToComponentValue(converter.Kind);

                    try
                    {
                        InvokeWithoutReturn(args, storeContext);
                    }
                    finally
                    {
                        // Clean up any allocated memory for complex types
                        if (NeedsCleanup(converter.Kind) && HasAllocatedData(args[0]))
                        {
                            fixed (ComponentValue* argPtr = &args[0])
                            {
                                ComponentValueHelpers.ReleaseValue(argPtr);
                            }
                        }
                    }
                }
            };

            _wrapperCache = result;
            return result;
        }

        /// <summary>
        /// Attempt to wrap this function as an Action&lt;T1, T2&gt;. Wrapped Action is faster than a normal Invoke call.
        /// </summary>
        /// <returns>An Action to invoke this function, or null if the type signature is incompatible.</returns>
        public Action<T1, T2>? WrapAction<T1, T2>()
        {
            if (store is null || IsNull)
            {
                throw new InvalidOperationException("Cannot wrap a null function reference.");
            }

            // Try to retrieve it from the cache
            if (_wrapperCache?.GetType() == typeof(Action<T1, T2>))
            {
                return (Action<T1, T2>)_wrapperCache;
            }

            // Get converters for the parameter types
            var converter1 = ComponentValueRaw.Converter<T1>();
            var converter2 = ComponentValueRaw.Converter<T2>();

            Action<T1, T2> result = (p0, p1) =>
            {
                unsafe
                {
                    Span<ComponentValue> args = stackalloc ComponentValue[2];
                    var storeContext = store.Context;

                    // Convert parameters to ComponentValue
                    var raw0 = default(ComponentValueRaw);
                    converter1.Box(storeContext, store, ref raw0, p0);
                    args[0] = raw0.ToComponentValue(converter1.Kind);

                    var raw1 = default(ComponentValueRaw);
                    converter2.Box(storeContext, store, ref raw1, p1);
                    args[1] = raw1.ToComponentValue(converter2.Kind);

                    try
                    {
                        InvokeWithoutReturn(args, storeContext);
                    }
                    finally
                    {
                        // Clean up any allocated memory for complex types
                        if (NeedsCleanup(converter1.Kind) && HasAllocatedData(args[0]))
                        {
                            fixed (ComponentValue* argPtr = &args[0])
                            {
                                ComponentValueHelpers.ReleaseValue(argPtr);
                            }
                        }
                        if (NeedsCleanup(converter2.Kind) && HasAllocatedData(args[1]))
                        {
                            fixed (ComponentValue* argPtr = &args[1])
                            {
                                ComponentValueHelpers.ReleaseValue(argPtr);
                            }
                        }
                    }
                }
            };

            _wrapperCache = result;
            return result;
        }

        /// <summary>
        /// Attempt to wrap this function as a Func&lt;TR&gt;. Wrapped Func is faster than a normal Invoke call.
        /// </summary>
        /// <returns>A Func to invoke this function, or null if the type signature is incompatible.</returns>
        public Func<TR>? WrapFunc<TR>()
        {
            if (store is null || IsNull)
            {
                throw new InvalidOperationException("Cannot wrap a null function reference.");
            }

            // Try to retrieve it from the cache
            if (_wrapperCache?.GetType() == typeof(Func<TR>))
            {
                return (Func<TR>)_wrapperCache;
            }

            // Get converter for the return type
            var returnConverter = ComponentValueRaw.Converter<TR>();

            Func<TR> result = () =>
            {
                unsafe
                {
                    // Allocate space for the result
                    Span<ComponentValue> argsAndResults = stackalloc ComponentValue[1];
                    var storeContext = store.Context;

                    var resultKinds = new[] { returnConverter.Kind };
                    return InvokeWithReturn<TR>(argsAndResults, 1, resultKinds, (results) =>
                    {
                        var raw = ComponentValueRaw.FromComponentValue(results[0]);
                        var value = returnConverter.Unbox(store.Context, store, raw, results[0].kind);

                        // Clean up any allocated memory for complex types in the result
                        if (NeedsCleanup(returnConverter.Kind) && HasAllocatedData(results[0]))
                        {
                            fixed (ComponentValue* resultPtr = &results[0])
                            {
                                ComponentValueHelpers.ReleaseValue(resultPtr);
                            }
                        }

                        return value!;
                    }, storeContext);
                }
            };

            _wrapperCache = result;
            return result;
        }

        /// <summary>
        /// Attempt to wrap this function as a Func&lt;T, TR&gt;. Wrapped Func is faster than a normal Invoke call.
        /// </summary>
        /// <returns>A Func to invoke this function, or null if the type signature is incompatible.</returns>
        public Func<T, TR>? WrapFunc<T, TR>()
        {
            if (store is null || IsNull)
            {
                throw new InvalidOperationException("Cannot wrap a null function reference.");
            }

            // Try to retrieve it from the cache
            if (_wrapperCache?.GetType() == typeof(Func<T, TR>))
            {
                return (Func<T, TR>)_wrapperCache;
            }

            // Get converters
            var paramConverter = ComponentValueRaw.Converter<T>();
            var returnConverter = ComponentValueRaw.Converter<TR>();

            Func<T, TR> result = (p0) =>
            {
                unsafe
                {
                    // Allocate space for argument and result
                    Span<ComponentValue> argsAndResults = stackalloc ComponentValue[2];
                    var storeContext = store.Context;

                    // Convert parameter to ComponentValue
                    if (typeof(T).IsTupleType())
                    {
                        if (typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,>))
                        {
                            ComponentValue* tupledata = stackalloc ComponentValue[2];
                            
                            // For tuples, we need to create a proper tuple ComponentValue
                            argsAndResults[0] = BoxTupleToComponentValue(storeContext, store, p0, tupledata);
                        }
                        if (typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,,>))
                        {
                            ComponentValue* tupledata = stackalloc ComponentValue[3];
                            
                            // For tuples, we need to create a proper tuple ComponentValue
                            argsAndResults[0] = BoxTupleToComponentValue(storeContext, store, p0, tupledata);
                        }
                    }
                    else
                    {
                        // For non-tuples, use the existing logic
                        var raw = default(ComponentValueRaw);
                        paramConverter.Box(storeContext, store, ref raw, p0);
                        argsAndResults[0] = raw.ToComponentValue(paramConverter.Kind);
                    }

                    try
                    {
                        var resultKinds = new[] { returnConverter.Kind };
                        return InvokeWithReturn<TR>(argsAndResults, 1, resultKinds, (results) =>
                        {
                            var rawResult = ComponentValueRaw.FromComponentValue(results[0]);
                            var value = returnConverter.Unbox(store.Context, store, rawResult, results[0].kind);

                            // Clean up any allocated memory for complex types in the result
                            if (NeedsCleanup(returnConverter.Kind) && HasAllocatedData(results[0]))
                            {
                                fixed (ComponentValue* resultPtr = &results[0])
                                {
                                    ComponentValueHelpers.ReleaseValue(resultPtr);
                                }
                            }

                            return value!;
                        }, storeContext);
                    }
                    finally
                    {
                        // Clean up any allocated memory for complex types in parameters
                        if (HasAllocatedData(argsAndResults[0]))
                        {
                            fixed (ComponentValue* argPtr = &argsAndResults[0])
                            {
                                ComponentValueHelpers.ReleaseValue(argPtr);
                            }
                        }
                    }
                }
            };

            _wrapperCache = result;
            return result;
        }

        /// <summary>
        /// Attempt to wrap this function as a Func&lt;T1, T2, TR&gt;. Wrapped Func is faster than a normal Invoke call.
        /// </summary>
        /// <returns>A Func to invoke this function, or null if the type signature is incompatible.</returns>
        public Func<T1, T2, TR>? WrapFunc<T1, T2, TR>()
        {
            if (store is null || IsNull)
            {
                throw new InvalidOperationException("Cannot wrap a null function reference.");
            }

            // Try to retrieve it from the cache
            if (_wrapperCache?.GetType() == typeof(Func<T1, T2, TR>))
            {
                return (Func<T1, T2, TR>)_wrapperCache;
            }

            // Get converters
            var converter1 = ComponentValueRaw.Converter<T1>();
            var converter2 = ComponentValueRaw.Converter<T2>();
            var returnConverter = ComponentValueRaw.Converter<TR>();

            Func<T1, T2, TR> result = (p0, p1) =>
            {
                unsafe
                {
                    // Allocate space for arguments and result
                    Span<ComponentValue> argsAndResults = stackalloc ComponentValue[3];
                    var storeContext = store.Context;

                    // Convert parameters to ComponentValue
                    var raw0 = default(ComponentValueRaw);
                    converter1.Box(storeContext, store, ref raw0, p0);
                    argsAndResults[0] = raw0.ToComponentValue(converter1.Kind);

                    var raw1 = default(ComponentValueRaw);
                    converter2.Box(storeContext, store, ref raw1, p1);
                    argsAndResults[1] = raw1.ToComponentValue(converter2.Kind);

                    try
                    {
                        var resultKinds = new[] { returnConverter.Kind };
                        return InvokeWithReturn<TR>(argsAndResults, 1, resultKinds, (results) =>
                        {
                            var rawResult = ComponentValueRaw.FromComponentValue(results[0]);
                            var value = returnConverter.Unbox(store.Context, store, rawResult, results[0].kind);

                            // Clean up any allocated memory for complex types in the result
                            if (NeedsCleanup(returnConverter.Kind) && HasAllocatedData(results[0]))
                            {
                                fixed (ComponentValue* resultPtr = &results[0])
                                {
                                    ComponentValueHelpers.ReleaseValue(resultPtr);
                                }
                            }

                            return value!;
                        }, storeContext);
                    }
                    finally
                    {
                        // Clean up any allocated memory for complex types in parameters
                        if (NeedsCleanup(converter1.Kind) && HasAllocatedData(argsAndResults[0]))
                        {
                            fixed (ComponentValue* argPtr = &argsAndResults[0])
                            {
                                ComponentValueHelpers.ReleaseValue(argPtr);
                            }
                        }
                        if (NeedsCleanup(converter2.Kind) && HasAllocatedData(argsAndResults[1]))
                        {
                            fixed (ComponentValue* argPtr = &argsAndResults[1])
                            {
                                ComponentValueHelpers.ReleaseValue(argPtr);
                            }
                        }
                    }
                }
            };

            _wrapperCache = result;
            return result;
        }
    }
}