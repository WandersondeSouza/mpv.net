
namespace HandyControl.Tools.Interop
{
    internal static class CommonHandles
    {
        public static readonly int Icon = HandleCollector.RegisterType(20, 500);

        public static readonly int HDC = HandleCollector.RegisterType(100, 2);

        public static readonly int GDI = HandleCollector.RegisterType(50, 500);

        public static readonly int Kernel = HandleCollector.RegisterType(0, 1000);
    }
}
