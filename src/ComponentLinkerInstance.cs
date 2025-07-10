using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;
using System.Text;

#nullable enable

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
                var error = Native.wasmtime_component_linker_instance_add_instance(handle, namePtr, (nuint)nameBytes.Length, out var instancePtr);
                if (error != IntPtr.Zero)
                {
                    throw WasmtimeException.FromOwnedError(error);
                }
                
                return new(new Handle(instancePtr));
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
    /// Defines a function in the linker with no parameters or return value.
    /// </summary>
    /// <remarks>Functions defined with this method are store-independent.</remarks>
    /// <param name="module">The module name of the function.</param>
    /// <param name="name">The name of the function.</param>
    /// <param name="callback">The callback for when the function is invoked.</param>
    public void DefineFunction(string module, string name, Action callback)
    {
        if (module is null)
        {
            throw new ArgumentNullException(nameof(module));
        }

        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (callback is null)
        {
            throw new ArgumentNullException(nameof(callback));
        }

        unsafe
        {
            Native.WasmtimeComponentFuncCallback func = (data, ctx, args, nargs, results, nresults) =>
            {
                try
                {
                    // Verify we have the expected number of arguments and results
                    if (nargs != 0)
                    {
                        return CreateError($"Expected 0 arguments but got {nargs}");
                    }
                    if (nresults != 0)
                    {
                        return CreateError($"Expected 0 results but got {nresults}");
                    }
                    
                    // Invoke the callback
                    callback();
                    
                    return IntPtr.Zero;
                }
                catch (Exception ex)
                {
                    return CreateError(ex.Message);
                }
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
    
    /// <summary>
    /// Defines a function in the linker with one parameter and a return value.
    /// </summary>
    /// <remarks>Functions defined with this method are store-independent.</remarks>
    /// <param name="module">The module name of the function.</param>
    /// <param name="name">The name of the function.</param>
    /// <param name="callback">The callback for when the function is invoked.</param>
    public void DefineFunction<T, TResult>(string module, string name, Func<T, TResult> callback)
    {
        if (module is null)
        {
            throw new ArgumentNullException(nameof(module));
        }

        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (callback is null)
        {
            throw new ArgumentNullException(nameof(callback));
        }

        unsafe
        {
            Native.WasmtimeComponentFuncCallback func = (data, ctx, args, nargs, results, nresults) =>
            {
                try
                {
                    // Verify we have the expected number of arguments and results
                    if (nargs != 1)
                    {
                        return CreateError($"Expected 1 argument but got {nargs}");
                    }
                    if (nresults != 1)
                    {
                        return CreateError($"Expected 1 result but got {nresults}");
                    }
                    
                    // Convert args[0] to T
                    var argPtr = (ComponentValue*)args;
                    var arg = ComponentValueHelpers.ToValueBox(null!, *argPtr);
                    
                    T typedArg;
                    if (typeof(T) == typeof(int))
                    {
                        typedArg = (T)(object)arg.AsS32();
                    }
                    else if (typeof(T) == typeof(string))
                    {
                        typedArg = (T)(object)arg.AsString()!;
                    }
                    else
                    {
                        throw new NotSupportedException($"Type {typeof(T)} is not yet supported for component host functions");
                    }
                    
                    // Invoke callback with converted argument
                    var result = callback(typedArg);
                    
                    // Convert result to ComponentValue and store in results[0]
                    var resultPtr = (ComponentValue*)results;
                    if (typeof(TResult) == typeof(int))
                    {
                        *resultPtr = new ComponentValue
                        {
                            kind = ComponentValueKind.S32,
                            of = new ComponentValueUnion { s32 = (int)(object)result! }
                        };
                    }
                    else if (typeof(TResult) == typeof(string))
                    {
                        var str = (string)(object)result!;
                        var bytes = Encoding.UTF8.GetBytes(str);
                        var ptr = Marshal.AllocHGlobal(bytes.Length);
                        Marshal.Copy(bytes, 0, ptr, bytes.Length);
                        *resultPtr = new ComponentValue
                        {
                            kind = ComponentValueKind.String,
                            of = new ComponentValueUnion 
                            { 
                                @string = new WasmName 
                                { 
                                    size = (nuint)bytes.Length, 
                                    data = (byte*)ptr 
                                } 
                            }
                        };
                    }
                    else
                    {
                        throw new NotSupportedException($"Type {typeof(TResult)} is not yet supported for component host functions");
                    }
                    
                    return IntPtr.Zero;
                }
                catch (Exception ex)
                {
                    return CreateError(ex.Message);
                }
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
    
    /// <summary>
    /// Defines a function in the linker with two parameters and a return value.
    /// </summary>
    /// <remarks>Functions defined with this method are store-independent.</remarks>
    /// <param name="module">The module name of the function.</param>
    /// <param name="name">The name of the function.</param>
    /// <param name="callback">The callback for when the function is invoked.</param>
    public void DefineFunction<T1, T2, TResult>(string module, string name, Func<T1, T2, TResult> callback)
    {
        if (module is null)
        {
            throw new ArgumentNullException(nameof(module));
        }

        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (callback is null)
        {
            throw new ArgumentNullException(nameof(callback));
        }

        unsafe
        {
            Native.WasmtimeComponentFuncCallback func = (data, ctx, args, nargs, results, nresults) =>
            {
                try
                {
                    // Verify we have the expected number of arguments and results
                    if (nargs != 2)
                    {
                        return CreateError($"Expected 2 arguments but got {nargs}");
                    }
                    if (nresults != 1)
                    {
                        return CreateError($"Expected 1 result but got {nresults}");
                    }
                    
                    // Convert args[0] to T1, args[1] to T2
                    var argPtr = (ComponentValue*)args;
                    var arg1 = ComponentValueHelpers.ToValueBox(null!, argPtr[0]);
                    var arg2 = ComponentValueHelpers.ToValueBox(null!, argPtr[1]);
                    
                    T1 typedArg1;
                    if (typeof(T1) == typeof(int))
                    {
                        typedArg1 = (T1)(object)arg1.AsS32();
                    }
                    else if (typeof(T1) == typeof(string))
                    {
                        typedArg1 = (T1)(object)arg1.AsString()!;
                    }
                    else
                    {
                        throw new NotSupportedException($"Type {typeof(T1)} is not yet supported for component host functions");
                    }
                    
                    T2 typedArg2;
                    if (typeof(T2) == typeof(int))
                    {
                        typedArg2 = (T2)(object)arg2.AsS32();
                    }
                    else if (typeof(T2) == typeof(string))
                    {
                        typedArg2 = (T2)(object)arg2.AsString()!;
                    }
                    else
                    {
                        throw new NotSupportedException($"Type {typeof(T2)} is not yet supported for component host functions");
                    }
                    
                    // Invoke callback with converted arguments
                    var result = callback(typedArg1, typedArg2);
                    
                    // Convert result to ComponentValue and store in results[0]
                    var resultPtr = (ComponentValue*)results;
                    if (typeof(TResult) == typeof(int))
                    {
                        *resultPtr = new ComponentValue
                        {
                            kind = ComponentValueKind.S32,
                            of = new ComponentValueUnion { s32 = (int)(object)result! }
                        };
                    }
                    else if (typeof(TResult) == typeof(string))
                    {
                        var str = (string)(object)result!;
                        var bytes = Encoding.UTF8.GetBytes(str);
                        var ptr = Marshal.AllocHGlobal(bytes.Length);
                        Marshal.Copy(bytes, 0, ptr, bytes.Length);
                        *resultPtr = new ComponentValue
                        {
                            kind = ComponentValueKind.String,
                            of = new ComponentValueUnion 
                            { 
                                @string = new WasmName 
                                { 
                                    size = (nuint)bytes.Length, 
                                    data = (byte*)ptr 
                                } 
                            }
                        };
                    }
                    else
                    {
                        throw new NotSupportedException($"Type {typeof(TResult)} is not yet supported for component host functions");
                    }
                    
                    return IntPtr.Zero;
                }
                catch (Exception ex)
                {
                    return CreateError(ex.Message);
                }
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
    
    private static unsafe IntPtr CreateError(string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        fixed (byte* ptr = bytes)
        {
            return Native.wasmtime_trap_new(ptr, (nuint)bytes.Length);
        }
    }
        
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
        public static extern unsafe IntPtr wasmtime_component_linker_instance_add_instance(Handle linkerInstance, byte* name, nuint nameLen, out IntPtr addedLinkerInstanceHandle);
        
        [DllImport(Engine.LibraryName)]
        public static extern unsafe IntPtr wasmtime_component_linker_instance_add_module(Handle linkerInstance, byte* name, nuint nameLen, Module.Handle module);
        
        [DllImport(Engine.LibraryName)]
        public static extern unsafe IntPtr wasmtime_component_linker_instance_add_func(Handle linkerInstance, byte* name, nuint nameLen, WasmtimeComponentFuncCallback callback, IntPtr hostData, Finalizer finalizer);
        
        [DllImport(Engine.LibraryName)]
        public static extern unsafe IntPtr wasmtime_trap_new(byte* message, nuint messageLen);
    }
        
    private readonly Handle handle;
}