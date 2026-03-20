using System.Collections.Generic;
using System.Xml.Serialization;

namespace BoundTreeGenerator;

public class AbstractNode : TreeType
{
    [XmlElement(ElementName = "Field", Type = typeof(Field))]
    public List<Field> Fields;
}
