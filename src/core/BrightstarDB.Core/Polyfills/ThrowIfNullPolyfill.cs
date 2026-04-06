// Polyfill for ArgumentNullException.ThrowIfNull, which is not available in netstandard2.0.
// On .NET 6+ targets the native implementation is called; on older targets a manual check is performed.
// A global using static import makes ThrowIfNull available without qualification in every file.
global using static BrightstarDB.Polyfills.ArgumentNullExceptionPolyfill;

#if !NET6_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    internal sealed class CallerArgumentExpressionAttribute : Attribute
    {
        public CallerArgumentExpressionAttribute(string parameterName) => ParameterName = parameterName;
        public string ParameterName { get; }
    }
}
#endif

namespace BrightstarDB.Polyfills
{
    internal static class ArgumentNullExceptionPolyfill
    {
        public static void ThrowIfNull(
            object argument,
            [System.Runtime.CompilerServices.CallerArgumentExpression("argument")] string paramName = null)
        {
#if NET6_0_OR_GREATER
            System.ArgumentNullException.ThrowIfNull(argument, paramName);
#else
            if (argument is null)
            {
                throw new System.ArgumentNullException(paramName);
            }
#endif
        }
    }
}
