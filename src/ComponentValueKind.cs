using System;
using System.Runtime.InteropServices;

namespace Wasmtime;

public enum ComponentValueKind : byte
{ 
    Bool = 0,
    S8 = 1,
    U8 = 2,
    S16 = 3,
    U16 = 4,
    S32 = 5,
    U32 = 6,
    S64 = 7,
    U64 = 8,
    F32 = 9,
    F64 = 10,
    Char = 11,
    String = 12,
    List = 13,
    Record = 14,
    Tuple = 15,
    Variant = 16,
    Enum = 17,
    Option = 18,
    Result = 19,
    Flags = 20,    
}

/// <summary>
/// Represents a variant type.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct WasmName
{
    public nuint size;
    public unsafe byte* data;
}

[StructLayout(LayoutKind.Sequential)]
internal struct ValVariant
{
    /// <summary>
    /// The discriminant of the variant.
    /// </summary>
    public WasmName discriminant;
    
    /// <summary>
    /// The payload of the variant
    /// </summary>
    public unsafe ComponentValue* value;
};

/// <summary>
/// Represents a result type
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ValResult
{
    /// <summary>
    /// The discriminant of the result.
    /// </summary>
    public bool isOk;
    
    /// <summary>
    /// The 'ok' value if <see cref="isOk"/> is true, else the 'err' value
    /// </summary>
    public unsafe ComponentValue* value;
};

[StructLayout(LayoutKind.Sequential)]
internal struct ValList
{
    public nuint size;
    public unsafe ComponentValue* data;
};

[StructLayout(LayoutKind.Sequential)]
internal struct ValRecord
{
    public nuint size;
    public unsafe ValRecordEntry* data;
};

[StructLayout(LayoutKind.Sequential)]
internal struct ValTuple
{
    public nuint size;
    public unsafe ComponentValue* data;
};

[StructLayout(LayoutKind.Sequential)]
internal struct ValFlags
{
    public nuint size;
    public unsafe WasmName* data;
};

[StructLayout(LayoutKind.Explicit)]
internal unsafe struct ComponentValueUnion
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
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ComponentValue
{
    static ComponentValue()
    {
        var actualSize = Marshal.SizeOf(typeof(ComponentValue));
        // ComponentValue should be 32 bytes on 64-bit platforms:
        // - 1 byte for kind
        // - 7 bytes padding (to align union to 8-byte boundary)
        // - 24 bytes for union (includes WasmName which is 16 bytes: 8 for size_t + 8 for pointer)
        System.Diagnostics.Debug.Assert(actualSize == 32, $"ComponentValue size mismatch: expected 32, got {actualSize}");
    }
    
    public ComponentValueKind kind;
    public ComponentValueUnion of;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ValRecordEntry
{
    /// <summary>
    /// The name of this entry
    /// </summary>
    public WasmName name;

    /// <summary>
    /// The value of this entry
    /// </summary>
    public ComponentValue val;
}

