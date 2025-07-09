using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Wasmtime
{
    /// <summary>
    /// Represents an instantiated WebAssembly component.
    /// </summary>
    public class ComponentInstance
    {
        /// <summary>
        /// Native representation of wasmtime_component_instance_t
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct ComponentInstanceHandle
        {
            static ComponentInstanceHandle() => System.Diagnostics.Debug.Assert(Marshal.SizeOf(typeof(ComponentInstanceHandle)) == 16);
            
            public ulong store_id;
            public uint __private;
        }
        /// <summary>
        /// Looks up a specific export of this component by name, optionally nested within the instance provided
        /// </summary>
        /// <param name="name">The name of the export</param>
        /// <param name="store">The store</param>
        /// <param name="lookupInstance">optional instance to look up in</param>
        /// <param name="exportIndex">The handle to the export if found</param>
        /// <returns>True if found, else false</returns>
        public bool TryGetExportIndex(string name, Store store, ComponentExportIndexHandle? lookupInstance, out ComponentExportIndexHandle exportIndex)
        {
            using var nameBytes = name.ToUTF8(stackalloc byte[Math.Min(64, name.Length * 2)]);

            unsafe
            {
                fixed (byte* namePtr = nameBytes.Span)
                {
                    var index = Native.wasmtime_component_instance_get_export_index(
                        _instance,
                        store.Context.handle,
                        lookupInstance?.DangerousGetHandle() ?? IntPtr.Zero,
                        namePtr,
                        (nuint)nameBytes.Length);
                    
                    GC.KeepAlive(store);
                    
                    if (index == IntPtr.Zero)
                    {
                        exportIndex = new(IntPtr.Zero);
                        return false;
                    }

                    exportIndex = new(index);
                    return true;

                }
            }
        }

        /// <summary>
        /// Gets an exported function by name from this component instance.
        /// </summary>
        /// <param name="name">The name of the function to get</param>
        /// <param name="store">The store this instance belongs to</param>
        /// <param name="lookupInstance">Optional instance to look up in</param>
        /// <returns>The component function if found, otherwise null</returns>
        public ComponentFunction? GetFunction(string name, Store store, ComponentExportIndexHandle? lookupInstance = null)
        {
            if (!TryGetExportIndex(name, store, lookupInstance, out var exportIndex))
            {
                return null;
            }

            using (exportIndex)
            {
                unsafe
                {
                    var func = new ComponentFunction.ComponentFunc();
                    bool found = Native.wasmtime_component_instance_get_func(
                        _instance,
                        store.Context.handle,
                        exportIndex.DangerousGetHandle(),
                        &func);

                    GC.KeepAlive(store);

                    if (!found)
                    {
                        return null;
                    }

                    return new ComponentFunction(store, func);
                }
            }
        }
        
        internal ComponentInstance(Store store, ComponentInstanceHandle instance)
        {
            if (store is null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            this._store = store;
            this._instance = instance;
        }
        
        internal ComponentInstance(Store store, ExternComponentInstance externInstance)
        {
            if (store is null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            this._store = store;
            // Convert ExternComponentInstance to ComponentInstanceHandle
            this._instance = new ComponentInstanceHandle
            {
                store_id = externInstance.store,
                __private = (uint)externInstance.__private
            };
        }

        private static class Native
        {
            [DllImport(Engine.LibraryName)]
            public static extern unsafe IntPtr wasmtime_component_instance_get_export_index(in ComponentInstanceHandle instance, IntPtr context, IntPtr instanceExportIndex, byte* name, nuint nameLength);

            [DllImport(Engine.LibraryName)]
            [return: MarshalAs(UnmanagedType.I1)]
            public static extern unsafe bool wasmtime_component_instance_get_func(in ComponentInstanceHandle instance, IntPtr context, IntPtr exportIndex, ComponentFunction.ComponentFunc* funcOut);
        }

        private readonly Store _store;
        private readonly ComponentInstanceHandle _instance;
    }
}
