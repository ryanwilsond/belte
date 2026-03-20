using System.Xml.Serialization;

namespace BoundTreeGenerator;

public class EnumField {
    [XmlAttribute]
    public string Name;

    [XmlAttribute]
    public string Value;
}
