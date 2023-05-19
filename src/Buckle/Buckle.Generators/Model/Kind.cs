﻿using System;
using System.Xml.Serialization;

namespace Buckle.Generators;

public sealed class Kind : IEquatable<Kind> {
    [XmlAttribute]
    public string Name;

    public override bool Equals(object? obj) => Equals(obj as Kind);

    public bool Equals(Kind? other) => Name == other?.Name;

    public override int GetHashCode() => Name == null ? 0 : Name.GetHashCode();
}
