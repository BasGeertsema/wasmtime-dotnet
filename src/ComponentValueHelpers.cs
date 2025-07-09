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
            // Initialize the entire structure to zero first
            var value = default(ComponentValue);
            value.kind = box.Kind;

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

                case ComponentValueKind.List:
                    if (box.ObjectValue is Array array)
                    {
                        var elementCount = array.Length;
                        ComponentValue* componentValues = null;
                        
                        if (elementCount > 0)
                        {
                            componentValues = (ComponentValue*)Marshal.AllocHGlobal(elementCount * sizeof(ComponentValue));
                            
                            for (int i = 0; i < elementCount; i++)
                            {
                                var element = array.GetValue(i);
                                ComponentValueBox elementBox = element switch
                                {
                                    bool b => b,
                                    sbyte s8 => s8,
                                    byte u8 => u8,
                                    short s16 => s16,
                                    ushort u16 => u16,
                                    int s32 => s32,
                                    uint u32 => u32,
                                    long s64 => s64,
                                    ulong u64 => u64,
                                    float f32 => f32,
                                    double f64 => f64,
                                    char c => c,
                                    string s => s,
                                    _ => throw new NotSupportedException($"Unsupported list element type: {element?.GetType()}")
                                };
                                componentValues[i] = FromValueBox(store, elementBox);
                            }
                        }
                        
                        value.of.list = new ValList
                        {
                            size = (nuint)elementCount,
                            data = componentValues
                        };
                    }
                    break;

                case ComponentValueKind.Tuple:
                    if (box.ObjectValue is object[] tupleElements)
                    {
                        var elementCount = tupleElements.Length;
                        ComponentValue* componentValues = null;
                        
                        if (elementCount > 0)
                        {
                            componentValues = (ComponentValue*)Marshal.AllocHGlobal(elementCount * sizeof(ComponentValue));
                            
                            for (int i = 0; i < elementCount; i++)
                            {
                                var element = tupleElements[i];
                                ComponentValueBox elementBox = element switch
                                {
                                    bool b => b,
                                    sbyte s8 => s8,
                                    byte u8 => u8,
                                    short s16 => s16,
                                    ushort u16 => u16,
                                    int s32 => s32,
                                    uint u32 => u32,
                                    long s64 => s64,
                                    ulong u64 => u64,
                                    float f32 => f32,
                                    double f64 => f64,
                                    char c => c,
                                    string s => s,
                                    object[] nested => ComponentValueBox.FromTuple(nested),
                                    (string, ComponentValueBox)[] record => ComponentValueBox.FromRecord(record),
                                    _ => throw new NotSupportedException($"Unsupported tuple element type: {element?.GetType()}")
                                };
                                componentValues[i] = FromValueBox(store, elementBox);
                            }
                        }
                        
                        value.of.tuple = new ValTuple
                        {
                            size = (nuint)elementCount,
                            data = componentValues
                        };
                    }
                    break;

                case ComponentValueKind.Record:
                    if (box.ObjectValue is (string, ComponentValueBox)[] recordFields)
                    {
                        var fieldCount = recordFields.Length;
                        ValRecordEntry* recordEntries = null;
                        
                        if (fieldCount > 0)
                        {
                            recordEntries = (ValRecordEntry*)Marshal.AllocHGlobal(fieldCount * sizeof(ValRecordEntry));
                            
                            for (int i = 0; i < fieldCount; i++)
                            {
                                var (name, fieldValue) = recordFields[i];
                                
                                // Allocate and copy field name
                                var nameBytes = Encoding.UTF8.GetBytes(name);
                                var namePtr = Marshal.AllocHGlobal(nameBytes.Length);
                                Marshal.Copy(nameBytes, 0, namePtr, nameBytes.Length);
                                
                                recordEntries[i].name = new WasmName
                                {
                                    size = (nuint)nameBytes.Length,
                                    data = (byte*)namePtr
                                };
                                
                                // Convert field value
                                recordEntries[i].val = FromValueBox(store, fieldValue);
                            }
                        }
                        
                        value.of.record = new ValRecord
                        {
                            size = (nuint)fieldCount,
                            data = recordEntries
                        };
                    }
                    break;

                // TODO: Implement complex types (Record, Variant, Enum, Option, Result, Flags)
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

                case ComponentValueKind.List:
                    if (value.of.list.size > 0 && value.of.list.data != null)
                    {
                        // First pass: determine the element type from the first element
                        var firstElement = value.of.list.data[0];
                        var elementType = firstElement.kind switch
                        {
                            ComponentValueKind.Bool => typeof(bool),
                            ComponentValueKind.S8 => typeof(sbyte),
                            ComponentValueKind.U8 => typeof(byte),
                            ComponentValueKind.S16 => typeof(short),
                            ComponentValueKind.U16 => typeof(ushort),
                            ComponentValueKind.S32 => typeof(int),
                            ComponentValueKind.U32 => typeof(uint),
                            ComponentValueKind.S64 => typeof(long),
                            ComponentValueKind.U64 => typeof(ulong),
                            ComponentValueKind.F32 => typeof(float),
                            ComponentValueKind.F64 => typeof(double),
                            ComponentValueKind.Char => typeof(char),
                            ComponentValueKind.String => typeof(string),
                            _ => throw new NotSupportedException($"Unsupported list element kind: {firstElement.kind}")
                        };

                        // Create the typed array
                        var array = Array.CreateInstance(elementType, (int)value.of.list.size);
                        
                        // Convert each element
                        for (int i = 0; i < (int)value.of.list.size; i++)
                        {
                            var elementBox = ToValueBox(store, value.of.list.data[i]);
                            var elementValue = elementBox.Kind switch
                            {
                                ComponentValueKind.Bool => (object)elementBox.AsBool(),
                                ComponentValueKind.S8 => elementBox.AsS8(),
                                ComponentValueKind.U8 => elementBox.AsU8(),
                                ComponentValueKind.S16 => elementBox.AsS16(),
                                ComponentValueKind.U16 => elementBox.AsU16(),
                                ComponentValueKind.S32 => elementBox.AsS32(),
                                ComponentValueKind.U32 => elementBox.AsU32(),
                                ComponentValueKind.S64 => elementBox.AsS64(),
                                ComponentValueKind.U64 => elementBox.AsU64(),
                                ComponentValueKind.F32 => elementBox.AsF32(),
                                ComponentValueKind.F64 => elementBox.AsF64(),
                                ComponentValueKind.Char => elementBox.AsChar(),
                                ComponentValueKind.String => elementBox.AsString(),
                                _ => throw new NotSupportedException($"Unsupported list element kind: {elementBox.Kind}")
                            };
                            array.SetValue(elementValue, i);
                        }
                        
                        // Use reflection to call the generic FromList method with the correct type
                        var fromListMethod = typeof(ComponentValueBox).GetMethod(nameof(ComponentValueBox.FromList))!;
                        var genericMethod = fromListMethod.MakeGenericMethod(elementType);
                        return (ComponentValueBox)genericMethod.Invoke(null, new object[] { array })!;
                    }
                    // Return an empty int array for empty lists (default for s32 list)
                    return ComponentValueBox.FromList(Array.Empty<int>());

                case ComponentValueKind.Tuple:
                    if (value.of.tuple.size > 0 && value.of.tuple.data != null)
                    {
                        var tupleElements = new object[(int)value.of.tuple.size];
                        
                        for (int i = 0; i < (int)value.of.tuple.size; i++)
                        {
                            var elementBox = ToValueBox(store, value.of.tuple.data[i]);
                            var elementValue = elementBox.Kind switch
                            {
                                ComponentValueKind.Bool => (object)elementBox.AsBool(),
                                ComponentValueKind.S8 => elementBox.AsS8(),
                                ComponentValueKind.U8 => elementBox.AsU8(),
                                ComponentValueKind.S16 => elementBox.AsS16(),
                                ComponentValueKind.U16 => elementBox.AsU16(),
                                ComponentValueKind.S32 => elementBox.AsS32(),
                                ComponentValueKind.U32 => elementBox.AsU32(),
                                ComponentValueKind.S64 => elementBox.AsS64(),
                                ComponentValueKind.U64 => elementBox.AsU64(),
                                ComponentValueKind.F32 => elementBox.AsF32(),
                                ComponentValueKind.F64 => elementBox.AsF64(),
                                ComponentValueKind.Char => elementBox.AsChar(),
                                ComponentValueKind.String => elementBox.AsString(),
                                ComponentValueKind.List => elementBox.ObjectValue, // Keep as array
                                ComponentValueKind.Tuple => elementBox.AsTuple(), // Nested tuple
                                ComponentValueKind.Record => elementBox.AsRecord(), // Nested record
                                _ => throw new NotSupportedException($"Unsupported tuple element kind: {elementBox.Kind}")
                            };
                            tupleElements[i] = elementValue!;
                        }
                        
                        return ComponentValueBox.FromTuple(tupleElements);
                    }
                    // Return an empty tuple
                    return ComponentValueBox.FromTuple(Array.Empty<object>());

                case ComponentValueKind.Record:
                    if (value.of.record.size > 0 && value.of.record.data != null)
                    {
                        var recordFields = new (string, ComponentValueBox)[(int)value.of.record.size];
                        
                        for (int i = 0; i < (int)value.of.record.size; i++)
                        {
                            var entry = value.of.record.data[i];
                            
                            // Convert field name from WasmName
                            string fieldName = string.Empty;
                            if (entry.name.data != null && entry.name.size > 0)
                            {
                                byte[] nameBytes = new byte[entry.name.size];
                                Marshal.Copy((IntPtr)entry.name.data, nameBytes, 0, (int)entry.name.size);
                                fieldName = Encoding.UTF8.GetString(nameBytes);
                            }
                            
                            // Convert field value
                            var fieldValue = ToValueBox(store, entry.val);
                            
                            recordFields[i] = (fieldName, fieldValue);
                        }
                        
                        return ComponentValueBox.FromRecord(recordFields);
                    }
                    // Return an empty record
                    return ComponentValueBox.FromRecord(Array.Empty<(string, ComponentValueBox)>());

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

            // Free allocated lists
            if (value->kind == ComponentValueKind.List && value->of.list.data != null)
            {
                // First free any nested values in the list (only if size > 0)
                for (nuint i = 0; i < value->of.list.size; i++)
                {
                    ReleaseValue(&value->of.list.data[i]);
                }
                
                // Then free the list array itself
                // Note: We always allocate memory even for empty lists, so always free it
                Marshal.FreeHGlobal((IntPtr)value->of.list.data);
                value->of.list.data = null;
                value->of.list.size = 0;
            }

            // Free allocated tuples
            if (value->kind == ComponentValueKind.Tuple && value->of.tuple.data != null)
            {
                // First free any nested values in the tuple
                for (nuint i = 0; i < value->of.tuple.size; i++)
                {
                    ReleaseValue(&value->of.tuple.data[i]);
                }
                
                // Then free the tuple array itself
                Marshal.FreeHGlobal((IntPtr)value->of.tuple.data);
                value->of.tuple.data = null;
                value->of.tuple.size = 0;
            }

            // Free allocated records
            if (value->kind == ComponentValueKind.Record && value->of.record.data != null)
            {
                // First free any field names and nested values in the record
                for (nuint i = 0; i < value->of.record.size; i++)
                {
                    // Free field name
                    if (value->of.record.data[i].name.data != null)
                    {
                        Marshal.FreeHGlobal((IntPtr)value->of.record.data[i].name.data);
                        value->of.record.data[i].name.data = null;
                        value->of.record.data[i].name.size = 0;
                    }
                    
                    // Free field value
                    ReleaseValue(&value->of.record.data[i].val);
                }
                
                // Then free the record array itself
                Marshal.FreeHGlobal((IntPtr)value->of.record.data);
                value->of.record.data = null;
                value->of.record.size = 0;
            }

            // TODO: Free other complex types (variant, etc.)
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