using System.Collections.Generic;
using System.Xml.Serialization;

namespace SyntaxGenerator;

public abstract class TreeType {
    [XmlAttribute]
    public string Name;

    [XmlAttribute]
    public string Base;

    [XmlElement(ElementName = "Field", Type = typeof(Field))]
    public List<TreeTypeChild> Children = new List<TreeTypeChild>();
}
