using System.Collections.Generic;
using System.Xml.Serialization;

namespace Buckle.Generators;

public sealed class Node : TreeType {
    [XmlAttribute]
    public string Root;

    [XmlElement(ElementName = "Kind", Type = typeof(Kind))]
    public List<Kind> Kinds = new List<Kind>();

    public readonly List<Field> Fields = new List<Field>();
}
