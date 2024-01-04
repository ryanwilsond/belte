using System.Collections.Generic;
using System.Xml.Serialization;

namespace SyntaxGenerator;

public sealed class Node : TreeType {
    [XmlAttribute]
    public string root;

    [XmlElement(ElementName = "Kind", Type = typeof(Kind))]
    public List<Kind> kinds = new List<Kind>();

    public readonly List<Field> fields = new List<Field>();
}
