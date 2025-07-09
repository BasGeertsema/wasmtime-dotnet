using System;

namespace Wasmtime
{
    /// <summary>
    /// Represents a record entry in a WebAssembly component.
    /// </summary>
    public struct ComponentRecordEntry
    {
        /// <summary>
        /// The name of the record field.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The value of the record field.
        /// </summary>
        public object? Value { get; set; }
    }

    /// <summary>
    /// Allocation free container for a single component value
    /// </summary>
    public readonly struct ComponentValueBox
    {
        internal readonly ComponentValueKind Kind;
        internal readonly ComponentValueUnion Union;
        internal readonly object? ObjectValue;

        internal ComponentValueBox(ComponentValueKind kind, ComponentValueUnion of)
        {
            Kind = kind;
            Union = of;
            ObjectValue = null;
        }

        internal ComponentValueBox(ComponentValueKind kind, object? objectValue)
        {
            Kind = kind;
            Union = default;
            ObjectValue = objectValue;
        }

        /// <summary>
        /// "Unbox" a <see cref="bool"/> value.
        /// </summary>
        public bool AsBool()
        {
            ThrowIfNotOfCorrectKind(ComponentValueKind.Bool);
            return Union.boolean;
        }

        /// <summary>
        /// "Unbox" a <see cref="sbyte"/> value.
        /// </summary>
        public sbyte AsS8()
        {
            ThrowIfNotOfCorrectKind(ComponentValueKind.S8);
            return Union.s8;
        }

        /// <summary>
        /// "Unbox" a <see cref="byte"/> value.
        /// </summary>
        public byte AsU8()
        {
            ThrowIfNotOfCorrectKind(ComponentValueKind.U8);
            return Union.u8;
        }

        /// <summary>
        /// "Unbox" a <see cref="short"/> value.
        /// </summary>
        public short AsS16()
        {
            ThrowIfNotOfCorrectKind(ComponentValueKind.S16);
            return Union.s16;
        }

        /// <summary>
        /// "Unbox" a <see cref="ushort"/> value.
        /// </summary>
        public ushort AsU16()
        {
            ThrowIfNotOfCorrectKind(ComponentValueKind.U16);
            return Union.u16;
        }

        /// <summary>
        /// "Unbox" a <see cref="int"/> value.
        /// </summary>
        public int AsS32()
        {
            ThrowIfNotOfCorrectKind(ComponentValueKind.S32);
            return Union.s32;
        }

        /// <summary>
        /// "Unbox" a <see cref="uint"/> value.
        /// </summary>
        public uint AsU32()
        {
            ThrowIfNotOfCorrectKind(ComponentValueKind.U32);
            return Union.u32;
        }

        /// <summary>
        /// "Unbox" a <see cref="long"/> value.
        /// </summary>
        public long AsS64()
        {
            ThrowIfNotOfCorrectKind(ComponentValueKind.S64);
            return Union.s64;
        }

        /// <summary>
        /// "Unbox" a <see cref="ulong"/> value.
        /// </summary>
        public ulong AsU64()
        {
            ThrowIfNotOfCorrectKind(ComponentValueKind.U64);
            return Union.u64;
        }

        /// <summary>
        /// "Unbox" a <see cref="float"/> value.
        /// </summary>
        public float AsF32()
        {
            ThrowIfNotOfCorrectKind(ComponentValueKind.F32);
            return Union.f32;
        }

        /// <summary>
        /// "Unbox" a <see cref="double"/> value.
        /// </summary>
        public double AsF64()
        {
            ThrowIfNotOfCorrectKind(ComponentValueKind.F64);
            return Union.f64;
        }

        /// <summary>
        /// "Unbox" a <see cref="char"/> value.
        /// </summary>
        public char AsChar()
        {
            ThrowIfNotOfCorrectKind(ComponentValueKind.Char);
            return (char)Union.character;
        }

        /// <summary>
        /// "Unbox" a <see cref="string"/> value.
        /// </summary>
        public string AsString()
        {
            ThrowIfNotOfCorrectKind(ComponentValueKind.String);
            return (string)(ObjectValue ?? string.Empty);
        }

        /// <summary>
        /// "Unbox" a list value.
        /// </summary>
        public T[]? AsList<T>()
        {
            ThrowIfNotOfCorrectKind(ComponentValueKind.List);
            return ObjectValue as T[];
        }

        /// <summary>
        /// "Unbox" as a generic type.
        /// </summary>
        public T? As<T>() where T : class
        {
            return (T?)ObjectValue;
        }

        private void ThrowIfNotOfCorrectKind(ComponentValueKind expectedKind)
        {
            if (Kind != expectedKind)
            {
                throw new InvalidCastException($"Cannot convert from `{Kind}` to `{expectedKind}`");
            }
        }

        /// <summary>
        /// "Box" a bool without any heap allocations
        /// </summary>
        public static implicit operator ComponentValueBox(bool value)
        {
            return new ComponentValueBox(ComponentValueKind.Bool, new ComponentValueUnion { boolean = value });
        }

        /// <summary>
        /// "Box" a sbyte without any heap allocations
        /// </summary>
        public static implicit operator ComponentValueBox(sbyte value)
        {
            return new ComponentValueBox(ComponentValueKind.S8, new ComponentValueUnion { s8 = value });
        }

        /// <summary>
        /// "Box" a byte without any heap allocations
        /// </summary>
        public static implicit operator ComponentValueBox(byte value)
        {
            return new ComponentValueBox(ComponentValueKind.U8, new ComponentValueUnion { u8 = value });
        }

        /// <summary>
        /// "Box" a short without any heap allocations
        /// </summary>
        public static implicit operator ComponentValueBox(short value)
        {
            return new ComponentValueBox(ComponentValueKind.S16, new ComponentValueUnion { s16 = value });
        }

        /// <summary>
        /// "Box" a ushort without any heap allocations
        /// </summary>
        public static implicit operator ComponentValueBox(ushort value)
        {
            return new ComponentValueBox(ComponentValueKind.U16, new ComponentValueUnion { u16 = value });
        }

        /// <summary>
        /// "Box" an int without any heap allocations
        /// </summary>
        public static implicit operator ComponentValueBox(int value)
        {
            return new ComponentValueBox(ComponentValueKind.S32, new ComponentValueUnion { s32 = value });
        }

        /// <summary>
        /// "Box" a uint without any heap allocations
        /// </summary>
        public static implicit operator ComponentValueBox(uint value)
        {
            return new ComponentValueBox(ComponentValueKind.U32, new ComponentValueUnion { u32 = value });
        }

        /// <summary>
        /// "Box" a long without any heap allocations
        /// </summary>
        public static implicit operator ComponentValueBox(long value)
        {
            return new ComponentValueBox(ComponentValueKind.S64, new ComponentValueUnion { s64 = value });
        }

        /// <summary>
        /// "Box" a ulong without any heap allocations
        /// </summary>
        public static implicit operator ComponentValueBox(ulong value)
        {
            return new ComponentValueBox(ComponentValueKind.U64, new ComponentValueUnion { u64 = value });
        }

        /// <summary>
        /// "Box" a float without any heap allocations
        /// </summary>
        public static implicit operator ComponentValueBox(float value)
        {
            return new ComponentValueBox(ComponentValueKind.F32, new ComponentValueUnion { f32 = value });
        }

        /// <summary>
        /// "Box" a double without any heap allocations
        /// </summary>
        public static implicit operator ComponentValueBox(double value)
        {
            return new ComponentValueBox(ComponentValueKind.F64, new ComponentValueUnion { f64 = value });
        }

        /// <summary>
        /// "Box" a char without any heap allocations
        /// </summary>
        public static implicit operator ComponentValueBox(char value)
        {
            return new ComponentValueBox(ComponentValueKind.Char, new ComponentValueUnion { character = (uint)value });
        }

        /// <summary>
        /// "Box" a string
        /// </summary>
        public static implicit operator ComponentValueBox(string value)
        {
            return new ComponentValueBox(ComponentValueKind.String, value);
        }

        /// <summary>
        /// Create a ComponentValueBox from a list of values
        /// </summary>
        public static ComponentValueBox FromList<T>(T[] values)
        {
            return new ComponentValueBox(ComponentValueKind.List, values);
        }

        /// <summary>
        /// "Box" an arbitrary reference type
        /// </summary>
        public static ComponentValueBox AsBox<T>(T? value) where T : class
        {
            if (value is string str)
            {
                return new ComponentValueBox(ComponentValueKind.String, str);
            }

            // For now, treat other reference types as records
            return new ComponentValueBox(ComponentValueKind.Record, value);
        }
    }
}