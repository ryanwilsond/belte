using System.Xml.Serialization;

namespace BoundTreeGenerator;

public class TreeType {
    [XmlAttribute]
    public string Name;

    [XmlAttribute]
    public string Base;

    [XmlAttribute]
    public string HasValidate;
}
