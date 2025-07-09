using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Wasmtime
{
    /// <summary>
    /// Helper methods for converting between ComponentValueBox and native ComponentValue
    /// </summary>
    internal static class ComponentValueHelpers
    {
        /// <summary>
        /// Convert a ComponentValueBox to a native ComponentValue
        /// </summary>
        public static unsafe ComponentValue FromValueBox(Store store, ComponentValueBox box)
        {
            var value = new ComponentValue
            {
                kind = box.Kind,
                of = new ComponentValueUnion()
            };

            switch (box.Kind)
            {
                case ComponentValueKind.Bool:
                    value.of.boolean = box.Union.boolean;
                    break;

                case ComponentValueKind.S8:
                    value.of.s8 = box.Union.s8;
                    break;

                case ComponentValueKind.U8:
                    value.of.u8 = box.Union.u8;
                    break;

                case ComponentValueKind.S16:
                    value.of.s16 = box.Union.s16;
                    break;

                case ComponentValueKind.U16:
                    value.of.u16 = box.Union.u16;
                    break;

                case ComponentValueKind.S32:
                    value.of.s32 = box.Union.s32;
                    break;

                case ComponentValueKind.U32:
                    value.of.u32 = box.Union.u32;
                    break;

                case ComponentValueKind.S64:
                    value.of.s64 = box.Union.s64;
                    break;

                case ComponentValueKind.U64:
                    value.of.u64 = box.Union.u64;
                    break;

                case ComponentValueKind.F32:
                    value.of.f32 = box.Union.f32;
                    break;

                case ComponentValueKind.F64:
                    value.of.f64 = box.Union.f64;
                    break;

                case ComponentValueKind.Char:
                    value.of.character = box.Union.character;
                    break;

                case ComponentValueKind.String:
                    if (box.ObjectValue is string str)
                    {
                        var bytes = Encoding.UTF8.GetBytes(str);
                        var ptr = Marshal.AllocHGlobal(bytes.Length);
                        Marshal.Copy(bytes, 0, ptr, bytes.Length);
                        value.of.@string = new WasmName
                        {
                            size = (nuint)bytes.Length,
                            data = (byte*)ptr
                        };
                    }
                    break;

                // TODO: Implement complex types (List, Record, Tuple, Variant, Enum, Option, Result, Flags)
                default:
                    throw new NotImplementedException($"Component value kind {box.Kind} is not yet implemented");
            }

            return value;
        }

        /// <summary>
        /// Convert a native ComponentValue to a ComponentValueBox
        /// </summary>
        public static unsafe ComponentValueBox ToValueBox(Store store, ComponentValue value)
        {
            switch (value.kind)
            {
                case ComponentValueKind.Bool:
                    return value.of.boolean;

                case ComponentValueKind.S8:
                    return value.of.s8;

                case ComponentValueKind.U8:
                    return value.of.u8;

                case ComponentValueKind.S16:
                    return value.of.s16;

                case ComponentValueKind.U16:
                    return value.of.u16;

                case ComponentValueKind.S32:
                    return value.of.s32;

                case ComponentValueKind.U32:
                    return value.of.u32;

                case ComponentValueKind.S64:
                    return value.of.s64;

                case ComponentValueKind.U64:
                    return value.of.u64;

                case ComponentValueKind.F32:
                    return value.of.f32;

                case ComponentValueKind.F64:
                    return value.of.f64;

                case ComponentValueKind.Char:
                    return (char)value.of.character;

                case ComponentValueKind.String:
                    if (value.of.@string.data != null && value.of.@string.size > 0)
                    {
                        // Convert to string using the size from wasm_name_t
                        byte[] bytes = new byte[value.of.@string.size];
                        Marshal.Copy((IntPtr)value.of.@string.data, bytes, 0, (int)value.of.@string.size);
                        return Encoding.UTF8.GetString(bytes);
                    }
                    return string.Empty;

                // TODO: Implement complex types
                default:
                    throw new NotImplementedException($"Component value kind {value.kind} is not yet implemented");
            }
        }

        /// <summary>
        /// Free a native ComponentValue
        /// </summary>
        public static unsafe void ReleaseValue(ComponentValue* value)
        {
            if (value == null) return;

            // Free allocated strings
            if (value->kind == ComponentValueKind.String && value->of.@string.data != null)
            {
                Marshal.FreeHGlobal((IntPtr)value->of.@string.data);
                value->of.@string.data = null;
                value->of.@string.size = 0;
            }

            // TODO: Free complex types (lists, records, etc.)
        }

        /// <summary>
        /// Native interop methods
        /// </summary>
        internal static class Native
        {
            [DllImport(Engine.LibraryName)]
            public static extern unsafe ComponentValue* wasmtime_component_val_new();

            [DllImport(Engine.LibraryName)]
            public static extern unsafe void wasmtime_component_val_delete(ComponentValue* value);
        }
    }
}