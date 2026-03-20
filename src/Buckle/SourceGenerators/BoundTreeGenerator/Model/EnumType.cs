using System.Collections.Generic;
using System.Xml.Serialization;

namespace BoundTreeGenerator;

public class EnumType : TreeType {
    [XmlAttribute]
    public string Flags;

    [XmlElement(ElementName = "Field", Type = typeof(EnumField))]
    public List<EnumField> fields;
}
