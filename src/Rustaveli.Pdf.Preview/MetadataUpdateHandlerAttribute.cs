#if !NET6_0_OR_GREATER
namespace System.Reflection.Metadata;

/// <summary>
/// Names a type the runtime tells of hot reload updates. The runtime finds it by its name, so on frameworks that do not
/// declare it this declaration serves.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
internal sealed class MetadataUpdateHandlerAttribute(Type handlerType) : Attribute
{
    public Type HandlerType { get; } = handlerType;
}
#endif
