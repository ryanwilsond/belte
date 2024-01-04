using System.Collections.Generic;
using System.Xml.Serialization;

namespace SyntaxGenerator;

public abstract class TreeType {
    [XmlAttribute]
    public string name;

    [XmlAttribute]
    public string @base;

    [XmlElement(ElementName = "Field", Type = typeof(Field))]
    public List<TreeTypeChild> children = new List<TreeTypeChild>();
}
