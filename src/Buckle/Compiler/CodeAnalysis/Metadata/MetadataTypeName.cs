using System;
using System.Collections.Immutable;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

[NonCopyable]
internal partial struct MetadataTypeName {
    private string _fullName;
    private string _namespaceName;
    private ReadOnlyMemory<char> _namespaceNameMemory;
    private string _typeName;
    private ReadOnlyMemory<char> _typeNameMemory;
    private string _unmangledTypeName;
    private ReadOnlyMemory<char> _unmangledTypeNameMemory;
    private short _inferredArity;
    private short _forcedArity;
    private bool _useCLSCompliantNameArityEncoding;
    private ImmutableArray<string> _namespaceSegments;
    private ImmutableArray<ReadOnlyMemory<char>> _namespaceSegmentsMemory;

    internal static MetadataTypeName FromFullName(
        string fullName,
        bool useCLSCompliantNameArityEncoding = false,
        int forcedArity = -1) {
        MetadataTypeName name;

        name._fullName = fullName;
        name._namespaceName = null;
        name._namespaceNameMemory = default;
        name._typeName = null;
        name._typeNameMemory = default;
        name._unmangledTypeName = null;
        name._unmangledTypeNameMemory = default;
        name._inferredArity = -1;
        name._useCLSCompliantNameArityEncoding = useCLSCompliantNameArityEncoding;
        name._forcedArity = (short)forcedArity;
        name._namespaceSegments = default;
        name._namespaceSegmentsMemory = default;

        return name;
    }

    internal static MetadataTypeName FromNamespaceAndTypeName(
        string namespaceName,
        string typeName,
        bool useCLSCompliantNameArityEncoding = false,
        int forcedArity = -1) {
        MetadataTypeName name;

        name._fullName = null;
        name._namespaceName = namespaceName;
        name._namespaceNameMemory = namespaceName.AsMemory();
        name._typeName = typeName;
        name._typeNameMemory = typeName.AsMemory();
        name._unmangledTypeName = null;
        name._unmangledTypeNameMemory = default;
        name._inferredArity = -1;
        name._useCLSCompliantNameArityEncoding = useCLSCompliantNameArityEncoding;
        name._forcedArity = (short)forcedArity;
        name._namespaceSegments = default;
        name._namespaceSegmentsMemory = default;

        return name;
    }

    internal static MetadataTypeName FromTypeName(
        string typeName,
        bool useCLSCompliantNameArityEncoding = false,
        int forcedArity = -1) {
        MetadataTypeName name;

        name._fullName = typeName;
        name._namespaceName = string.Empty;
        name._namespaceNameMemory = string.Empty.AsMemory();
        name._typeName = typeName;
        name._typeNameMemory = typeName.AsMemory();
        name._unmangledTypeName = null;
        name._unmangledTypeNameMemory = default;
        name._inferredArity = -1;
        name._useCLSCompliantNameArityEncoding = useCLSCompliantNameArityEncoding;
        name._forcedArity = (short)forcedArity;
        name._namespaceSegments = [];
        name._namespaceSegmentsMemory = [];

        return name;
    }

    internal string fullName {
        get {
            _fullName ??= MetadataHelpers.BuildQualifiedName(_namespaceName, _typeName);
            return _fullName;
        }
    }

    internal ReadOnlyMemory<char> namespaceNameMemory {
        get {
            if (_namespaceNameMemory.Equals(default))
                _typeNameMemory = MetadataHelpers.SplitQualifiedName(_fullName, out _namespaceNameMemory);

            return _namespaceNameMemory;
        }
    }

    internal string namespaceName => _namespaceName ??= namespaceNameMemory.ToString();

    internal ReadOnlyMemory<char> typeNameMemory {
        get {
            if (_typeNameMemory.Equals(default))
                _typeNameMemory = MetadataHelpers.SplitQualifiedName(_fullName, out _namespaceNameMemory);

            return _typeNameMemory;
        }
    }

    internal string typeName => _typeName ??= typeNameMemory.ToString();

    internal ReadOnlyMemory<char> unmangledTypeNameMemory {
        get {
            if (_unmangledTypeNameMemory.Equals(default)) {
                _unmangledTypeNameMemory = MetadataHelpers.InferTypeArityAndUnmangleMetadataName(
                    typeNameMemory,
                    out _inferredArity
                );
            }

            return _unmangledTypeNameMemory;
        }
    }

    internal string unmangledTypeName {
        get {
            _unmangledTypeName ??= unmangledTypeNameMemory.Equals(typeNameMemory)
                ? typeName
                : unmangledTypeNameMemory.ToString();

            return _unmangledTypeName;
        }
    }

    internal int inferredArity {
        get {
            if (_inferredArity == -1) {
                _unmangledTypeNameMemory = MetadataHelpers.InferTypeArityAndUnmangleMetadataName(
                    typeNameMemory,
                    out _inferredArity
                );
            }

            return _inferredArity;
        }
    }

    internal bool isMangled => inferredArity > 0;

    internal readonly bool useCLSCompliantNameArityEncoding => _useCLSCompliantNameArityEncoding;

    internal readonly int forcedArity => _forcedArity;

    internal ImmutableArray<ReadOnlyMemory<char>> namespaceSegmentsMemory {
        get {
            if (_namespaceSegmentsMemory.IsDefault)
                _namespaceSegmentsMemory = MetadataHelpers.SplitQualifiedName(namespaceNameMemory);

            return _namespaceSegmentsMemory;
        }
    }

    internal ImmutableArray<string> namespaceSegments {
        get {
            if (_namespaceSegments.IsDefault)
                _namespaceSegments = namespaceSegmentsMemory.SelectAsArray(static s => s.ToString());

            return _namespaceSegments;
        }
    }

    internal readonly bool isNull => _typeName == null && _fullName == null;

    public override string ToString() {
        if (isNull)
            return "{Null}";
        else
            return $"{{ {namespaceName},{typeName},{useCLSCompliantNameArityEncoding},{_forcedArity} }}";
    }

    internal readonly Key ToKey() {
        return new Key(in this);
    }
}
