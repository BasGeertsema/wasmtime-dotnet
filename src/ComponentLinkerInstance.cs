using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Wasmtime;


/// <summary>
/// The finalizer for the host-specific data passed to wasmtime_component_linker_instance_add_func.
/// </summary>
public delegate void HostSpecificDataFinalizer();


/// <summary>
/// Encapsulates an component function callback that receives arguments and can set results via a span of <see cref="ValueBox"/>.
/// </summary>
/// <param name="caller">The caller.</param>
/// <param name="arguments">The function arguments.</param>
/// <param name="results">The function results. These must be set (using the correct type) before returning, except when the method throws (in which case they are ignored).</param>
public unsafe delegate void UntypedCallbackDelegate(
    IntPtr caller, ReadOnlySpan<ValueBox> arguments, Span<ValueBox> results);

/// <summary>
/// An instance of the component linker
/// </summary>
public class ComponentLinkerInstance : IDisposable
{
    internal ComponentLinkerInstance(Handle handle)
    {
        this.handle = handle;
    }

    /// <summary>
    /// Defines a nested instance within this instance
    /// </summary>
    /// <remarks>
    /// This can be used to describe arbitrarily nested levels of instances within a
    /// linker to satisfy nested instance exports of components.
    ///
    /// WARNING: This acquires exclusive access to the linker instance. This linker instance
    /// *MUST* not be accessed by anything until the returned linker instance is disposed of.
    /// </remarks>
    /// <param name="name"></param>
    /// <returns></returns>
    /// <exception cref="WasmtimeException"></exception>
    public ComponentLinkerInstance AddInstance(string name)
    {
        unsafe
        {
            var nameBytes = Encoding.UTF8.GetBytes(name);
            fixed (byte* namePtr = nameBytes)
            {
                var error = Native.wasmtime_component_linker_instance_add_instance(handle, namePtr, (nuint)nameBytes.Length, out var instanceHandle);
                if (error != IntPtr.Zero)
                {
                    throw WasmtimeException.FromOwnedError(error);
                }
                
                return new(instanceHandle);
            }
        }
    }

    /// <summary>
    /// Defines a <see cref="Module"/> within this instance
    /// </summary>
    /// <remarks>
    /// This can be used to provide a core wasm module as an import to a component. The module provided
    /// is saved within the linker for the specified <paramref name="name"/> in this instance.
    /// </remarks>
    /// <param name="name"></param>
    /// <param name="module"></param>
    public void DefineModule(string name, Module module)
    {
        if (module is null)
        {
            throw new ArgumentNullException(nameof(module));
        }

        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        
        using var nameBytes = name.ToUTF8(stackalloc byte[Math.Min(64, name.Length * 2)]);
        unsafe
        {
            fixed (byte* namePtr = nameBytes.Span)
            {
                var error = Native.wasmtime_component_linker_instance_add_module(handle, namePtr, (nuint)nameBytes.Length, module.NativeHandle);
                if (error != IntPtr.Zero)
                {
                    throw WasmtimeException.FromOwnedError(error);
                }
            }
        }
    }
    
    /// <summary>
    /// Defines an function in the linker given an untyped callback.
    /// </summary>
    /// <remarks>Functions defined with this method are store-independent.</remarks>
    /// <param name="module">The module name of the function.</param>
    /// <param name="name">The name of the function.</param>
    /// <param name="callback">The callback for when the function is invoked.</param>
    public void DefineFunction(string module, string name)
    {
        if (module is null)
        {
            throw new ArgumentNullException(nameof(module));
        }

        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        // if (callback is null)
        // {
        //     throw new ArgumentNullException(nameof(callback));
        // }

        unsafe
        {
            Native.WasmtimeComponentFuncCallback func = (data, ctx, args, nargs, results, nresults) =>
            {
                // TODO: implement actually calling the function!
                // for now it is a no-op
                return IntPtr.Zero;
            };

            using var nameBytes = name.ToUTF8(stackalloc byte[Math.Min(64, name.Length * 2)]);
            
            fixed (byte* namePtr = nameBytes.Span)
            {
                var error = Native.wasmtime_component_linker_instance_add_func(
                    handle,
                    namePtr,
                    (nuint)nameBytes.Length,
                    func,
                    GCHandle.ToIntPtr(GCHandle.Alloc(func)),
                    Finalizer
                );

                if (error != IntPtr.Zero)
                {
                    throw WasmtimeException.FromOwnedError(error);
                }
            }
        }
    }
    
    internal static readonly Native.Finalizer Finalizer = (p) => GCHandle.FromIntPtr(p).Free();
        
    /// <summary>
    /// 
    /// </summary>
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
            Native.wasmtime_component_linker_instance_delete(handle);
            return true;
        }
    }

    internal static class Native
    {
        /// <summary>
        /// Callback delegate for component functions in wasmtime_component_linker_instance_add_func.
        /// </summary>
        /// <param name="data">User-defined data pointer passed to wasmtime_component_linker_instance_add_func</param>
        /// <param name="context">The wasmtime context</param>
        /// <param name="args">Pointer to the array of input wasmtime_component_val_t arguments</param>
        /// <param name="nargs">Number of input arguments</param>
        /// <param name="results">Pointer to the array of wasmtime_component_val_t where results should be written</param>
        /// <param name="nresults">Number of results expected</param>
        /// <returns>Pointer to a wasmtime_error_t on error, or IntPtr.Zero on success</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public unsafe delegate IntPtr WasmtimeComponentFuncCallback(
            IntPtr data,
            IntPtr context,
            IntPtr args,
            nuint nargs,
            IntPtr results,
            nuint nresults);
        
        public delegate void Finalizer(IntPtr data);
        
        [DllImport(Engine.LibraryName)]
        public static extern void wasmtime_component_linker_instance_delete(IntPtr linker);
        
        [DllImport(Engine.LibraryName)]
        public static extern unsafe IntPtr wasmtime_component_linker_instance_add_instance(Handle linkerInstance, byte* name, nuint nameLen, out Handle addedLinkerInstanceHandle);
        
        [DllImport(Engine.LibraryName)]
        public static extern unsafe IntPtr wasmtime_component_linker_instance_add_module(Handle linkerInstance, byte* name, nuint nameLen, Module.Handle module);
        
        [DllImport(Engine.LibraryName)]
        public static extern unsafe IntPtr wasmtime_component_linker_instance_add_func(Handle linkerInstance, byte* name, nuint nameLen, WasmtimeComponentFuncCallback callback, IntPtr hostData, Finalizer finalizer);
    }
        
    private readonly Handle handle;
}