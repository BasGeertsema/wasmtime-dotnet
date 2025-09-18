using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using FluentAssertions;
using Xunit;

namespace Wasmtime.Tests
{
    public class ComponentValueMapperInfoFixedSizeTests
    {
        // For reference: these sizes should match what's defined in ComponentValueMapperInfo
        private static readonly int ComponentValueSize = Marshal.SizeOf<ComponentValue>();
        private static readonly int ValRecordEntrySize = Marshal.SizeOf<ValRecordEntry>();

        #region Tuple Tests

        [Fact]
        public void GetFixedAllocatedSize_EmptyTuple_ReturnsComponentValueSize()
        {
            // Empty tuple (ValueTuple) still needs one ComponentValue for metadata
            var size = ComponentValueMapperInfo<ValueTuple>.GetFixedAllocatedSize();
            size.Should().Be(ComponentValueSize);
        }

        [Fact]
        public void GetFixedAllocatedSize_SingleElementTuple_ReturnsTwoComponentValues()
        {
            // One for metadata + one for the element
            var size = ComponentValueMapperInfo<ValueTuple<int>>.GetFixedAllocatedSize();
            size.Should().Be(ComponentValueSize + (1 * ComponentValueSize));
        }

        [Fact]
        public void GetFixedAllocatedSize_TwoElementTuple_ReturnsThreeComponentValues()
        {
            // One for metadata + two for elements
            var size = ComponentValueMapperInfo<(int, string)>.GetFixedAllocatedSize();
            size.Should().Be(ComponentValueSize + (2 * ComponentValueSize));
        }

        [Fact]
        public void GetFixedAllocatedSize_SevenElementTuple_ReturnsEightComponentValues()
        {
            // One for metadata + seven for elements
            var size = ComponentValueMapperInfo<(int, string, bool, double, float, long, byte)>.GetFixedAllocatedSize();
            size.Should().Be(ComponentValueSize + (7 * ComponentValueSize));
        }

        [Fact]
        public void GetFixedAllocatedSize_EightElementTuple_ReturnsNineComponentValues()
        {
            // Tuples with 8+ elements use nested tuples internally, but GetGenericArguments still returns all
            var size = ComponentValueMapperInfo<(int, string, bool, double, float, long, byte, short)>.GetFixedAllocatedSize();
            size.Should().Be(ComponentValueSize + (8 * ComponentValueSize));
        }

        #endregion

        #region Record Tests

        private record EmptyRecord();

        [Fact]
        public void GetFixedAllocatedSize_EmptyRecord_ThrowsInvalidOperationException()
        {
            // Empty record has no properties, so it's not supported
            Action act = () => ComponentValueMapperInfo<EmptyRecord>.GetFixedAllocatedSize();
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("Unsupported type: Wasmtime.Tests.ComponentValueMapperInfoFixedSizeTests+EmptyRecord");
        }

        private record SinglePropertyRecord(int Value);

        [Fact]
        public void GetFixedAllocatedSize_SinglePropertyRecord_ReturnsValRecordEntryPlusPropertyNameSize()
        {
            var propertyName = "Value";
            var expectedSize = ValRecordEntrySize + System.Text.Encoding.UTF8.GetByteCount(propertyName);
            
            var size = ComponentValueMapperInfo<SinglePropertyRecord>.GetFixedAllocatedSize();
            size.Should().Be(expectedSize);
        }

        private record MultiPropertyRecord(int Id, string Name, bool IsActive);

        [Fact]
        public void GetFixedAllocatedSize_MultiPropertyRecord_ReturnsCorrectSize()
        {
            var properties = new[] { "Id", "Name", "IsActive" };
            var expectedSize = properties.Length * ValRecordEntrySize;
            foreach (var prop in properties)
            {
                expectedSize += System.Text.Encoding.UTF8.GetByteCount(prop);
            }
            
            var size = ComponentValueMapperInfo<MultiPropertyRecord>.GetFixedAllocatedSize();
            size.Should().Be(expectedSize);
        }

        private record RecordWithUnicodeProperty(string 名前, int 値);

        [Fact]
        public void GetFixedAllocatedSize_RecordWithUnicodeProperties_ReturnsCorrectUtf8Size()
        {
            // UTF-8 encoding of Japanese characters takes more bytes
            var properties = new[] { "名前", "値" };
            var expectedSize = properties.Length * ValRecordEntrySize;
            foreach (var prop in properties)
            {
                expectedSize += System.Text.Encoding.UTF8.GetByteCount(prop);
            }
            
            var size = ComponentValueMapperInfo<RecordWithUnicodeProperty>.GetFixedAllocatedSize();
            size.Should().Be(expectedSize);
        }

        #endregion

        #region Enum Tests

        private enum EmptyEnum { }

        private enum SimpleEnum
        {
            Value1,
            Value2,
            LongerValueName
        }

        [Fact]
        public void GetFixedAllocatedSize_SimpleEnum_ReturnsLongestEnumNameByteCount()
        {
            // Should return the byte count of "LongerValueName"
            var longestName = "LongerValueName";
            var expectedSize = System.Text.Encoding.UTF8.GetByteCount(longestName);
            
            var size = ComponentValueMapperInfo<SimpleEnum>.GetFixedAllocatedSize();
            size.Should().Be(expectedSize);
        }

        private enum EnumWithVeryLongName
        {
            A,
            AB,
            ABC,
            ThisIsAVeryLongEnumValueNameThatShouldBeTheMaximum
        }

        [Fact]
        public void GetFixedAllocatedSize_EnumWithVeryLongName_ReturnsLongestNameByteCount()
        {
            var longestName = "ThisIsAVeryLongEnumValueNameThatShouldBeTheMaximum";
            var expectedSize = System.Text.Encoding.UTF8.GetByteCount(longestName);
            
            var size = ComponentValueMapperInfo<EnumWithVeryLongName>.GetFixedAllocatedSize();
            size.Should().Be(expectedSize);
        }

        [Flags]
        private enum FlagsEnum
        {
            None = 0,
            Flag1 = 1,
            Flag2 = 2,
            VeryLongFlagName = 4
        }

        [Fact]
        public void GetFixedAllocatedSize_FlagsEnum_ReturnsZero()
        {
            // Flags are handled differently and return 0
            var size = ComponentValueMapperInfo<FlagsEnum>.GetFixedAllocatedSize();
            size.Should().Be(0);
        }

        #endregion

        #region Result Tests

        [Fact]
        public void GetFixedAllocatedSize_Result_ReturnsOneComponentValue()
        {
            var size = ComponentValueMapperInfo<Result<int, string>>.GetFixedAllocatedSize();
            size.Should().Be(ComponentValueSize);
        }

        [Fact]
        public void GetFixedAllocatedSize_ResultWithComplexTypes_ReturnsOneComponentValue()
        {
            var size = ComponentValueMapperInfo<Result<List<string>, Exception>>.GetFixedAllocatedSize();
            size.Should().Be(ComponentValueSize);
        }

        #endregion

        #region Option Tests
        
        [Fact]
        public void GetFixedAllocatedSize_Option_ReturnsZero()
        {
            // Options currently return 0 (not implemented in the switch)
            var size = ComponentValueMapperInfo<int?>.GetFixedAllocatedSize();
            size.Should().Be(0);
        }

        #endregion

        #region Other Types Tests

        [Fact]
        public void GetFixedAllocatedSize_PrimitiveTypes_ReturnZero()
        {
            ComponentValueMapperInfo<bool>.GetFixedAllocatedSize().Should().Be(0);
            ComponentValueMapperInfo<int>.GetFixedAllocatedSize().Should().Be(0);
            ComponentValueMapperInfo<double>.GetFixedAllocatedSize().Should().Be(0);
            ComponentValueMapperInfo<string>.GetFixedAllocatedSize().Should().Be(0);
        }

        [Fact]
        public void GetFixedAllocatedSize_ListTypes_ReturnZero()
        {
            ComponentValueMapperInfo<int[]>.GetFixedAllocatedSize().Should().Be(0);
            ComponentValueMapperInfo<List<string>>.GetFixedAllocatedSize().Should().Be(0);
            ComponentValueMapperInfo<ICollection<int>>.GetFixedAllocatedSize().Should().Be(0);
        }

        [Fact]
        public void GetFixedAllocatedSize_NullableTypes_ReturnZero()
        {
            ComponentValueMapperInfo<int?>.GetFixedAllocatedSize().Should().Be(0);
            ComponentValueMapperInfo<bool?>.GetFixedAllocatedSize().Should().Be(0);
            ComponentValueMapperInfo<SimpleEnum?>.GetFixedAllocatedSize().Should().Be(0);
        }

        private struct CustomStruct
        {
            public int Value { get; set; }
        }

        [Fact]
        public void GetFixedAllocatedSize_CustomStruct_ReturnsCorrectSize()
        {
            // CustomStruct has one property "Value"
            var expectedSize = ValRecordEntrySize + System.Text.Encoding.UTF8.GetByteCount("Value");
            var size = ComponentValueMapperInfo<CustomStruct>.GetFixedAllocatedSize();
            size.Should().Be(expectedSize);
        }

        private struct EmptyStructWithoutProperties
        {
        }

        [Fact]
        public void GetFixedAllocatedSize_UnsupportedType_ThrowsInvalidOperationException()
        {
            // Struct without properties should throw
            Action act = () => ComponentValueMapperInfo<EmptyStructWithoutProperties>.GetFixedAllocatedSize();
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("Unsupported type: Wasmtime.Tests.ComponentValueMapperInfoFixedSizeTests+EmptyStructWithoutProperties");
        }

        #endregion

        #region Edge Cases

        private enum SingleValueEnum
        {
            OnlyValue
        }

        [Fact]
        public void GetFixedAllocatedSize_SingleValueEnum_ReturnsCorrectSize()
        {
            var expectedSize = System.Text.Encoding.UTF8.GetByteCount("OnlyValue");
            var size = ComponentValueMapperInfo<SingleValueEnum>.GetFixedAllocatedSize();
            size.Should().Be(expectedSize);
        }

        private record RecordWithManyProperties(
            int Property1,
            string Property2,
            bool Property3,
            double Property4,
            float Property5,
            long Property6,
            byte Property7,
            short Property8,
            uint Property9,
            ulong Property10
        );

        [Fact]
        public void GetFixedAllocatedSize_RecordWithManyProperties_ReturnsCorrectSize()
        {
            var properties = new[] { 
                "Property1", "Property2", "Property3", "Property4", "Property5",
                "Property6", "Property7", "Property8", "Property9", "Property10"
            };
            var expectedSize = properties.Length * ValRecordEntrySize;
            foreach (var prop in properties)
            {
                expectedSize += System.Text.Encoding.UTF8.GetByteCount(prop);
            }
            
            var size = ComponentValueMapperInfo<RecordWithManyProperties>.GetFixedAllocatedSize();
            size.Should().Be(expectedSize);
        }

        #endregion
    }
}