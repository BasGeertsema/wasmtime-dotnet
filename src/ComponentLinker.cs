using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Wasmtime
{
    /// <summary>
    /// Represents the Wasmtime linker that can be used to define imports
    /// and instantiate WebAssembly components.
    /// </summary>
    public class ComponentLinker : IDisposable
    {
        /// <summary>
        /// Constructs a new linker from the given engine.
        /// </summary>
        /// <param name="engine">The Wasmtime engine to use for the linker.</param>
        public ComponentLinker(Engine engine)
        {
            if (engine is null)
            {
                throw new ArgumentNullException(nameof(engine));
            }

            handle = new Handle(Native.wasmtime_component_linker_new(engine.NativeHandle));
        }

        /// <summary>
        /// Returns the "root instance" of this linker, used to define names into the root namespace. 
        /// </summary>
        /// <remarks>
        /// WARNING: this acquires exclusive access to this <see cref="ComponentLinker"/>. The <see cref="ComponentLinker"/>
        /// *MUST* nit be accessed by anything until the returned <see cref="ComponentLinkerInstance"/> is disposed. 
        /// </remarks>
        /// <returns>The root instance of this linker</returns>
        public ComponentLinkerInstance GetRoot()
        {
            var linkerInstanceHandle = new ComponentLinkerInstance.Handle(Native.wasmtime_component_linker_root(this.handle));
            return new ComponentLinkerInstance(linkerInstanceHandle);
        }

        /// <summary>
        /// Instantiates a component with imports from items defined in the linker.
        /// </summary>
        /// <param name="store">The store to instantiate in.</param>
        /// <param name="component">The component to instantiate.</param>
        /// <returns>Returns the new instance.</returns>
        public ComponentInstance Instantiate(Store store, Component component)
        {
            if (store is null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            if (component is null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            var error = Native.wasmtime_component_linker_instantiate(handle, store.Context.handle, component.NativeHandle, out var instance);
            GC.KeepAlive(store);

            if (error != IntPtr.Zero)
            {
                throw WasmtimeException.FromOwnedError(error);
            }

            return new ComponentInstance(store, instance);
        }



        /// <inheritdoc/>
        public void Dispose()
        {
            handle.Dispose();
        }
        
        internal class Handle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public Handle(IntPtr handle)
                : base(true)
            {
                SetHandle(handle);
            }

            protected override bool ReleaseHandle()
            {
                Native.wasmtime_component_linker_delete(handle);
                return true;
            }
        }

        internal static class Native
        {
            [DllImport(Engine.LibraryName)]
            public static extern IntPtr wasmtime_component_linker_new(Engine.Handle engine);

            [DllImport(Engine.LibraryName)]
            public static extern void wasmtime_component_linker_delete(IntPtr linker);

            [DllImport(Engine.LibraryName)]
            public static extern IntPtr wasmtime_component_linker_instantiate(Handle linker, IntPtr context, Component.Handle component, out ExternComponentInstance instance);
            
            [DllImport(Engine.LibraryName)]
            public static extern IntPtr wasmtime_component_linker_root(ComponentLinker.Handle linker);
        }

        private readonly Handle handle;
    }
}