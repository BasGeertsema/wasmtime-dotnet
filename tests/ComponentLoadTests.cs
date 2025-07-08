using System;
using System.Reflection;
using FluentAssertions;
using Wasmtime;
using Xunit;

namespace Wasmtime.Tests
{
    public class ComponentLoadTests
    {
        [Fact]
        public void ItLoadsComponentFromEmbeddedResource()
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("component.wasm");
            stream.Should().NotBeNull();

            using var engine = new Engine();
            Component.FromStream(engine, "component.wasm", stream!).Should().NotBeNull();

            // `LoadModule` is not supposed to close the supplied stream,
            // so the following statement should complete without throwing
            // `ObjectDisposedException`
            stream.ReadExactly([], 0, 0);
        }

        // [Fact]
        // public void ItValidatesComponentFromEmbeddedResource()
        // {
        //     using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("component.wasm");
        //     stream.Should().NotBeNull();
        //
        //     byte[] buffer = new byte[stream.Length];
        //
        //     stream.Read(buffer, 0, buffer.Length);
        //
        //     using var engine = new Engine();
        //     Component.Validate(engine, buffer).Should().BeNull();
        // }

        // [Fact]
        // public void ItLoadsModuleTextFromEmbeddedResource()
        // {
        //     using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("hello.wat");
        //     stream.Should().NotBeNull();
        //
        //     using var engine = new Engine();
        //     Component.FromTextStream(engine, "hello.wat", stream).Should().NotBeNull();
        //
        //     // `LoadModuleText` is not supposed to close the supplied stream,
        //     // so the following statement should complete without throwing
        //     // `ObjectDisposedException`
        //     stream.Read(new byte[0], 0, 0);
        // }

        [Fact]
        public void ItCannotBeAccessedOnceDisposed()
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("component.wasm");
            stream.Should().NotBeNull();

            using var engine = new Engine();
            var module = Component.FromStream(engine, "component.wasm", stream!);

            module.Dispose();

            Assert.Throws<ObjectDisposedException>(() => module.NativeHandle);
            Assert.Throws<ObjectDisposedException>(() => module.Serialize());
        }
    }
}
