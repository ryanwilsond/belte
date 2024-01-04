using System;
using System.Xml.Serialization;

namespace SyntaxGenerator;

public sealed class Kind : IEquatable<Kind> {
    [XmlAttribute]
    public string name;

    public override bool Equals(object obj) => Equals(obj as Kind);

    public bool Equals(Kind other) => name == other?.name;

    public override int GetHashCode() => name is null ? 0 : name.GetHashCode();
}
