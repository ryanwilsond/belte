using System.Collections.Generic;
using System.Xml.Serialization;

namespace BoundTreeGenerator;

[XmlRoot]
public class Tree {
    [XmlAttribute]
    public string Root;

    [XmlElement(ElementName = "Node", Type = typeof(Node))]
    [XmlElement(ElementName = "AbstractNode", Type = typeof(AbstractNode))]
    [XmlElement(ElementName = "PredefinedNode", Type = typeof(PredefinedNode))]
    [XmlElement(ElementName = "Enum", Type = typeof(EnumType))]
    [XmlElement(ElementName = "ValueType", Type = typeof(ValueType))]
    public List<TreeType> types;
}
