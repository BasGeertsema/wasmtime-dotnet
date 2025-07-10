using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Wasmtime
{
    /// <summary>
    /// Represents a raw component value for optimized marshaling.
    /// This struct is used for fast conversion between .NET types and component values
    /// without allocating ComponentValueBox objects.
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
            value.of = Unsafe.As<ComponentValueRaw, ComponentValueUnion>(ref this);
            return value;
        }

        /// <summary>
        /// Create a ComponentValueRaw from a ComponentValue.
        /// This is used to convert results back to raw format.
        /// </summary>
        public static ComponentValueRaw FromComponentValue(ComponentValue value)
        {
            return Unsafe.As<ComponentValueUnion, ComponentValueRaw>(ref value.of);
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
}