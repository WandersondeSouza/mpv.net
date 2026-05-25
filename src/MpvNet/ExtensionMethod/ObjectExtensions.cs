
namespace MpvNet.Extensions;

public static class ObjectExtensions
{
    public static string ToStringEx(this object? instance) => instance?.ToString() ?? "";
}
