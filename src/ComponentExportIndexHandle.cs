using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;

namespace Wasmtime;

/// <summary>
/// Handle to a value which represents a known export of a component
/// </summary>
public class ComponentExportIndexHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>
    /// Create a new handle from a raw handle.
    /// </summary>
    /// <param name="handle"></param>
    public ComponentExportIndexHandle(IntPtr handle)
        : base(true)
    {
        SetHandle(handle);
    }

    /// <summary>
    /// Delete the resource represented by this handle.
    /// </summary>
    protected override bool ReleaseHandle()
    {
        Native.wasmtime_component_export_index_delete(handle);
        return true;
    }
    
    internal static class Native
    {
        [DllImport(Engine.LibraryName)]
        public static extern void wasmtime_component_export_index_delete(IntPtr exportIndex);
    }
}