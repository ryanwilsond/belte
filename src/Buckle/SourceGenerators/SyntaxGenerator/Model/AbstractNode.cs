using System.Collections.Generic;

namespace SyntaxGenerator;

public sealed class AbstractNode : TreeType {
    public readonly List<Field> Fields = new List<Field>();
}
