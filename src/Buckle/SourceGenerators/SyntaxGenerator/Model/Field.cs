using System.Collections.Generic;
using System.Xml.Serialization;

namespace SyntaxGenerator;

public sealed class Field : TreeTypeChild {
    [XmlAttribute]
    public string Name;

    [XmlAttribute]
    public string Type;

    [XmlAttribute]
    public string Optional;

    [XmlAttribute]
    public string Override;

    [XmlElement(ElementName = "Kind", Type = typeof(Kind))]
    public List<Kind> Kinds = new List<Kind>();

    public bool isToken => Type == "SyntaxToken";
    public bool isOptional => Optional == "true";
}
