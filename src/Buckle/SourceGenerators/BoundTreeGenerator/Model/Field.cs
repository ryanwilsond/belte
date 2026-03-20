using System.Xml.Serialization;

namespace BoundTreeGenerator;

public class Field {
    [XmlAttribute]
    public string Name;

    [XmlAttribute]
    public string Type;

    [XmlAttribute]
    public string Null;

    [XmlAttribute]
    public bool Override;

    [XmlAttribute]
    public string New;

    [XmlAttribute]
    public string PropertyOverrides;

    [XmlAttribute]
    public string SkipInVisitor;

    [XmlAttribute]
    public string SkipInNullabilityRewriter;
}
