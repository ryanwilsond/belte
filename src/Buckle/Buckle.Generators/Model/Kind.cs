using System;
using System.Xml.Serialization;

namespace Buckle.Generators;

public class Kind : IEquatable<Kind> {
    [XmlAttribute]
    public string name;

    public override bool Equals(object? obj) => Equals(obj as Kind);

    public bool Equals(Kind? other) => name == other?.name;

    public override int GetHashCode() => name == null ? 0 : name.GetHashCode();
}
