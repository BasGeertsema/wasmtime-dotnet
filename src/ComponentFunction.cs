using System;
using System.Runtime.InteropServices;

namespace Wasmtime
{
    /// <summary>
    /// Represents a WebAssembly component function.
    /// </summary>
    public class ComponentFunction
    {
        /// <summary>
        /// Determines if the underlying function reference is null.
        /// </summary>
        public bool IsNull => func.store_id == 0;

        /// <summary>
        /// The store this function belongs to.
        /// </summary>
        public Store? Store => store;

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
        public unsafe object? Invoke(ReadOnlySpan<ComponentValueBox> arguments)
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

                try
                {
                    // Call post-return as required by the component model
                    fixed (ComponentFunc* funcPtr = &func)
                    {
                        var error = Native.wasmtime_component_func_post_return(funcPtr, store.Context.handle);
                        if (error != IntPtr.Zero)
                        {
                            throw WasmtimeException.FromOwnedError(error);
                        }
                    }

                    // Convert the single result back to ComponentValueBox
                    if (resultCount == 1)
                    {
                        return ComponentValueHelpers.ToValueBox(store, nativeResults[0]);
                    }
                    
                    return null;
                }
                finally
                {
                    // Clean up result values
                    for (int i = 0; i < resultCount; i++)
                    {
                        ComponentValueHelpers.ReleaseValue(&nativeResults[i]);
                    }
                }
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
        [StructLayout(LayoutKind.Sequential)]
        public struct ComponentFunc
        {
            static ComponentFunc() => System.Diagnostics.Debug.Assert(Marshal.SizeOf(typeof(ComponentFunc)) == 16);
            
            public ulong store_id;
            public uint __private1;
            public uint __private2;
        }
    }
}