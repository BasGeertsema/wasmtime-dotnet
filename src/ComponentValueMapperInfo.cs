using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wasmtime;

/// <summary>
/// Get information about the mapping between type T and the corresponding ComponentValue.
/// </summary>
/// <typeparam name="T"></typeparam>
public static class ComponentValueMapperInfo<T>
{
    /// <summary>
    /// Get the ComponentValueKind corresponding with type T.
    /// </summary>
    public static ComponentValueKind MapsTo {
        get
        {
            if (typeof(T) == typeof(bool)) return ComponentValueKind.Bool;
            if (typeof(T) == typeof(sbyte)) return ComponentValueKind.S8;
            if (typeof(T) == typeof(byte)) return ComponentValueKind.U8;
            if (typeof(T) == typeof(short)) return ComponentValueKind.S16;
            if (typeof(T) == typeof(ushort)) return ComponentValueKind.U16;
            if (typeof(T) == typeof(int)) return ComponentValueKind.S32;
            if (typeof(T) == typeof(uint)) return ComponentValueKind.U32;
            if (typeof(T) == typeof(long)) return ComponentValueKind.S64;
            if (typeof(T) == typeof(ulong)) return ComponentValueKind.U64;
            if (typeof(T) == typeof(float)) return ComponentValueKind.F32;
            if (typeof(T) == typeof(double)) return ComponentValueKind.F64;
            if (typeof(T) == typeof(char)) return ComponentValueKind.Char;
            if (typeof(T) == typeof(string)) return ComponentValueKind.String;
            // Check if T implements ICollection<T> for any T
            if (typeof(T).GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>))
                || (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(ICollection<>))) 
                return ComponentValueKind.List;
            if (typeof(T).IsEnum)
            {
                var isFlag = typeof(T).IsDefined(typeof(FlagsAttribute));
                return isFlag ? ComponentValueKind.Flags : ComponentValueKind.Enum;
            }
            
            if (typeof(T).IsTupleType()) return ComponentValueKind.Tuple;
            if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Nullable<>))
                return ComponentValueKind.Option;
            if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Result<,>)) 
                return ComponentValueKind.Result;

            if (typeof(T).IsClass && typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Any()) 
                return ComponentValueKind.Record;
            
            if (typeof(T).IsValueType && typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Any()) 
                return ComponentValueKind.Record;
            
            // TODO: option<> of non-value types such as records
            // TODO: variant, there is not really a default way to represent this in C#

            throw new InvalidOperationException($"Unsupported type: {typeof(T)}");
        }
    }

    private static readonly int ComponentValueSize = Marshal.SizeOf<ComponentValue>();
    private static readonly int ValRecordEntrySize = Marshal.SizeOf<ValRecordEntry>();
    private static readonly ComponentValueKind Kind = MapsTo;
    
    /// <summary>
    /// Get the total size in bytes for fixed allocations.
    /// </summary>
    /// <remarks>
    /// A value of type T is mapped to the ComponentValue union. Some component value types required more memory, such
    /// as strings, records and tuples. These extra allocations are sometimes fixed and be allocated on the stack
    /// with each function call. For example, the names of a record type are fixed and known in advance so we know
    /// exactly how much to allocate. A user-supplied string value, however, is not known in advance so we will
    /// always allocate it on the heap.
    /// 
    /// This functions returns the size in bytes of the fixed allocations. It is then up to the caller to allocate
    /// this on the heap or stack.  
    /// </remarks>
    public static int GetFixedAllocatedSize()
    {
        var determineRecordSize = (ReadOnlySpan<PropertyInfo> properties) =>
        {
            var cnt = properties.Length * ValRecordEntrySize;
                
            foreach (var prop in properties)
            {
                // add size in bytes of the property name
                cnt += System.Text.Encoding.UTF8.GetByteCount(prop.Name);
            }
                
            return cnt;
        };
            
        return MapsTo switch
        {
            // the amount of tuple values is fixed
            ComponentValueKind.Tuple => ComponentValueSize + (typeof(T).GetGenericArguments().Length * ComponentValueSize),
            
            // a record has a fixed number of properties with fixed names
            ComponentValueKind.Record => determineRecordSize(typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)),
            
            // an enum has a maximum string length, take the largest string value
            ComponentValueKind.Enum => System.Text.Encoding.UTF8.GetByteCount(typeof(T).GetEnumNames().OrderByDescending(x => x.Length).First()),
            
            // stores a single component value if Some() if None() then we would allocate a bit on the stack unused but that is worth it
            ComponentValueKind.Result => 1 * ComponentValueSize,

            _ => 0
        };
    }
    
}