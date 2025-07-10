using System;
using System.Runtime.InteropServices;

namespace Wasmtime
{
    /// <summary>
    /// Represents a WebAssembly component function.
    /// </summary>
    public partial class ComponentFunction
    {
        /// <summary>
        /// Determines if the underlying function reference is null.
        /// </summary>
        public bool IsNull => func.store == 0;

        /// <summary>
        /// The store this function belongs to.
        /// </summary>
        public Store? Store => store;

        /// <summary>
        /// Cache for wrapped delegates to avoid repeated allocations.
        /// </summary>
        private object? _wrapperCache;

        /// <summary>
        /// Invokes the component function with no arguments.
        /// </summary>
        /// <returns>
        ///   Returns null if the function has no return value.
        ///   Returns the value if the function returns a single value.
        ///   Returns an array of values if the function returns more than one value.
        /// </returns>
        public object? Invoke()
        {
            return Invoke(ReadOnlySpan<ComponentValueBox>.Empty);
        }

        /// <summary>
        /// Invokes the component function.
        /// </summary>
        /// <param name="arguments">The array of arguments to pass to the function.</param>
        /// <returns>
        ///   Returns null if the function has no return value.
        ///   Returns the value if the function returns a single value.
        ///   Returns an array of values if the function returns more than one value.
        /// </returns>
        public object? Invoke(params ComponentValueBox[] arguments)
        {
            return Invoke((ReadOnlySpan<ComponentValueBox>)arguments);
        }

        /// <summary>
        /// Invokes the component function.
        /// </summary>
        /// <param name="arguments">The arguments to pass to the function, wrapped in ComponentValueBox</param>
        /// <returns>
        ///   Returns null if the function has no return value.
        ///   Returns the value if the function returns a single value.
        ///   Returns an array of values if the function returns more than one value.
        /// </returns>
        public unsafe ComponentValueBox? Invoke(ReadOnlySpan<ComponentValueBox> arguments)
        {
            if (IsNull)
            {
                throw new InvalidOperationException("Cannot invoke a null function reference.");
            }

            if (store is null)
            {
                throw new InvalidOperationException("Function is not associated with a store.");
            }

            // Allocate native ComponentValue structs for arguments
            var nativeArgs = stackalloc ComponentValue[arguments.Length];
            
            // Convert arguments from ComponentValueBox to native ComponentValue
            for (int i = 0; i < arguments.Length; ++i)
            {
                try
                {
                    nativeArgs[i] = ComponentValueHelpers.FromValueBox(store, arguments[i]);
                }
                catch
                {
                    // Clean up previously allocated values
                    for (int j = 0; j < i; j++)
                    {
                        ComponentValueHelpers.ReleaseValue(&nativeArgs[j]);
                    }
                    throw;
                }
            }

            try
            {
                // For now, assume a single result (common case)
                // In a real implementation, we'd need to query the function type
                const int resultCount = 1;
                var nativeResults = stackalloc ComponentValue[resultCount];
                
                // Initialize results (the C API may require this)
                for (int i = 0; i < resultCount; i++)
                {
                    nativeResults[i] = default;
                }

                // Call the component function
                fixed (ComponentFunc* funcPtr = &func)
                {
                    var error = Native.wasmtime_component_func_call(
                        funcPtr,
                        store.Context.handle,
                        nativeArgs,
                        (nuint)arguments.Length,
                        nativeResults,
                        (nuint)resultCount
                    );
                    
                    if (error != IntPtr.Zero)
                    {
                        throw WasmtimeException.FromOwnedError(error);
                    }
                }

                // Convert the single result back to ComponentValueBox first
                ComponentValueBox? result = null;
                if (resultCount == 1)
                {
                    result = ComponentValueHelpers.ToValueBox(store, nativeResults[0]);
                }

                // Call post-return as required by the component model
                // This must be called BEFORE releasing any values
                fixed (ComponentFunc* funcPtr = &func)
                {
                    var error = Native.wasmtime_component_func_post_return(funcPtr, store.Context.handle);
                    if (error != IntPtr.Zero)
                    {
                        throw WasmtimeException.FromOwnedError(error);
                    }
                }
                
                // Note: We should NOT manually release result values here
                // The wasmtime_component_func_post_return call handles cleanup
                // of result values according to the component model
                
                return result;
            }
            finally
            {
                // Clean up argument values
                for (int i = 0; i < arguments.Length; i++)
                {
                    ComponentValueHelpers.ReleaseValue(&nativeArgs[i]);
                }

                GC.KeepAlive(store);
            }
        }

        /// <summary>
        /// Invokes the component function with optimized marshaling.
        /// Assumes arguments are the correct type, and the span is large enough to also hold the results.
        /// </summary>
        /// <typeparam name="TR">The return type</typeparam>
        /// <param name="argsAndResults">Span of arguments and results as ComponentValue.</param>
        /// <param name="resultCount">Number of results expected.</param>
        /// <param name="resultKinds">Kinds of results expected.</param>
        /// <param name="unboxResult">Function to unbox the result.</param>
        /// <param name="storeContext">The StoreContext from the store.</param>
        /// <returns>The return value from the function</returns>
        private unsafe TR InvokeWithReturn<TR>(Span<ComponentValue> argsAndResults, int resultCount, ComponentValueKind[] resultKinds, Func<ComponentValue[], TR> unboxResult, StoreContext storeContext)
        {
            if (IsNull)
            {
                throw new InvalidOperationException("Cannot invoke a null function reference.");
            }

            if (store is null)
            {
                throw new InvalidOperationException("Function is not associated with a store.");
            }

            // Calculate the number of arguments
            var argCount = argsAndResults.Length - resultCount;
            
            // Allocate separate space for results to avoid overlapping memory
            Span<ComponentValue> results = stackalloc ComponentValue[resultCount];
            
            // Initialize results
            for (int i = 0; i < resultCount; i++)
            {
                results[i] = default;
            }

            fixed (ComponentFunc* funcPtr = &func)
            fixed (ComponentValue* argsPtr = argsAndResults)
            fixed (ComponentValue* resultsPtr = results)
            {
                var error = Native.wasmtime_component_func_call(
                    funcPtr,
                    storeContext.handle,
                    argsPtr,
                    (nuint)argCount,
                    resultsPtr,
                    (nuint)resultCount
                );

                if (error != IntPtr.Zero)
                {
                    throw WasmtimeException.FromOwnedError(error);
                }
            }

            // Copy results to array for post-processing
            var resultArray = new ComponentValue[resultCount];
            for (int i = 0; i < resultCount; i++)
            {
                resultArray[i] = results[i];
            }

            // Call post-return as required by the component model
            fixed (ComponentFunc* funcPtr = &func)
            {
                var error = Native.wasmtime_component_func_post_return(funcPtr, storeContext.handle);
                if (error != IntPtr.Zero)
                {
                    throw WasmtimeException.FromOwnedError(error);
                }
            }

            GC.KeepAlive(store);

            return unboxResult(resultArray);
        }

        /// <summary>
        /// Invokes the component function with optimized marshaling and no return value.
        /// </summary>
        /// <param name="arguments">Span of arguments as ComponentValue.</param>
        /// <param name="storeContext">The StoreContext from the store.</param>
        private unsafe void InvokeWithoutReturn(Span<ComponentValue> arguments, StoreContext storeContext)
        {
            if (IsNull)
            {
                throw new InvalidOperationException("Cannot invoke a null function reference.");
            }

            if (store is null)
            {
                throw new InvalidOperationException("Function is not associated with a store.");
            }

            fixed (ComponentFunc* funcPtr = &func)
            fixed (ComponentValue* argsPtr = arguments)
            {
                var error = Native.wasmtime_component_func_call(
                    funcPtr,
                    storeContext.handle,
                    argsPtr,
                    (nuint)arguments.Length,
                    null,
                    0
                );

                if (error != IntPtr.Zero)
                {
                    throw WasmtimeException.FromOwnedError(error);
                }
            }

            // Call post-return as required by the component model
            fixed (ComponentFunc* funcPtr = &func)
            {
                var error = Native.wasmtime_component_func_post_return(funcPtr, storeContext.handle);
                if (error != IntPtr.Zero)
                {
                    throw WasmtimeException.FromOwnedError(error);
                }
            }

            GC.KeepAlive(store);
        }

        internal ComponentFunction(Store store, ComponentFunc func)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.func = func;
        }

        private static class Native
        {
            [DllImport(Engine.LibraryName)]
            public static unsafe extern IntPtr wasmtime_component_func_call(
                ComponentFunc* func,
                IntPtr context,
                ComponentValue* args,
                nuint args_size,
                ComponentValue* results,
                nuint results_size
            );

            [DllImport(Engine.LibraryName)]
            public static unsafe extern IntPtr wasmtime_component_func_post_return(
                ComponentFunc* func,
                IntPtr context
            );
        }

        private readonly Store? store;
        private readonly ComponentFunc func;

        /// <summary>
        /// Native component function representation matching wasmtime_component_func_t
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        public struct ComponentFunc
        {
            [FieldOffset(0)]
            public ulong store;
            [FieldOffset(8)]
            public uint __private1;
            [FieldOffset(16)]
            public uint __private2;
        }
    }
}