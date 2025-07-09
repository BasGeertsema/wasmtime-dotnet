using System;
using System.Runtime.InteropServices;

namespace Wasmtime
{
    /// <summary>
    /// Extension methods for ComponentInstance to work around current limitations
    /// </summary>
    public static class ComponentInstanceExtensions
    {
        /// <summary>
        /// Gets a function from a component export index.
        /// This is a workaround for the current limitation where GetFunction doesn't work correctly
        /// with nested exports (functions within interfaces).
        /// </summary>
        public static unsafe ComponentFunction? GetFunctionFromExportIndex(
            this ComponentInstance instance, 
            Store store, 
            ComponentExportIndexHandle exportIndex)
        {
            if (exportIndex == null || exportIndex.IsInvalid)
                return null;
                
            var func = new ComponentFunction.ComponentFunc();
            bool found = Native.wasmtime_component_instance_get_func(
                instance.GetInstanceHandle(),
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

        // Helper to get the instance handle - would need to be made internal in ComponentInstance
        private static ComponentInstance.ComponentInstanceHandle GetInstanceHandle(this ComponentInstance instance)
        {
            // This is a hack - in real implementation, we'd need to expose this properly
            var field = typeof(ComponentInstance).GetField("_instance", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (ComponentInstance.ComponentInstanceHandle)field!.GetValue(instance)!;
        }

        private static class Native
        {
            [DllImport(Engine.LibraryName)]
            [return: MarshalAs(UnmanagedType.I1)]
            public static extern unsafe bool wasmtime_component_instance_get_func(
                in ComponentInstance.ComponentInstanceHandle instance, 
                IntPtr context, 
                IntPtr exportIndex, 
                ComponentFunction.ComponentFunc* funcOut);
        }
    }
}