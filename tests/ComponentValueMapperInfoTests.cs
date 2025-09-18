using System;
using System.Collections;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Wasmtime.Tests
{
    public class ComponentValueMapperInfoTests
    {
        [Fact]
        public void MapsTo_Bool_ReturnsBool()
        {
            ComponentValueMapperInfo<bool>.MapsTo.Should().Be(ComponentValueKind.Bool);
        }

        [Fact]
        public void MapsTo_SByte_ReturnsS8()
        {
            ComponentValueMapperInfo<sbyte>.MapsTo.Should().Be(ComponentValueKind.S8);
        }

        [Fact]
        public void MapsTo_Byte_ReturnsU8()
        {
            ComponentValueMapperInfo<byte>.MapsTo.Should().Be(ComponentValueKind.U8);
        }

        [Fact]
        public void MapsTo_Short_ReturnsS16()
        {
            ComponentValueMapperInfo<short>.MapsTo.Should().Be(ComponentValueKind.S16);
        }

        [Fact]
        public void MapsTo_UShort_ReturnsU16()
        {
            ComponentValueMapperInfo<ushort>.MapsTo.Should().Be(ComponentValueKind.U16);
        }

        [Fact]
        public void MapsTo_Int_ReturnsS32()
        {
            ComponentValueMapperInfo<int>.MapsTo.Should().Be(ComponentValueKind.S32);
        }

        [Fact]
        public void MapsTo_UInt_ReturnsU32()
        {
            ComponentValueMapperInfo<uint>.MapsTo.Should().Be(ComponentValueKind.U32);
        }

        [Fact]
        public void MapsTo_Long_ReturnsS64()
        {
            ComponentValueMapperInfo<long>.MapsTo.Should().Be(ComponentValueKind.S64);
        }

        [Fact]
        public void MapsTo_ULong_ReturnsU64()
        {
            ComponentValueMapperInfo<ulong>.MapsTo.Should().Be(ComponentValueKind.U64);
        }

        [Fact]
        public void MapsTo_Float_ReturnsF32()
        {
            ComponentValueMapperInfo<float>.MapsTo.Should().Be(ComponentValueKind.F32);
        }

        [Fact]
        public void MapsTo_Double_ReturnsF64()
        {
            ComponentValueMapperInfo<double>.MapsTo.Should().Be(ComponentValueKind.F64);
        }

        [Fact]
        public void MapsTo_Char_ReturnsChar()
        {
            ComponentValueMapperInfo<char>.MapsTo.Should().Be(ComponentValueKind.Char);
        }

        [Fact]
        public void MapsTo_String_ReturnsString()
        {
            ComponentValueMapperInfo<string>.MapsTo.Should().Be(ComponentValueKind.String);
        }

        [Fact]
        public void MapsTo_List_ReturnsList()
        {
            // List<T> implements ICollection<T>
            ComponentValueMapperInfo<List<int>>.MapsTo.Should().Be(ComponentValueKind.List);
        }

        [Fact]
        public void MapsTo_Array_ReturnsList()
        {
            // Arrays implement ICollection<T>
            ComponentValueMapperInfo<int[]>.MapsTo.Should().Be(ComponentValueKind.List);
        }

        [Fact]
        public void MapsTo_ICollection_ReturnsList()
        {
            // ICollection<T> interface matches exactly
            ComponentValueMapperInfo<ICollection<string>>.MapsTo.Should().Be(ComponentValueKind.List);
        }

        [Fact]
        public void MapsTo_CustomCollection_ReturnsList()
        {
            // List<int> implements ICollection<int>
            ComponentValueMapperInfo<List<int>>.MapsTo.Should().Be(ComponentValueKind.List);
        }

        [Fact]
        public void MapsTo_ArrayList_ReturnsRecord()
        {
            // ArrayList has public properties like Capacity, Count, etc.
            ComponentValueMapperInfo<ArrayList>.MapsTo.Should().Be(ComponentValueKind.Record);
        }

        private enum SimpleEnum
        {
            Value1,
            Value2
        }

        [Fact]
        public void MapsTo_SimpleEnum_ReturnsEnum()
        {
            ComponentValueMapperInfo<SimpleEnum>.MapsTo.Should().Be(ComponentValueKind.Enum);
        }

        [Flags]
        private enum FlagsEnum
        {
            None = 0,
            Flag1 = 1,
            Flag2 = 2,
            Flag3 = 4
        }

        [Fact]
        public void MapsTo_FlagsEnum_ReturnsFlags()
        {
            ComponentValueMapperInfo<FlagsEnum>.MapsTo.Should().Be(ComponentValueKind.Flags);
        }

        [Fact]
        public void MapsTo_ValueTuple_ReturnsTuple()
        {
            ComponentValueMapperInfo<(int, string)>.MapsTo.Should().Be(ComponentValueKind.Tuple);
        }

        [Fact]
        public void MapsTo_ValueTuple3_ReturnsTuple()
        {
            ComponentValueMapperInfo<(int, string, bool)>.MapsTo.Should().Be(ComponentValueKind.Tuple);
        }

        [Fact]
        public void MapsTo_ValueTuple7_ReturnsTuple()
        {
            ComponentValueMapperInfo<(int, string, bool, double, float, long, byte)>.MapsTo.Should().Be(ComponentValueKind.Tuple);
        }

        [Fact]
        public void MapsTo_NullableInt_ReturnsOption()
        {
            ComponentValueMapperInfo<int?>.MapsTo.Should().Be(ComponentValueKind.Option);
        }

        [Fact]
        public void MapsTo_NullableBool_ReturnsOption()
        {
            ComponentValueMapperInfo<bool?>.MapsTo.Should().Be(ComponentValueKind.Option);
        }

        [Fact]
        public void MapsTo_NullableDouble_ReturnsOption()
        {
            ComponentValueMapperInfo<double?>.MapsTo.Should().Be(ComponentValueKind.Option);
        }

        [Fact]
        public void MapsTo_NullableEnum_ReturnsOption()
        {
            ComponentValueMapperInfo<SimpleEnum?>.MapsTo.Should().Be(ComponentValueKind.Option);
        }

        [Fact]
        public void MapsTo_NullableStruct_ReturnsOption()
        {
            ComponentValueMapperInfo<DateTime?>.MapsTo.Should().Be(ComponentValueKind.Option);
        }

        [Fact]
        public void MapsTo_ResultType_ReturnsResult()
        {
            ComponentValueMapperInfo<Result<int, string>>.MapsTo.Should().Be(ComponentValueKind.Result);
        }

        [Fact]
        public void MapsTo_ResultWithComplexTypes_ReturnsResult()
        {
            ComponentValueMapperInfo<Result<List<string>, Exception>>.MapsTo.Should().Be(ComponentValueKind.Result);
        }

        private struct CustomStruct
        {
            public int Value { get; set; }
        }

        [Fact]
        public void MapsTo_CustomStruct_ReturnsRecord()
        {
            // Structs with public properties map to Record
            ComponentValueMapperInfo<CustomStruct>.MapsTo.Should().Be(ComponentValueKind.Record);
        }

        private struct EmptyStruct
        {
        }

        [Fact]
        public void MapsTo_EmptyStruct_ThrowsInvalidOperationException()
        {
            // Structs without properties are not supported
            Action act = () => { var _ = ComponentValueMapperInfo<EmptyStruct>.MapsTo; };
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("Unsupported type: Wasmtime.Tests.ComponentValueMapperInfoTests+EmptyStruct");
        }

        private class CustomClass
        {
            public string Value { get; set; }
        }

        [Fact]
        public void MapsTo_CustomClass_ReturnsRecord()
        {
            // Classes with public properties map to Record
            ComponentValueMapperInfo<CustomClass>.MapsTo.Should().Be(ComponentValueKind.Record);
        }

        private class EmptyClass
        {
        }

        [Fact]
        public void MapsTo_EmptyClass_ThrowsInvalidOperationException()
        {
            // Classes without properties are not supported
            Action act = () => { var _ = ComponentValueMapperInfo<EmptyClass>.MapsTo; };
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("Unsupported type: Wasmtime.Tests.ComponentValueMapperInfoTests+EmptyClass");
        }

        private record SimpleRecord(int Id, string Name);

        [Fact]
        public void MapsTo_Record_ReturnsRecord()
        {
            // Records map to Record
            ComponentValueMapperInfo<SimpleRecord>.MapsTo.Should().Be(ComponentValueKind.Record);
        }

        private record EmptyRecord();

        [Fact]
        public void MapsTo_EmptyRecord_ThrowsInvalidOperationException()
        {
            // Records without properties are not supported
            Action act = () => { var _ = ComponentValueMapperInfo<EmptyRecord>.MapsTo; };
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("Unsupported type: Wasmtime.Tests.ComponentValueMapperInfoTests+EmptyRecord");
        }

        [Fact]
        public void MapsTo_Decimal_ReturnsRecord()
        {
            // Decimal has a Scale property
            ComponentValueMapperInfo<decimal>.MapsTo.Should().Be(ComponentValueKind.Record);
        }

        [Fact]
        public void MapsTo_DateTime_ReturnsRecord()
        {
            // DateTime has many properties like Year, Month, Day, etc.
            ComponentValueMapperInfo<DateTime>.MapsTo.Should().Be(ComponentValueKind.Record);
        }

        [Fact]
        public void MapsTo_Guid_ThrowsInvalidOperationException()
        {
            Action act = () => { var _ = ComponentValueMapperInfo<Guid>.MapsTo; };
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("Unsupported type: System.Guid");
        }
    }
}