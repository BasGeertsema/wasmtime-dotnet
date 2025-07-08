using System;
using System.IO;
using FluentAssertions;
using System.Reflection;
using Xunit;

namespace Wasmtime.Tests
{
    public class ComponentSerializationTests
    {
        [Fact]
        public void ItSerializesAndDeserializesAComponent()
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("component.wasm");
            stream.Should().NotBeNull();
            using var engine = new Engine();
            using var original = Component.FromStream(engine, "component.wasm", stream!);
            original.Should().NotBeNull();
            
            var bytes = original.Serialize();
            bytes.Should().NotBeNull();
            bytes.Length.Should().NotBe(0);

            using var deserialized = Component.Deserialize(engine, "test", bytes);
            deserialized.Should().NotBeNull();
        }

        [Fact]
        public void ItDeserializesFromAFile()
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("component.wasm");
            stream.Should().NotBeNull();
            using var engine = new Engine();
            using var original = Component.FromStream(engine, "component.wasm", stream!);
            original.Should().NotBeNull();

            var bytes = original.Serialize();
            bytes.Should().NotBeNull();
            bytes.Length.Should().NotBe(0);

            var path = Path.GetTempFileName();

            File.WriteAllBytes(path, bytes);

            try
            {
                using var deserialized = Component.DeserializeFile(engine, "test", path);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
