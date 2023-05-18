using System.Collections.Generic;

namespace Buckle.Generators;

public class AbstractNode : TreeType {
    public readonly List<Field> fields = new List<Field>();
}
