using System;

namespace Buckle.Utilities;

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.GenericParameter)]
internal sealed class NonCopyableAttribute : Attribute { }
