#if !NET
using System.ComponentModel;

namespace System.Diagnostics.CodeAnalysis {
	[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
	sealed class MaybeNullWhenAttribute(bool returnValue) : Attribute {
		public bool ReturnValue { get; } = returnValue;
	}
}

namespace System.Runtime.CompilerServices {
	[EditorBrowsable(EditorBrowsableState.Never)]
	static class IsExternalInit { }
}
#endif

#if NETFRAMEWORK && !NET35_OR_GREATER
namespace System {
	delegate TResult Func<in T, out TResult>(T arg);
}
#endif
