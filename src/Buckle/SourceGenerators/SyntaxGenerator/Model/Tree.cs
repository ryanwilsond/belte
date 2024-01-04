using System.Collections.Generic;
using System.Xml.Serialization;

namespace SyntaxGenerator;

[XmlRoot]
public sealed class Tree {
    [XmlAttribute]
    public string root;

    [XmlElement(ElementName = "Node", Type = typeof(Node))]
    [XmlElement(ElementName = "AbstractNode", Type = typeof(AbstractNode))]
    [XmlElement(ElementName = "PredefinedNode", Type = typeof(PredefinedNode))]
    public List<TreeType> types;
}
