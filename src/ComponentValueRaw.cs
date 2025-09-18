using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Wasmtime
{
    /// <summary>
    /// Represents a raw component value for optimized marshaling.
    /// This struct is used for fast conversion between .NET types and component values
    /// without allocating ComponentValueBox objects.
    /// 
    /// Currently supported types for optimized marshaling:
    /// - Primitive types: bool, s8/u8, s16/u16, s32/u32, s64/u64, f32, f64, char
    /// - Strings
    /// - Lists (arrays)
    /// - Options (nullable value types)
    /// - Results (using the Result struct)
    /// - Tuples (ValueTuple with 2-4 elements)
    /// 
    /// Not yet supported for optimization:
    /// - Records (need type mapping infrastructure)
    /// - Variants (need discriminant handling)
    /// - Enums (Component Model uses strings)
    /// - Flags (Component Model uses string arrays with special semantics)
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal unsafe struct ComponentValueRaw
    {
        [FieldOffset(0)]
        public bool boolean;

        [FieldOffset(0)]
        public sbyte s8;

        [FieldOffset(0)]
        public byte u8;

        [FieldOffset(0)]
        public short s16;

        [FieldOffset(0)]
        public ushort u16;

        [FieldOffset(0)]
        public int s32;

        [FieldOffset(0)]
        public uint u32;

        [FieldOffset(0)]
        public long s64;

        [FieldOffset(0)]
        public ulong u64;

        [FieldOffset(0)]
        public float f32;

        [FieldOffset(0)]
        public double f64;

        [FieldOffset(0)]
        public uint character;

        [FieldOffset(0)]
        public WasmName @string;

        [FieldOffset(0)]
        public ValList list;

        [FieldOffset(0)]
        public ValRecord record;

        [FieldOffset(0)]
        public ValTuple tuple;

        [FieldOffset(0)]
        public ValVariant variant;

        [FieldOffset(0)]
        public WasmName enumeration;

        [FieldOffset(0)]
        public ComponentValue* option;

        [FieldOffset(0)]
        public ValResult result;

        [FieldOffset(0)]
        public ValFlags flags;

        public static IComponentValueRawConverter<T> Converter<T>()
        {
            // Ensure we are on a little endian system
            if (!BitConverter.IsLittleEndian)
            {
                throw new PlatformNotSupportedException("Big endian systems are currently not supported for raw component values.");
            }

            if (typeof(T).IsTupleType())
            {
                var args = typeof(T).GetGenericArguments();
                var converter = (args.Length) switch
                {
                    2 => typeof(ComponentTuple2ValueRawConverter<,>),
                    3 => typeof(ComponentTuple3ValueRawConverter<,,>),
                    4 => typeof(ComponentTuple4ValueRawConverter<,,,>),
                    _ => throw new InvalidOperationException($"Cannot convert tuple with {args.Length} elements"),
                };

                var instance = converter.MakeGenericType(args).GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                return (IComponentValueRawConverter<T>)instance!.GetValue(null)!;
            }

            // Bool
            if (typeof(T) == typeof(bool))
            {
                return (IComponentValueRawConverter<T>)BoolValueRawConverter.Instance;
            }

            // Signed integers
            if (typeof(T) == typeof(sbyte))
            {
                return (IComponentValueRawConverter<T>)S8ValueRawConverter.Instance;
            }

            if (typeof(T) == typeof(short))
            {
                return (IComponentValueRawConverter<T>)S16ValueRawConverter.Instance;
            }

            if (typeof(T) == typeof(int))
            {
                return (IComponentValueRawConverter<T>)S32ValueRawConverter.Instance;
            }

            if (typeof(T) == typeof(long))
            {
                return (IComponentValueRawConverter<T>)S64ValueRawConverter.Instance;
            }

            // Unsigned integers
            if (typeof(T) == typeof(byte))
            {
                return (IComponentValueRawConverter<T>)U8ValueRawConverter.Instance;
            }

            if (typeof(T) == typeof(ushort))
            {
                return (IComponentValueRawConverter<T>)U16ValueRawConverter.Instance;
            }

            if (typeof(T) == typeof(uint))
            {
                return (IComponentValueRawConverter<T>)U32ValueRawConverter.Instance;
            }

            if (typeof(T) == typeof(ulong))
            {
                return (IComponentValueRawConverter<T>)U64ValueRawConverter.Instance;
            }

            // Floating point
            if (typeof(T) == typeof(float))
            {
                return (IComponentValueRawConverter<T>)F32ValueRawConverter.Instance;
            }

            if (typeof(T) == typeof(double))
            {
                return (IComponentValueRawConverter<T>)F64ValueRawConverter.Instance;
            }

            // String
            if (typeof(T) == typeof(string))
            {
                return (IComponentValueRawConverter<T>)StringValueRawConverter.Instance;
            }

            // Char
            if (typeof(T) == typeof(char))
            {
                return (IComponentValueRawConverter<T>)CharValueRawConverter.Instance;
            }

            // Arrays/Lists
            if (typeof(T).IsArray)
            {
                var elementType = typeof(T).GetElementType()!;
                var converterType = typeof(ListValueRawConverter<>).MakeGenericType(elementType);
                var instance = converterType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                return (IComponentValueRawConverter<T>)instance!.GetValue(null)!;
            }

            // Nullable (Option)
            var underlyingType = Nullable.GetUnderlyingType(typeof(T));
            if (underlyingType != null)
            {
                var converterType = typeof(OptionValueRawConverter<>).MakeGenericType(underlyingType);
                var instance = converterType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                return (IComponentValueRawConverter<T>)instance!.GetValue(null)!;
            }

            // Enum types - Component Model represents enums as strings
            if (typeof(T).IsEnum)
            {
                // Check if it's a flags enum
                if (typeof(T).GetCustomAttributes(typeof(FlagsAttribute), false).Length > 0)
                {
                     var converterType = typeof(FlagsValueRawConverter<>).MakeGenericType(typeof(T));
                     var instance = converterType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                    return (IComponentValueRawConverter<T>)instance!.GetValue(null)!;
                }
                else
                {
                    var converterType = typeof(EnumValueRawConverter<>).MakeGenericType(typeof(T));
                    var instance = converterType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                    return (IComponentValueRawConverter<T>)instance!.GetValue(null)!;
                }
            }

            // Result types - Result<TOk, TErr>
            if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Result<,>))
            {
                var args = typeof(T).GetGenericArguments();
                var converterType = typeof(ResultValueRawConverter<,>).MakeGenericType(args);
                var instance = converterType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                return (IComponentValueRawConverter<T>)instance!.GetValue(null)!;
            }

            // Variant types - classes with nested type inheritance
            // Check if T is a class with nested types that inherit from it
            if (typeof(T).IsClass && !typeof(T).IsArray)
            {
                var nestedTypes = typeof(T).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(t => typeof(T).IsAssignableFrom(t))
                    .ToList();

                if (nestedTypes.Any())
                {
                    // This looks like a variant type
                    var converterType = typeof(VariantValueRawConverter<>).MakeGenericType(typeof(T));
                    var instance = converterType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                    return (IComponentValueRawConverter<T>)instance!.GetValue(null)!;
                }
            }

            // Record types - structs or classes with properties
            if (typeof(T).IsClass || (typeof(T).IsValueType && !typeof(T).IsPrimitive))
            {
                // Make sure it has a parameterless constructor
                if (typeof(T).GetConstructor(Type.EmptyTypes) != null || typeof(T).IsValueType)
                {
                    var converterType = typeof(RecordValueRawConverter<>).MakeGenericType(typeof(T));
                    var instance = converterType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                    return (IComponentValueRawConverter<T>)instance!.GetValue(null)!;
                }
            }
            

            throw new InvalidOperationException($"Cannot convert type '{typeof(T).Name}' into a WASM component parameter type");
        }

        /// <summary>
        /// Convert a ComponentValueRaw to a ComponentValue with the specified kind.
        /// This is used during function invocation to convert raw values to the native format.
        /// </summary>
        public ComponentValue ToComponentValue(ComponentValueKind kind)
        {
            var value = default(ComponentValue);
            value.kind = kind;
            
            // We need to properly copy the raw value into the union field
            // Can't use Unsafe.As because ComponentValue has padding between kind and of
            switch (kind)
            {
                case ComponentValueKind.Bool:
                    value.of.boolean = this.boolean;
                    break;
                case ComponentValueKind.S8:
                    value.of.s8 = this.s8;
                    break;
                case ComponentValueKind.U8:
                    value.of.u8 = this.u8;
                    break;
                case ComponentValueKind.S16:
                    value.of.s16 = this.s16;
                    break;
                case ComponentValueKind.U16:
                    value.of.u16 = this.u16;
                    break;
                case ComponentValueKind.S32:
                    value.of.s32 = this.s32;
                    break;
                case ComponentValueKind.U32:
                    value.of.u32 = this.u32;
                    break;
                case ComponentValueKind.S64:
                    value.of.s64 = this.s64;
                    break;
                case ComponentValueKind.U64:
                    value.of.u64 = this.u64;
                    break;
                case ComponentValueKind.F32:
                    value.of.f32 = this.f32;
                    break;
                case ComponentValueKind.F64:
                    value.of.f64 = this.f64;
                    break;
                case ComponentValueKind.Char:
                    value.of.character = this.character;
                    break;
                case ComponentValueKind.String:
                    value.of.@string = this.@string;
                    break;
                case ComponentValueKind.List:
                    value.of.list = this.list;
                    break;
                case ComponentValueKind.Record:
                    value.of.record = this.record;
                    break;
                case ComponentValueKind.Tuple:
                    value.of.tuple = this.tuple;
                    break;
                case ComponentValueKind.Variant:
                    value.of.variant = this.variant;
                    break;
                case ComponentValueKind.Enum:
                    value.of.enumeration = this.enumeration;
                    break;
                case ComponentValueKind.Option:
                    value.of.option = this.option;
                    break;
                case ComponentValueKind.Result:
                    value.of.result = this.result;
                    break;
                case ComponentValueKind.Flags:
                    value.of.flags = this.flags;
                    break;
                default:
                    throw new NotSupportedException($"Component value kind {kind} is not yet supported in optimized path");
            }
            
            return value;
        }

        /// <summary>
        /// Create a ComponentValueRaw from a ComponentValue.
        /// This is used to convert results back to raw format.
        /// </summary>
        public static ComponentValueRaw FromComponentValue(ComponentValue value)
        {
            var raw = default(ComponentValueRaw);
            
            // We need to properly copy the union value into the raw struct
            // Can't use Unsafe.As because of potential alignment/padding issues
            switch (value.kind)
            {
                case ComponentValueKind.Bool:
                    raw.boolean = value.of.boolean;
                    break;
                case ComponentValueKind.S8:
                    raw.s8 = value.of.s8;
                    break;
                case ComponentValueKind.U8:
                    raw.u8 = value.of.u8;
                    break;
                case ComponentValueKind.S16:
                    raw.s16 = value.of.s16;
                    break;
                case ComponentValueKind.U16:
                    raw.u16 = value.of.u16;
                    break;
                case ComponentValueKind.S32:
                    raw.s32 = value.of.s32;
                    break;
                case ComponentValueKind.U32:
                    raw.u32 = value.of.u32;
                    break;
                case ComponentValueKind.S64:
                    raw.s64 = value.of.s64;
                    break;
                case ComponentValueKind.U64:
                    raw.u64 = value.of.u64;
                    break;
                case ComponentValueKind.F32:
                    raw.f32 = value.of.f32;
                    break;
                case ComponentValueKind.F64:
                    raw.f64 = value.of.f64;
                    break;
                case ComponentValueKind.Char:
                    raw.character = value.of.character;
                    break;
                case ComponentValueKind.String:
                    raw.@string = value.of.@string;
                    break;
                case ComponentValueKind.List:
                    raw.list = value.of.list;
                    break;
                case ComponentValueKind.Record:
                    raw.record = value.of.record;
                    break;
                case ComponentValueKind.Tuple:
                    raw.tuple = value.of.tuple;
                    break;
                case ComponentValueKind.Variant:
                    raw.variant = value.of.variant;
                    break;
                case ComponentValueKind.Enum:
                    raw.enumeration = value.of.enumeration;
                    break;
                case ComponentValueKind.Option:
                    raw.option = value.of.option;
                    break;
                case ComponentValueKind.Result:
                    raw.result = value.of.result;
                    break;
                case ComponentValueKind.Flags:
                    raw.flags = value.of.flags;
                    break;
                default:
                    throw new NotSupportedException($"Component value kind {value.kind} is not yet supported in optimized path");
            }
            
            return raw;
        }
    }

    internal interface IComponentValueRawConverter<T>
    {
        T? Unbox(StoreContext storeContext, Store store, in ComponentValueRaw valueRaw, ComponentValueKind kind);
        void Box(StoreContext storeContext, Store store, ref ComponentValueRaw valueRaw, T value);
        ComponentValueKind Kind { get; }
    }

    internal class BoolValueRawConverter : IComponentValueRawConverter<bool>
    {
        public static readonly BoolValueRawConverter Instance = new();
        public ComponentValueKind Kind => ComponentValueKind.Bool;

        private BoolValueRawConverter() { }

        public bool Unbox(StoreContext storeContext, Store store, in ComponentValueRaw valueRaw, ComponentValueKind kind)
        {
            return valueRaw.boolean;
        }

        public void Box(StoreContext storeContext, Store store, ref ComponentValueRaw valueRaw, bool value)
        {
            // Initialize the entire struct to ensure all bytes are set
            valueRaw = default;
            valueRaw.boolean = value;
        }
    }

    internal class S8ValueRawConverter : IComponentValueRawConverter<sbyte>
    {
        public static readonly S8ValueRawConverter Instance = new();
        public ComponentValueKind Kind => ComponentValueKind.S8;

        private S8ValueRawConverter() { }

        public sbyte Unbox(StoreContext storeContext, Store store, in ComponentValueRaw valueRaw, ComponentValueKind kind)
        {
            return valueRaw.s8;
        }

        public void Box(StoreContext storeContext, Store store, ref ComponentValueRaw valueRaw, sbyte value)
        {
            valueRaw = default;
            valueRaw.s8 = value;
        }
    }

    internal class U8ValueRawConverter : IComponentValueRawConverter<byte>
    {
        public static readonly U8ValueRawConverter Instance = new();
        public ComponentValueKind Kind => ComponentValueKind.U8;

        private U8ValueRawConverter() { }

        public byte Unbox(StoreContext storeContext, Store store, in ComponentValueRaw valueRaw, ComponentValueKind kind)
        {
            return valueRaw.u8;
        }

        public void Box(StoreContext storeContext, Store store, ref ComponentValueRaw valueRaw, byte value)
        {
            valueRaw = default;
            valueRaw.u8 = value;
        }
    }

    internal class S16ValueRawConverter : IComponentValueRawConverter<short>
    {
        public static readonly S16ValueRawConverter Instance = new();
        public ComponentValueKind Kind => ComponentValueKind.S16;

        private S16ValueRawConverter() { }

        public short Unbox(StoreContext storeContext, Store store, in ComponentValueRaw valueRaw, ComponentValueKind kind)
        {
            return valueRaw.s16;
        }

        public void Box(StoreContext storeContext, Store store, ref ComponentValueRaw valueRaw, short value)
        {
            valueRaw = default;
            valueRaw.s16 = value;
        }
    }

    internal class U16ValueRawConverter : IComponentValueRawConverter<ushort>
    {
        public static readonly U16ValueRawConverter Instance = new();
        public ComponentValueKind Kind => ComponentValueKind.U16;

        private U16ValueRawConverter() { }

        public ushort Unbox(StoreContext storeContext, Store store, in ComponentValueRaw valueRaw, ComponentValueKind kind)
        {
            return valueRaw.u16;
        }

        public void Box(StoreContext storeContext, Store store, ref ComponentValueRaw valueRaw, ushort value)
        {
            valueRaw = default;
            valueRaw.u16 = value;
        }
    }

    internal class S32ValueRawConverter : IComponentValueRawConverter<int>
    {
        public static readonly S32ValueRawConverter Instance = new();
        public ComponentValueKind Kind => ComponentValueKind.S32;

        private S32ValueRawConverter() { }

        public int Unbox(StoreContext storeContext, Store store, in ComponentValueRaw valueRaw, ComponentValueKind kind)
        {
            return valueRaw.s32;
        }

        public void Box(StoreContext storeContext, Store store, ref ComponentValueRaw valueRaw, int value)
        {
            valueRaw = default;
            valueRaw.s32 = value;
        }
    }

    internal class U32ValueRawConverter : IComponentValueRawConverter<uint>
    {
        public static readonly U32ValueRawConverter Instance = new();
        public ComponentValueKind Kind => ComponentValueKind.U32;

        private U32ValueRawConverter() { }

        public uint Unbox(StoreContext storeContext, Store store, in ComponentValueRaw valueRaw, ComponentValueKind kind)
        {
            return valueRaw.u32;
        }

        public void Box(StoreContext storeContext, Store store, ref ComponentValueRaw valueRaw, uint value)
        {
            valueRaw = default;
            valueRaw.u32 = value;
        }
    }

    internal class S64ValueRawConverter : IComponentValueRawConverter<long>
    {
        public static readonly S64ValueRawConverter Instance = new();
        public ComponentValueKind Kind => ComponentValueKind.S64;

        private S64ValueRawConverter() { }

        public long Unbox(StoreContext storeContext, Store store, in ComponentValueRaw valueRaw, ComponentValueKind kind)
        {
            return valueRaw.s64;
        }

        public void Box(StoreContext storeContext, Store store, ref ComponentValueRaw valueRaw, long value)
        {
            valueRaw = default;
            valueRaw.s64 = value;
        }
    }

    internal class U64ValueRawConverter : IComponentValueRawConverter<ulong>
    {
        public static readonly U64ValueRawConverter Instance = new();
        public ComponentValueKind Kind => ComponentValueKind.U64;

        private U64ValueRawConverter() { }

        public ulong Unbox(StoreContext storeContext, Store store, in ComponentValueRaw valueRaw, ComponentValueKind kind)
        {
            return valueRaw.u64;
        }

        public void Box(StoreContext storeContext, Store store, ref ComponentValueRaw valueRaw, ulong value)
        {
            valueRaw = default;
            valueRaw.u64 = value;
        }
    }

    internal class F32ValueRawConverter : IComponentValueRawConverter<float>
    {
        public static readonly F32ValueRawConverter Instance = new();
        public ComponentValueKind Kind => ComponentValueKind.F32;

        private F32ValueRawConverter() { }

        public float Unbox(StoreContext storeContext, Store store, in ComponentValueRaw valueRaw, ComponentValueKind kind)
        {
            return valueRaw.f32;
        }

        public void Box(StoreContext storeContext, Store store, ref ComponentValueRaw valueRaw, float value)
        {
            valueRaw = default;
            valueRaw.f32 = value;
        }
    }

    internal class F64ValueRawConverter : IComponentValueRawConverter<double>
    {
        public static readonly F64ValueRawConverter Instance = new();
        public ComponentValueKind Kind => ComponentValueKind.F64;

        private F64ValueRawConverter() { }

        public double Unbox(StoreContext storeContext, Store store, in ComponentValueRaw valueRaw, ComponentValueKind kind)
        {
            return valueRaw.f64;
        }

        public void Box(StoreContext storeContext, Store store, ref ComponentValueRaw valueRaw, double value)
        {
            valueRaw = default;
            valueRaw.f64 = value;
        }
    }

    internal class CharValueRawConverter : IComponentValueRawConverter<char>
    {
        public static readonly CharValueRawConverter Instance = new();
        public ComponentValueKind Kind => ComponentValueKind.Char;

        private CharValueRawConverter() { }

        public char Unbox(StoreContext storeContext, Store store, in ComponentValueRaw valueRaw, ComponentValueKind kind)
        {
            // Component model char is a Unicode scalar value (u32)
            return (char)valueRaw.character;
        }

        public void Box(StoreContext storeContext, Store store, ref ComponentValueRaw valueRaw, char value)
        {
            valueRaw = default;
            valueRaw.character = value;
        }
    }

    internal class StringValueRawConverter : IComponentValueRawConverter<string>
    {
        public static readonly StringValueRawConverter Instance = new();
        public ComponentValueKind Kind => ComponentValueKind.String;

        private StringValueRawConverter() { }

        public unsafe string? Unbox(StoreContext storeContext, Store store, in ComponentValueRaw valueRaw, ComponentValueKind kind)
        {
            if (valueRaw.@string.data == null || valueRaw.@string.size == 0)
            {
                return string.Empty;
            }

            // Convert from UTF-8 bytes
            var bytes = new byte[valueRaw.@string.size];
            Marshal.Copy((IntPtr)valueRaw.@string.data, bytes, 0, (int)valueRaw.@string.size);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        public unsafe void Box(StoreContext storeContext, Store store, ref ComponentValueRaw valueRaw, string value)
        {
            valueRaw = default;

            if (string.IsNullOrEmpty(value))
            {
                valueRaw.@string = new WasmName { size = 0, data = null };
                return;
            }

            // Convert to UTF-8 bytes
            var bytes = System.Text.Encoding.UTF8.GetBytes(value);
            var ptr = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);

            valueRaw.@string = new WasmName
            {
                size = (nuint)bytes.Length,
                data = (byte*)ptr
            };
        }
    }

    // Tuple converters for multiple return values
    internal class ComponentTuple2ValueRawConverter<T1, T2> : IComponentValueRawConverter<ValueTuple<T1, T2>>
    {
        public static readonly ComponentTuple2ValueRawConverter<T1, T2> Instance = new();

        private readonly IComponentValueRawConverter<T1> Converter1 = ComponentValueRaw.Converter<T1>();
        private readonly IComponentValueRawConverter<T2> Converter2 = ComponentValueRaw.Converter<T2>();

        public ComponentValueKind Kind => ComponentValueKind.Tuple;

        public (T1, T2) Unbox(StoreContext storeContext, Store store, in ComponentValueRaw valueRaw, ComponentValueKind kind)
        {
            throw new NotSupportedException("Cannot unbox tuple from single ComponentValueRaw");
        }

        public void Box(StoreContext storeContext, Store store, ref ComponentValueRaw valueRaw, (T1, T2) value)
        {
            unsafe
            {
                fixed (ComponentValueRaw* ptr = &valueRaw)
                {
                    ref var a = ref *(ptr + 0);
                    ref var b = ref *(ptr + 1);

                    Converter1.Box(storeContext, store, ref a, value.Item1);
                    Converter2.Box(storeContext, store, ref b, value.Item2);
                }
            }
        }
    }

    internal class ComponentTuple3ValueRawConverter<T1, T2, T3> : IComponentValueRawConverter<ValueTuple<T1, T2, T3>>
    {
        public static readonly ComponentTuple3ValueRawConverter<T1, T2, T3> Instance = new();

        private readonly IComponentValueRawConverter<T1> Converter1 = ComponentValueRaw.Converter<T1>();
        private readonly IComponentValueRawConverter<T2> Converter2 = ComponentValueRaw.Converter<T2>();
        private readonly IComponentValueRawConverter<T3> Converter3 = ComponentValueRaw.Converter<T3>();

        public ComponentValueKind Kind => ComponentValueKind.Tuple;

        public (T1, T2, T3) Unbox(StoreContext storeContext, Store store, in ComponentValueRaw valueRaw, ComponentValueKind kind)
        {
            throw new NotSupportedException("Cannot unbox tuple from single ComponentValueRaw");
        }

        public void Box(StoreContext storeContext, Store store, ref ComponentValueRaw valueRaw, (T1, T2, T3) value)
        {
            unsafe
            {
                fixed (ComponentValueRaw* ptr = &valueRaw)
                {
                    ref var a = ref *(ptr + 0);
                    ref var b = ref *(ptr + 1);
                    ref var c = ref *(ptr + 2);

                    Converter1.Box(storeContext, store, ref a, value.Item1);
                    Converter2.Box(storeContext, store, ref b, value.Item2);
                    Converter3.Box(storeContext, store, ref c, value.Item3);
                }
            }
        }
    }

    internal class ComponentTuple4ValueRawConverter<T1, T2, T3, T4> : IComponentValueRawConverter<ValueTuple<T1, T2, T3, T4>>
    {
        public static readonly ComponentTuple4ValueRawConverter<T1, T2, T3, T4> Instance = new();

        private readonly IComponentValueRawConverter<T1> Converter1 = ComponentValueRaw.Converter<T1>();
        private readonly IComponentValueRawConverter<T2> Converter2 = ComponentValueRaw.Converter<T2>();
        private readonly IComponentValueRawConverter<T3> Converter3 = ComponentValueRaw.Converter<T3>();
        private readonly IComponentValueRawConverter<T4> Converter4 = ComponentValueRaw.Converter<T4>();

        public ComponentValueKind Kind => ComponentValueKind.Tuple;

        public (T1, T2, T3, T4) Unbox(StoreContext storeContext, Store store, in ComponentValueRaw valueRaw, ComponentValueKind kind)
        {
            throw new NotSupportedException("Cannot unbox tuple from single ComponentValueRaw");
        }

        public void Box(StoreContext storeContext, Store store, ref ComponentValueRaw valueRaw, (T1, T2, T3, T4) value)
        {
            unsafe
            {
                fixed (ComponentValueRaw* ptr = &valueRaw)
                {
                    ref var a = ref *(ptr + 0);
                    ref var b = ref *(ptr + 1);
                    ref var c = ref *(ptr + 2);
                    ref var d = ref *(ptr + 3);

                    Converter1.Box(storeContext, store, ref a, value.Item1);
                    Converter2.Box(storeContext, store, ref b, value.Item2);
                    Converter3.Box(storeContext, store, ref c, value.Item3);
                    Converter4.Box(storeContext, store, ref d, value.Item4);
                }
            }
        }
    }

    internal class ListValueRawConverter<TElement> : IComponentValueRawConverter<TElement[]>
    {
        public static readonly ListValueRawConverter<TElement> Instance = new();
        private readonly IComponentValueRawConverter<TElement> ElementConverter = ComponentValueRaw.Converter<TElement>();
        
        public ComponentValueKind Kind => ComponentValueKind.List;

        private ListValueRawConverter() { }

        public unsafe TElement[]? Unbox(StoreContext storeContext, Store store, in ComponentValueRaw valueRaw, ComponentValueKind kind)
        {
            if (valueRaw.list.size == 0)
            {
                return Array.Empty<TElement>();
            }

            if (valueRaw.list.data == null)
            {
                throw new InvalidOperationException("List data pointer is null for non-empty list");
            }

            var array = new TElement[valueRaw.list.size];
            for (int i = 0; i < array.Length; i++)
            {
                var elementValue = valueRaw.list.data[i];
                var elementRaw = ComponentValueRaw.FromComponentValue(elementValue);
                array[i] = ElementConverter.Unbox(storeContext, store, elementRaw, elementValue.kind)!;
            }

            return array;
        }

        public unsafe void Box(StoreContext storeContext, Store store, ref ComponentValueRaw valueRaw, TElement[] value)
        {
            valueRaw = default;

            if (value == null || value.Length == 0)
            {
                // For null arrays, use null data pointer
                valueRaw.list = new ValList { size = 0, data = null };
                return;
            }

            var size = (nuint)value.Length;
            
            // Always allocate memory, even for empty arrays
            var ptr = (ComponentValue*)Marshal.AllocHGlobal((int)(size * (nuint)sizeof(ComponentValue)));

            try
            {
                // Convert each element
                for (int i = 0; i < value.Length; i++)
                {
                    var elementRaw = default(ComponentValueRaw);
                    ElementConverter.Box(storeContext, store, ref elementRaw, value[i]);
                    ptr[i] = elementRaw.ToComponentValue(ElementConverter.Kind);
                }

                valueRaw.list = new ValList
                {
                    size = size,
                    data = ptr
                };
            }
            catch
            {
                // Clean up on error
                Marshal.FreeHGlobal((IntPtr)ptr);
                throw;
            }
        }
    }

    internal class OptionValueRawConverter<T> : IComponentValueRawConverter<T?>
        where T : struct
    {
        public static readonly OptionValueRawConverter<T> Instance = new();
        private readonly IComponentValueRawConverter<T> InnerConverter = ComponentValueRaw.Converter<T>();
        
        public ComponentValueKind Kind => ComponentValueKind.Option;

        private OptionValueRawConverter() { }

        public unsafe T? Unbox(StoreContext storeContext, Store store, in ComponentValueRaw valueRaw, ComponentValueKind kind)
        {
            if (valueRaw.option == null)
            {
                return null;
            }

            var innerValue = *valueRaw.option;
            var innerRaw = ComponentValueRaw.FromComponentValue(innerValue);
            return InnerConverter.Unbox(storeContext, store, innerRaw, innerValue.kind);
        }

        public unsafe void Box(StoreContext storeContext, Store store, ref ComponentValueRaw valueRaw, T? value)
        {
            valueRaw = default;

            if (!value.HasValue)
            {
                valueRaw.option = null;
                return;
            }

            // Allocate space for the inner value
            var ptr = (ComponentValue*)Marshal.AllocHGlobal(sizeof(ComponentValue));

            try
            {
                var innerRaw = default(ComponentValueRaw);
                InnerConverter.Box(storeContext, store, ref innerRaw, value.Value);
                *ptr = innerRaw.ToComponentValue(InnerConverter.Kind);
                valueRaw.option = ptr;
            }
            catch
            {
                Marshal.FreeHGlobal((IntPtr)ptr);
                throw;
            }
        }
    }

    // Enum converter - converts between C# enums and their string representations
    internal class EnumValueRawConverter<T> : IComponentValueRawConverter<T>
        where T : struct, Enum
    {
        public static readonly EnumValueRawConverter<T> Instance = new();

        public ComponentValueKind Kind => ComponentValueKind.Enum;

        private EnumValueRawConverter() { }

        public T Unbox(StoreContext storeContext, Store store, in ComponentValueRaw valueRaw, ComponentValueKind kind)
        {
            // Get the string representation from the component value
            var enumString = StringValueRawConverter.Instance.Unbox(storeContext, store, valueRaw, ComponentValueKind.String);

            if (enumString == null)
            {
                throw new InvalidOperationException($"Enum value cannot be null");
            }

            // Convert the string to the enum value
            // WASM component enums use lowercase, but C# enums typically use PascalCase
            // So we need to do case-insensitive parsing
            if (Enum.TryParse<T>(enumString, ignoreCase: true, out var result))
            {
                return result;
            }

            throw new InvalidOperationException($"Invalid enum value '{enumString}' for type {typeof(T).Name}");
        }

        public void Box(StoreContext storeContext, Store store, ref ComponentValueRaw valueRaw, T value)
        {
            // Convert the enum value to its string representation
            // WASM components expect lowercase enum values
            var enumString = value.ToString().ToLowerInvariant();

            // Use the string converter to box the value
            StringValueRawConverter.Instance.Box(storeContext, store, ref valueRaw, enumString);
        }
    }

    // Flags converter - converts between C# [Flags] enums and their string array representations
    internal class FlagsValueRawConverter<T> : IComponentValueRawConverter<T>
        where T : struct, Enum
    {
        public static readonly FlagsValueRawConverter<T> Instance = new();

        public ComponentValueKind Kind => ComponentValueKind.Flags;

        private FlagsValueRawConverter() { }

        public unsafe T Unbox(StoreContext storeContext, Store store, in ComponentValueRaw valueRaw, ComponentValueKind kind)
        {
            // Extract flag names from the ValFlags structure
            T result = default(T);

            if (valueRaw.flags.size == 0 || valueRaw.flags.data == null)
            {
                return result;
            }

            int currentValue = 0;
            for (nuint i = 0; i < valueRaw.flags.size; i++)
            {
                var namePtr = &valueRaw.flags.data[i];
            
                // Convert WasmName to string
                string flagName = string.Empty;
                if (namePtr->data != null && namePtr->size > 0)
                {
                    var bytes = new byte[namePtr->size];
                    Marshal.Copy((IntPtr)namePtr->data, bytes, 0, (int)namePtr->size);
                    flagName = System.Text.Encoding.UTF8.GetString(bytes);
                }
            
                if (Enum.TryParse<T>(flagName, ignoreCase: true, out var flagValue))
                {
                    int newFlagValue = Convert.ToInt32(flagValue);
                    currentValue |= newFlagValue;
                }
            }

            return (T)Enum.ToObject(typeof(T), currentValue);
        }

        public unsafe void Box(StoreContext storeContext, Store store, ref ComponentValueRaw valueRaw, T value)
        {
            // Convert the flags enum value to its ValFlags representation
            var flagsList = new List<string>();

            // Get all defined values in the enum
            var enumValues = Enum.GetValues(typeof(T));
            int valueAsInt = Convert.ToInt32(value);

            foreach (T enumValue in enumValues)
            {
                int enumValueAsInt = Convert.ToInt32(enumValue);

                // Skip None/0 value and check for non-power-of-two combinations
                if (enumValueAsInt == 0 || !IsSingleBitSet(enumValueAsInt))
                    continue;

                // Check if this flag is set
                if ((valueAsInt & enumValueAsInt) == enumValueAsInt)
                {
                    // Add the lowercase version of the flag name
                    flagsList.Add(enumValue.ToString().ToLowerInvariant());
                }
            }

            // Allocate memory for flag names
            var flagCount = flagsList.Count;
            WasmName* flagsData = null;
            
            if (flagCount > 0)
            {
                flagsData = (WasmName*)Marshal.AllocHGlobal(sizeof(WasmName) * flagCount);
                
                for (int i = 0; i < flagCount; i++)
                {
                    var flagBytes = System.Text.Encoding.UTF8.GetBytes(flagsList[i]);
                    var flagPtr = Marshal.AllocHGlobal(flagBytes.Length);
                    Marshal.Copy(flagBytes, 0, flagPtr, flagBytes.Length);
                
                    flagsData[i] = new WasmName
                    {
                        data = (byte*)flagPtr,
                        size = (nuint)flagBytes.Length
                    };
                }
            }

            valueRaw = default;
            valueRaw.flags = new ValFlags
            {
                size = (nuint)flagCount,
                data = flagsData
            };
        }

        private static bool IsSingleBitSet(int value)
        {
            return value != 0 && (value & (value - 1)) == 0;
        }
    }

    // Helper interfaces and classes for record conversion
    internal interface IPropertyConverter
    {
        ComponentValueKind Kind { get; }
        object Unbox(StoreContext storeContext, Store store, in ComponentValueRaw valueRaw, ComponentValueKind kind);
        void Box(StoreContext storeContext, Store store, ref ComponentValueRaw valueRaw, object? value);
    }

    internal class PropertyConverter<TProp> : IPropertyConverter
    {
        private readonly IComponentValueRawConverter<TProp> _converter;
        public ComponentValueKind Kind => _converter.Kind;

        public PropertyConverter(IComponentValueRawConverter<TProp> converter)
        {
            _converter = converter;
        }

        public object Unbox(StoreContext storeContext, Store store, in ComponentValueRaw valueRaw, ComponentValueKind kind)
        {
            return _converter.Unbox(storeContext, store, valueRaw, kind)!;
        }

        public void Box(StoreContext storeContext, Store store, ref ComponentValueRaw valueRaw, object? value)
        {
            _converter.Box(storeContext, store, ref valueRaw, (TProp)value!);
        }
    }

    // Record converter - converts between C# structs/classes and WASM records
    // This implementation requires the C# type to have properties that match the record field names
    internal class RecordValueRawConverter<T> : IComponentValueRawConverter<T>
        where T : new()
    {
        private static readonly PropertyInfo[] Properties;
        private static readonly Dictionary<string, PropertyInfo> PropertyLookup;
        private static readonly Dictionary<PropertyInfo, IPropertyConverter> PropertyConverters;

        static RecordValueRawConverter()
        {
            Properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            PropertyLookup = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
            PropertyConverters = new Dictionary<PropertyInfo, IPropertyConverter>();

            foreach (var prop in Properties)
            {
                PropertyLookup[prop.Name] = prop;

                // Get the converter for each property type
                var converterType = typeof(ComponentValueRaw);
                var converterMethod = converterType.GetMethod("Converter", BindingFlags.Public | BindingFlags.Static)!
                    .MakeGenericMethod(prop.PropertyType);
                var converter = converterMethod.Invoke(null, null)!;

                // Wrap the converter
                var wrapperType = typeof(PropertyConverter<>).MakeGenericType(prop.PropertyType);
                var wrapper = Activator.CreateInstance(wrapperType, converter)!;
                PropertyConverters[prop] = (IPropertyConverter)wrapper;
            }
        }

        public static readonly RecordValueRawConverter<T> Instance = new();
        public ComponentValueKind Kind => ComponentValueKind.Record;

        private RecordValueRawConverter() { }

        public unsafe T Unbox(StoreContext storeContext, Store store, in ComponentValueRaw valueRaw, ComponentValueKind kind)
        {
            // Box the result if it's a value type, so SetValue works correctly
            object result = new T();
            var recordVal = valueRaw.record;

            for (nuint i = 0; i < recordVal.size; i++)
            {
                var entry = recordVal.data[i];

                // Convert WasmName to string
                string fieldName = string.Empty;
                if (entry.name.data != null && entry.name.size > 0)
                {
                    var bytes = new byte[entry.name.size];
                    Marshal.Copy((IntPtr)entry.name.data, bytes, 0, (int)entry.name.size);
                    fieldName = System.Text.Encoding.UTF8.GetString(bytes);
                }

                if (PropertyLookup.TryGetValue(fieldName, out var property))
                {
                    var converter = PropertyConverters[property];
                    var innerRaw = ComponentValueRaw.FromComponentValue(entry.val);
                    var value = converter.Unbox(storeContext, store, innerRaw, entry.val.kind);
                    property.SetValue(result, value);
                }
            }

            return (T)result;
        }

        public unsafe void Box(StoreContext storeContext, Store store, ref ComponentValueRaw valueRaw, T value)
        {
            valueRaw = default;

            // Allocate memory for record entries
            var entries = (ValRecordEntry*)Marshal.AllocHGlobal(sizeof(ValRecordEntry) * Properties.Length);

            try
            {
                for (int i = 0; i < Properties.Length; i++)
                {
                    var property = Properties[i];
                    var propValue = property.GetValue(value);
                    var converter = PropertyConverters[property];

                    // Box the property value
                    var innerRaw = default(ComponentValueRaw);
                    converter.Box(storeContext, store, ref innerRaw, propValue);

                    // Create the field name
                    var fieldName = property.Name.ToLowerInvariant();
                    var nameBytes = System.Text.Encoding.UTF8.GetBytes(fieldName);
                    var namePtr = (byte*)Marshal.AllocHGlobal(nameBytes.Length);
                    Marshal.Copy(nameBytes, 0, (IntPtr)namePtr, nameBytes.Length);

                    entries[i] = new ValRecordEntry
                    {
                        name = new WasmName
                        {
                            size = (nuint)nameBytes.Length,
                            data = namePtr
                        },
                        val = innerRaw.ToComponentValue(converter.Kind)
                    };
                }

                valueRaw.record = new ValRecord
                {
                    size = (nuint)Properties.Length,
                    data = entries
                };
            }
            catch
            {
                // Clean up allocated memory on error
                for (int i = 0; i < Properties.Length; i++)
                {
                    if (entries[i].name.data != null)
                    {
                        Marshal.FreeHGlobal((IntPtr)entries[i].name.data);
                    }
                }
                Marshal.FreeHGlobal((IntPtr)entries);
                throw;
            }
        }
    }

    // Variant converter - converts between C# variant types and WASM variants
    // This implementation requires the C# type to follow specific patterns for variant representation
    internal class VariantValueRawConverter<T> : IComponentValueRawConverter<T>
        where T : class
    {
        private static readonly Dictionary<string, Type> DiscriminantToType = new();
        private static readonly Dictionary<Type, string> TypeToDiscriminant = new();
        private static readonly Dictionary<Type, PropertyInfo?> PayloadProperties = new();

        static VariantValueRawConverter()
        {
            // Analyze the type hierarchy to understand the variant structure
            var baseType = typeof(T);

            // Find all nested types that inherit from T
            var nestedTypes = baseType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .Where(t => baseType.IsAssignableFrom(t))
                .ToList();

            foreach (var nestedType in nestedTypes)
            {
                // The discriminant name is the lowercase version of the type name
                var discriminant = nestedType.Name.ToLowerInvariant();
                DiscriminantToType[discriminant] = nestedType;
                TypeToDiscriminant[nestedType] = discriminant;

                // Find the payload property if any
                // We look for the first public property that's not static
                var payloadProp = nestedType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault();
                PayloadProperties[nestedType] = payloadProp;
            }
        }

        public static readonly VariantValueRawConverter<T> Instance = new();
        public ComponentValueKind Kind => ComponentValueKind.Variant;

        private VariantValueRawConverter() { }

        public unsafe T Unbox(StoreContext storeContext, Store store, in ComponentValueRaw valueRaw, ComponentValueKind kind)
        {
            var variantVal = valueRaw.variant;

            // Convert discriminant to string
            string discriminant = string.Empty;
            if (variantVal.discriminant.data != null && variantVal.discriminant.size > 0)
            {
                var bytes = new byte[variantVal.discriminant.size];
                Marshal.Copy((IntPtr)variantVal.discriminant.data, bytes, 0, (int)variantVal.discriminant.size);
                discriminant = System.Text.Encoding.UTF8.GetString(bytes);
            }

            if (!DiscriminantToType.TryGetValue(discriminant, out var targetType))
            {
                throw new InvalidOperationException($"Unknown variant discriminant: {discriminant}");
            }

            object? result;

            // Check if this variant case has a payload
            var payloadProp = PayloadProperties[targetType];
            if (payloadProp != null && variantVal.value != null)
            {
                // Get the payload value
                var innerValue = *variantVal.value;
                var innerRaw = ComponentValueRaw.FromComponentValue(innerValue);

                // Get converter for the payload type
                var converterType = typeof(ComponentValueRaw);
                var converterMethod = converterType.GetMethod("Converter", BindingFlags.Public | BindingFlags.Static)!
                    .MakeGenericMethod(payloadProp.PropertyType);
                var converter = converterMethod.Invoke(null, null)!;

                // Create wrapper to handle the conversion
                var wrapperType = typeof(PropertyConverter<>).MakeGenericType(payloadProp.PropertyType);
                var wrapper = Activator.CreateInstance(wrapperType, converter) as IPropertyConverter;
                var payloadValue = wrapper!.Unbox(storeContext, store, innerRaw, innerValue.kind);

                // Create instance with payload
                var ctor = targetType.GetConstructor(new[] { payloadProp.PropertyType });
                if (ctor != null)
                {
                    result = ctor.Invoke(new[] { payloadValue });
                }
                else
                {
                    throw new InvalidOperationException($"No constructor found for variant case {targetType.Name} with payload type {payloadProp.PropertyType.Name}");
                }
            }
            else
            {
                // No payload - look for singleton instance or parameterless constructor
                var instanceField = targetType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceField != null)
                {
                    result = instanceField.GetValue(null);
                }
                else
                {
                    var ctor = targetType.GetConstructor(Type.EmptyTypes);
                    if (ctor != null)
                    {
                        result = ctor.Invoke(null);
                    }
                    else
                    {
                        throw new InvalidOperationException($"No parameterless constructor or Instance field found for variant case {targetType.Name}");
                    }
                }
            }

            return (T)result!;
        }

        public unsafe void Box(StoreContext storeContext, Store store, ref ComponentValueRaw valueRaw, T value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            valueRaw = default;

            var actualType = value.GetType();
            if (!TypeToDiscriminant.TryGetValue(actualType, out var discriminant))
            {
                throw new InvalidOperationException($"Unknown variant type: {actualType.Name}");
            }

            // Create the discriminant
            var discriminantBytes = System.Text.Encoding.UTF8.GetBytes(discriminant);
            var discriminantPtr = (byte*)Marshal.AllocHGlobal(discriminantBytes.Length);
            Marshal.Copy(discriminantBytes, 0, (IntPtr)discriminantPtr, discriminantBytes.Length);

            ComponentValue* payloadPtr = null;

            // Check if this variant has a payload
            var payloadProp = PayloadProperties[actualType];
            if (payloadProp != null)
            {
                var payloadValue = payloadProp.GetValue(value);
                if (payloadValue != null)
                {
                    // Allocate space for the payload
                    payloadPtr = (ComponentValue*)Marshal.AllocHGlobal(sizeof(ComponentValue));

                    // Get converter for the payload type
                    var converterType = typeof(ComponentValueRaw);
                    var converterMethod = converterType.GetMethod("Converter", BindingFlags.Public | BindingFlags.Static)!
                        .MakeGenericMethod(payloadProp.PropertyType);
                    var converter = converterMethod.Invoke(null, null)!;

                    // Create wrapper to handle the conversion
                    var wrapperType = typeof(PropertyConverter<>).MakeGenericType(payloadProp.PropertyType);
                    var wrapper = Activator.CreateInstance(wrapperType, converter) as IPropertyConverter;

                    var innerRaw = default(ComponentValueRaw);
                    wrapper!.Box(storeContext, store, ref innerRaw, payloadValue);
                    *payloadPtr = innerRaw.ToComponentValue(wrapper.Kind);
                }
            }

            valueRaw.variant = new ValVariant
            {
                discriminant = new WasmName
                {
                    size = (nuint)discriminantBytes.Length,
                    data = discriminantPtr
                },
                value = payloadPtr
            };
        }
    }

    // Result type converter - basic implementation
    internal class ResultValueRawConverter<TOk, TErr> : IComponentValueRawConverter<Result<TOk, TErr>>
    {
        public static readonly ResultValueRawConverter<TOk, TErr> Instance = new();
        private readonly IComponentValueRawConverter<TOk> OkConverter = ComponentValueRaw.Converter<TOk>();
        private readonly IComponentValueRawConverter<TErr> ErrConverter = ComponentValueRaw.Converter<TErr>();
        
        public ComponentValueKind Kind => ComponentValueKind.Result;

        private ResultValueRawConverter() { }

        public unsafe Result<TOk, TErr> Unbox(StoreContext storeContext, Store store, in ComponentValueRaw valueRaw, ComponentValueKind kind)
        {
            if (valueRaw.result.value == null)
            {
                throw new InvalidOperationException("Result value pointer is null");
            }

            var innerValue = *valueRaw.result.value;
            var innerRaw = ComponentValueRaw.FromComponentValue(innerValue);

            if (valueRaw.result.isOk)
            {
                var okValue = OkConverter.Unbox(storeContext, store, innerRaw, innerValue.kind);
                return Result<TOk, TErr>.Ok(okValue!);
            }
            else
            {
                var errValue = ErrConverter.Unbox(storeContext, store, innerRaw, innerValue.kind);
                return Result<TOk, TErr>.Err(errValue!);
            }
        }

        public unsafe void Box(StoreContext storeContext, Store store, ref ComponentValueRaw valueRaw, Result<TOk, TErr> value)
        {
            valueRaw = default;

            // Allocate space for the inner value
            var ptr = (ComponentValue*)Marshal.AllocHGlobal(sizeof(ComponentValue));

            try
            {
                var innerRaw = default(ComponentValueRaw);
                
                if (value.IsOk)
                {
                    OkConverter.Box(storeContext, store, ref innerRaw, value.OkValue!);
                    *ptr = innerRaw.ToComponentValue(OkConverter.Kind);
                    valueRaw.result = new ValResult
                    {
                        isOk = true,
                        value = ptr
                    };
                }
                else
                {
                    ErrConverter.Box(storeContext, store, ref innerRaw, value.ErrValue!);
                    *ptr = innerRaw.ToComponentValue(ErrConverter.Kind);
                    valueRaw.result = new ValResult
                    {
                        isOk = false,
                        value = ptr
                    };
                }
            }
            catch
            {
                Marshal.FreeHGlobal((IntPtr)ptr);
                throw;
            }
        }
    }

    // Simple Result type to use with the converter
    public readonly struct Result<TOk, TErr>
    {
        private readonly bool _isOk;
        private readonly TOk? _okValue;
        private readonly TErr? _errValue;

        private Result(bool isOk, TOk? okValue, TErr? errValue)
        {
            _isOk = isOk;
            _okValue = okValue;
            _errValue = errValue;
        }

        public bool IsOk => _isOk;
        public TOk? OkValue => _okValue;
        public TErr? ErrValue => _errValue;

        public static Result<TOk, TErr> Ok(TOk value) => new(true, value, default);
        public static Result<TOk, TErr> Err(TErr value) => new(false, default, value);
    }
}