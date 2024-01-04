using System.Collections.Generic;
using System.Xml.Serialization;

namespace SyntaxGenerator;

public sealed class Field : TreeTypeChild {
    [XmlAttribute]
    public string name;

    [XmlAttribute]
    public string type;

    [XmlAttribute]
    public string optional;

    [XmlAttribute]
    public string @override;

    [XmlElement(ElementName = "Kind", Type = typeof(Kind))]
    public List<Kind> kinds = new List<Kind>();

    public bool isToken => type == "SyntaxToken";
    public bool isOptional => optional == "true";
}
