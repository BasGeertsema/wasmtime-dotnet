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
        
        internal ComponentInstance(Store store, ExternComponentInstance instance)
        {
            if (store is null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            this._store = store;
            this._instance = instance;
        }

        private static class Native
        {
            [DllImport(Engine.LibraryName)]
            public static extern unsafe IntPtr wasmtime_component_instance_get_export_index(in ExternComponentInstance instance, IntPtr context, IntPtr instanceExportIndex, byte* name, nuint nameLength);
        }

        private readonly Store _store;
        private readonly ExternComponentInstance _instance;
    }
}
