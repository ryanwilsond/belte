using System.Collections.Generic;

namespace SyntaxGenerator;

public sealed class AbstractNode : TreeType {
    public readonly List<Field> fields = new List<Field>();
}
