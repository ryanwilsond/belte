using System.Collections.Generic;

namespace Buckle.Generators;

public sealed class AbstractNode : TreeType {
    public readonly List<Field> Fields = new List<Field>();
}
