using System.Collections.Generic;

namespace Buckle.CodeAnalysis;

internal interface IOrderedReadOnlySet<T> : IReadOnlySet<T>, IReadOnlyList<T> { }
