using System.Collections.Generic;
using System.Xml.Serialization;

namespace BoundTreeGenerator;

public class Node : AbstractNode {
    [XmlAttribute]
    public string Root;

    [XmlAttribute]
    public string Errors;

    [XmlAttribute]
    public string SkipInNullabilityRewriter;

    [XmlElement(ElementName = "Kind", Type = typeof(Kind))]
    public List<Kind> kinds;
}
