#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Wasmtime
{
    public partial class ComponentFunction
    {
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
                        if (converter.Kind == ComponentValueKind.String && args[0].of.@string.data != null)
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
                        if (converter1.Kind == ComponentValueKind.String && args[0].of.@string.data != null)
                        {
                            fixed (ComponentValue* argPtr = &args[0])
                            {
                                ComponentValueHelpers.ReleaseValue(argPtr);
                            }
                        }
                        if (converter2.Kind == ComponentValueKind.String && args[1].of.@string.data != null)
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
                        if (returnConverter.Kind == ComponentValueKind.String && results[0].of.@string.data != null)
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
                    var raw = default(ComponentValueRaw);
                    paramConverter.Box(storeContext, store, ref raw, p0);
                    argsAndResults[0] = raw.ToComponentValue(paramConverter.Kind);

                    try
                    {
                        var resultKinds = new[] { returnConverter.Kind };
                        return InvokeWithReturn<TR>(argsAndResults, 1, resultKinds, (results) =>
                        {
                            var rawResult = ComponentValueRaw.FromComponentValue(results[0]);
                            var value = returnConverter.Unbox(store.Context, store, rawResult, results[0].kind);

                            // Clean up any allocated memory for complex types in the result
                            if (returnConverter.Kind == ComponentValueKind.String && results[0].of.@string.data != null)
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
                        if (paramConverter.Kind == ComponentValueKind.String && argsAndResults[0].of.@string.data != null)
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
                            if (returnConverter.Kind == ComponentValueKind.String && results[0].of.@string.data != null)
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
                        if (converter1.Kind == ComponentValueKind.String && argsAndResults[0].of.@string.data != null)
                        {
                            fixed (ComponentValue* argPtr = &argsAndResults[0])
                            {
                                ComponentValueHelpers.ReleaseValue(argPtr);
                            }
                        }
                        if (converter2.Kind == ComponentValueKind.String && argsAndResults[1].of.@string.data != null)
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