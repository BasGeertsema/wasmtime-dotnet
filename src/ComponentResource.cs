using System;
using System.Runtime.InteropServices;

namespace Wasmtime
{
    /// <summary>
    /// Represents a WebAssembly Component Model resource handle.
    /// 
    /// Note: This is a placeholder for future resource support.
    /// Resources in the Component Model are opaque handles that represent
    /// instances of resource types defined in component interfaces.
    /// 
    /// Once Wasmtime C API implements resource support, this class would:
    /// - Wrap resource handles (typically u32 values)
    /// - Provide access to resource methods
    /// - Handle resource lifecycle (creation, destruction)
    /// - Support resource type checking
    /// </summary>
    public class ComponentResource
    {
        /// <summary>
        /// The resource handle value.
        /// In the Component Model, resources are typically represented as u32 handles.
        /// </summary>
        public uint Handle { get; private set; }

        /// <summary>
        /// The store this resource belongs to.
        /// </summary>
        public Store? Store { get; private set; }

        /// <summary>
        /// The resource type name (e.g., "blob").
        /// </summary>
        public string TypeName { get; private set; }

        internal ComponentResource(Store store, uint handle, string typeName)
        {
            Store = store ?? throw new ArgumentNullException(nameof(store));
            Handle = handle;
            TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
        }

        /// <summary>
        /// Creates a ComponentValueBox containing this resource handle.
        /// Resources are passed to component functions as u32 values.
        /// </summary>
        public ComponentValueBox ToComponentValue()
        {
            return (uint)Handle;
        }

        /// <summary>
        /// Creates a ComponentResource from a ComponentValueBox containing a u32 handle.
        /// </summary>
        public static ComponentResource FromComponentValue(Store store, ComponentValueBox value, string typeName)
        {
            var handle = value.AsU32();
            return new ComponentResource(store, handle, typeName);
        }
    }

    /// <summary>
    /// Extension methods for ComponentValueKind to support resources.
    /// </summary>
    internal static class ComponentValueKindExtensions
    {
        /// <summary>
        /// Resources would need a new value kind in the Component Model.
        /// This is a placeholder value that would be defined in the C API.
        /// </summary>
        internal const ComponentValueKind Resource = (ComponentValueKind)21;
    }

    /// <summary>
    /// Placeholder for resource type information.
    /// In a full implementation, this would provide:
    /// - Resource type validation
    /// - Method discovery (constructor, instance methods, static methods)
    /// - Type compatibility checking
    /// </summary>
    public class ComponentResourceType
    {
        /// <summary>
        /// The name of the resource type (e.g., "blob").
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// The interface this resource belongs to.
        /// </summary>
        public string Interface { get; private set; }

        internal ComponentResourceType(string name, string interfaceName)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Interface = interfaceName ?? throw new ArgumentNullException(nameof(interfaceName));
        }
    }
}