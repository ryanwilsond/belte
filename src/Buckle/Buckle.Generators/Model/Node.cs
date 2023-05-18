using System.Collections.Generic;
using System.Xml.Serialization;

namespace Buckle.Generators;

public class Node : TreeType {
    [XmlAttribute]
    public string root;

    [XmlAttribute]
    public string errors;

    [XmlElement(ElementName = "Kind", Type = typeof(Kind))]
    public List<Kind> kinds = new List<Kind>();

    public readonly List<Field> fields = new List<Field>();
}
