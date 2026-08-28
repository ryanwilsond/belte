using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Evaluating;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

internal sealed partial class TemplateMetadataWriter {
    /*

    Offset  Size

Header

    0       4       Magic
    4       2       Major Version
    6       2       Minor Version
    8       4       Header Size

Assembly Table

    0       4       Assembly Table Size
    4       4       Assembly Count

    :Assembly Entry:

            4       Assembly Identity Size
            ...     Assembly Identity

Type Table  (this describes unconstructed types only)

    0       4       Type Table Size
    4       4       Type Count

    :Type Entry:

            4       Type Entry Size
            4       Name Size
            ...     Name
            2       Arity
            1       Flags
            4       Namespace Name Size
            ...     Namespace Name
            4       Assembly Index
            1       IsNested
            4       Containing Type Index

Method Table

    0       4       Method Table Size
    4       4       Method Count

    :Method Entry:

            4       Method Entry Size
            4       Name Size
            ...     Name
            2       Arity
            4       Containing Type Entry Index
            4       Method Attributes
            2       Flags
            2       Template Parameter Count
            ...     Template Parameters
            1       Return Flags
            1       Return Type Kind
            ...     Return Type Info
            2       Parameter Count
            ...     Parameter Signature
            2       Expression Constraint Count
            ...     Expression Constraints

            :Template Parameter Entry:

            4       Name Size
            ...     Name
            1       Flags
            4       Generic Parameter Attributes
            1       Underlying Type Kind
            ...     Underlying Type Info
            ...     Default Value

            :Parameter Signature Entry:

            4       Name Size
            ...     Name
            1       Flags
            4       Attributes
            1       Type Kind
            ...     Type Info
            ...     Default Value

            :Expression Constraint Entry:

            4       Entry Size
            ...     IR

Type Definition Table

    0       4       Type Definition Table Size
    4       4       Type Definition Count

    :Type Definition Entry:

            4       Type Definition Entry Size
            4       Type Entry Index
            1       Flags
            4       Type Attributes
            1       Base Type Kind
            ...     Base Type Info
            2       Template Parameter Count
            ...     Template Parameters
            2       Interface Count
            ...     Interfaces
            2       Field Count
            ...     Fields
            2       Method Count
            ...     Methods
            2       Expression Constraint Count
            ...     Expression Constraints

            :Template Parameter Entry:

            4       Name Size
            ...     Name
            1       Flags
            4       Generic Parameter Attributes
            1       Underlying Type Kind
            ...     Underlying Type Info
            ...     Default Value

            :Interface Entry:

            1       Type Kind
            ...     Type Info

            :Field Entry:

            4       Name Size
            ...     Name
            1       Flags
            4       Field Attributes
            1       Type Kind
            ...     Type Info

            :Method Entry:

            4       Method Index

            :Expression Constraint Entry:

            4       Entry Size
            ...     IR

Template Table

    0       4       Template Table Size
    4       4       Template Count

    :Template Entry:

            4       Template Entry Size
            2       Kind/Flags
            4       Type Index
            4       Bound Entry Count   (1 per method)
            ...     Bound Entry Index   (relative to start of Syntax Table)
                                        (for methods this points to the containing type entry)

Bound Table

    0       4       Bound Table Size
    4       4       Bound Count

    :Bound Entry:

            4       Bound Entry Size
            4       Method Entry Index
            ...     IR

    */

    internal const string ResourceName = "TemplateMetadata";
    internal const ushort MajorVersion = 1;
    internal const ushort MinorVersion = 0;

    private readonly Compilation _compilation;

    private readonly Dictionary<TypeSymbol, uint> _typeTableIndexes = [];
    private readonly List<(SourceNamedTypeSymbol type, uint firstBoundEntry, uint boundEntryCount)> _templatesNeedingEntries = [];

    private readonly Dictionary<MethodSymbol, uint> _methodTableIndexes = [];

    private readonly Dictionary<AssemblyIdentity, uint> _assemblyTableIndexes = [];

    private readonly Dictionary<TypeSymbol, byte[]> _typeDefEntries = [];
    private readonly Dictionary<MethodSymbol, byte[]> _methodEntries = [];

    private uint _assemblyTableSize = 8;
    private uint _assemblyTableCount = 0;

    private uint _typeTableSize = 8;
    private uint _typeTableCount = 0;

    private uint _methodTableSize = 8;
    private uint _methodTableCount = 0;

    private uint _typeDefinitionTableSize = 8;
    private uint _typeDefinitionTableCount = 0;

    private uint _templateTableSize = 8;
    private uint _templateTableCount = 0;

    private readonly List<(MethodSymbol, byte[])> _boundEntries = [];
    private uint _boundTableSize = 8;
    private uint _boundTableCount = 0;

    private TemplateMetadataWriter(Compilation compilation) {
        _compilation = compilation;
    }

    internal static void Write(
        Compilation compilation,
        Stream stream,
        ImmutableArray<NamedTypeSymbol> types,
        ImmutableDictionary<MethodSymbol, BoundBlockStatement> methodBodies) {
        var writer = new TemplateMetadataWriter(compilation);
        writer.Collect(types, methodBodies);
        writer.Write(stream);
    }

    private void Collect(
        ImmutableArray<NamedTypeSymbol> types,
        ImmutableDictionary<MethodSymbol, BoundBlockStatement> methodBodies) {
        foreach (var type in types) {
            if (type.originalDefinition is not SourceNamedTypeSymbol sourceType)
                continue;

            if (sourceType.arity == 0) {
                var foundTemplate = false;

                foreach (var member in sourceType.GetMembers()) {
                    if (member.GetArity() > 0) {
                        foundTemplate = true;
                        break;
                    }
                }

                if (!foundTemplate)
                    continue;
            }

            if (_typeTableIndexes.ContainsKey(sourceType))
                continue;

            LogTypeEntryForType(sourceType);

            uint methodCountForType = 0;
            var methodIndexes = new List<uint>();
            var firstBoundEntry = (uint)_boundEntries.Count;

            foreach (var pair in methodBodies) {
                var method = pair.Key;

                if (method.containingType.Equals(type)) {
                    methodCountForType++;

                    var ir = CreateIR(pair.Value);

                    _boundEntries.Add((method, ir));

                    _boundTableCount++;
                    _boundTableSize +=
                        4 +                                                     // Bound Entry Size
                        4 +                                                     // Method Entry Index
                        (uint)ir.Length;                                        // IR

                    methodIndexes.Add(LogMethodEntryForMethod(method));
                }
            }

            _templatesNeedingEntries.Add((sourceType, firstBoundEntry, methodCountForType));

            _templateTableCount++;
            _templateTableSize +=
                4 +                                                             // Template Entry Size
                2 +                                                             // Kind/Flags
                4 +                                                             // Type Index
                4 +                                                             // Bound Entry Count
                4 * methodCountForType;                                         // Bound Entry Index

            Debug.Assert(methodIndexes.Count == methodCountForType);
            LogTypeDefinitionEntry(sourceType, methodCountForType, methodIndexes);
        }
    }

    private void LogTypeDefinitionEntry(NamedTypeSymbol type, uint methodCountForType, List<uint> methodIndexes) {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);

        writer.Write((uint)0);
        writer.Write(_typeTableIndexes[type]);
        writer.Write(CreateTypeDefFlags(type));
        writer.Write((uint)Executor.GetTypeAttributes(type, isNested: type.containingSymbol.IsTypeOrTypeAlias()));
        writer.Write(CreateTypeKindAndInfo(type.baseType));
        writer.Write((ushort)type.arity);

        foreach (var templateParameter in type.templateParameters) {
            Debug.Assert((uint)templateParameter.metadataName.Length == Encoding.UTF8.GetBytes(templateParameter.metadataName).Length);
            writer.Write((uint)templateParameter.metadataName.Length);
            writer.Write(Encoding.UTF8.GetBytes(templateParameter.metadataName));
            writer.Write(CreateTemplateParameterFlags(templateParameter));
            writer.Write(CreateGenericParameterFlags(templateParameter));
            writer.Write(CreateTypeKindAndInfo(templateParameter.underlyingType.type));

            if (templateParameter.defaultValue is not null)
                writer.Write(CreateTemplateParameterDefaultValue(templateParameter));
        }

        writer.Write((ushort)type.allInterfaces.Length);

        foreach (var inter in type.allInterfaces)
            writer.Write(CreateTypeKindAndInfo(inter));

        var fields = type.GetMembers().WhereAsArray(t => t is FieldSymbol);

        writer.Write((ushort)fields.Length);

        foreach (FieldSymbol field in fields) {
            Debug.Assert((uint)field.metadataName.Length == Encoding.UTF8.GetBytes(field.metadataName).Length);
            writer.Write((uint)field.metadataName.Length);
            writer.Write(Encoding.UTF8.GetBytes(field.metadataName));
            writer.Write(CreateFieldFlags(field));
            writer.Write((uint)Executor.GetFieldAttributes(field));
            writer.Write(CreateTypeKindAndInfo(field.type));
        }

        writer.Write((ushort)methodCountForType);

        for (var i = 0; i < methodCountForType; i++)
            writer.Write(methodIndexes[i]);

        writer.Write((ushort)type.templateConstraints.Length);

        foreach (var constraint in type.templateConstraints) {
            var ir = CreateIR(constraint);
            writer.Write((uint)ir.Length + 4);
            writer.Write(ir);
        }

        writer.BaseStream.Seek(0, SeekOrigin.Begin);
        writer.Write((uint)writer.BaseStream.Length);

        _typeDefinitionTableCount++;
        _typeDefinitionTableSize += (uint)writer.BaseStream.Length;

        _typeDefEntries.Add(type, stream.ToArray());
    }

    private byte CreateFieldFlags(FieldSymbol field) {
        if (field.refKind != RefKind.None)
            return (byte)FieldFlags.ByRef;

        return (byte)FieldFlags.None;
    }

    private byte CreateTemplateParameterFlags(TemplateParameterSymbol templateParameter) {
        var flags = (byte)TemplateParameterFlags.None;

        if (templateParameter.isCompileTimeType)
            flags |= (byte)TemplateParameterFlags.CompileTime;

        if (templateParameter.hasDefaultConstraint)
            flags |= (byte)TemplateParameterFlags.HasDefaultConstraint;

        if (templateParameter.hasNotNullConstraint)
            flags |= (byte)TemplateParameterFlags.HasNotNullConstraint;

        if (templateParameter.defaultValue is not null)
            flags |= (byte)TemplateParameterFlags.HasDefaultValue;

        return flags;
    }

    private uint CreateGenericParameterFlags(TemplateParameterSymbol templateParameter) {
        var flags = (uint)GenericParameterAttributes.None;

        if (templateParameter.hasReferenceTypeConstraint)
            flags |= (uint)GenericParameterAttributes.ReferenceTypeConstraint;

        if (templateParameter.hasConstructorConstraint)
            flags |= (uint)GenericParameterAttributes.DefaultConstructorConstraint;

        if (templateParameter.hasValueTypeConstraint)
            flags |= (uint)GenericParameterAttributes.NotNullableValueTypeConstraint;

        return flags;
    }

    private byte[] CreateTemplateParameterDefaultValue(TemplateParameterSymbol templateParameter) {
        Debug.Assert(templateParameter.defaultValue is not null);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);

        Debug.Assert(templateParameter.defaultValue.isType == (templateParameter.underlyingType.specialType == SpecialType.Type));

        if (templateParameter.defaultValue.isType)
            writer.Write(CreateTypeKindAndInfo(templateParameter.defaultValue.type.type));
        else
            WriteConstantValueValue(writer, templateParameter.defaultValue.constant);

        return stream.ToArray();
    }

    private byte CreateReturnFlags(MethodSymbol method) {
        if (method.returnsByRef)
            return (byte)ReturnFlags.ByRef;

        return (byte)ReturnFlags.None;
    }

    private byte CreateParameterFlags(ParameterSymbol parameter) {
        var flags = (byte)ParameterFlags.None;

        if (parameter.refKind != RefKind.None)
            flags |= (byte)ParameterFlags.ByRef;

        if (parameter.hasOutDefaultValue)
            flags |= (byte)ParameterFlags.HasOutDefaultValue;

        if (parameter.isConst)
            flags |= (byte)ParameterFlags.IsConst;

        return flags;
    }

    private uint CreateParameterAttributes(ParameterSymbol parameter) {
        var flags = (uint)ParameterAttributes.None;

        if (parameter.refKind == RefKind.Out)
            flags |= (uint)ParameterAttributes.Out;

        if (parameter.hasExplicitDefaultValue)
            flags |= (uint)ParameterAttributes.HasDefault;

        return flags;
    }

    private byte CreateTypeFlags(TypeSymbol type) {
        if (type.specialType == SpecialType.Object)
            return (byte)TypeFlags.IsObject;

        if (type.specialType == SpecialType.Nullable)
            return (byte)TypeFlags.IsNullable;

        if (type.containingAssembly is null &&
            (object)type.containingNamespace == _compilation.corLibrary.belteNamespace.originalDefinition) {
            return (byte)TypeFlags.IsInMemoryLibraryType;
        }

        return (byte)TypeFlags.None;
    }

    private ushort CreateMethodFlags(MethodSymbol method) {
        if (method.containingType.specialType == SpecialType.Nullable) {
            var flags = (ushort)MethodFlags.IsWellKnownMember;

            if (WellKnownMemberExtensions.GetMetadataName(WellKnownMember.Nullable_ctor) == method.metadataName)
                flags |= (byte)WellKnownMember.Nullable_ctor;
            else if (WellKnownMemberExtensions.GetMetadataName(WellKnownMember.Nullable_getHasValue) == method.metadataName)
                flags |= (byte)WellKnownMember.Nullable_getHasValue;
            else if (WellKnownMemberExtensions.GetMetadataName(WellKnownMember.Nullable_getValue) == method.metadataName)
                flags |= (byte)WellKnownMember.Nullable_getValue;
            else if (WellKnownMemberExtensions.GetMetadataName(WellKnownMember.Nullable_GetValueOrDefault) == method.metadataName && method.parameterCount == 0)
                flags |= (byte)WellKnownMember.Nullable_GetValueOrDefault;
            else if (WellKnownMemberExtensions.GetMetadataName(WellKnownMember.Nullable_GetValueOrDefault_T) == method.metadataName && method.parameterCount == 1)
                flags |= (byte)WellKnownMember.Nullable_GetValueOrDefault_T;

            return flags;
        }

        return (ushort)MethodFlags.None;
    }

    private byte CreateTypeDefFlags(NamedTypeSymbol type) {
        foreach (var templateParameter in type.templateParameters) {
            if (templateParameter.underlyingType.specialType != SpecialType.Type || templateParameter.isCompileTimeType)
                return (byte)TypeDefFlags.None;
        }

        return (byte)TypeDefFlags.IsForSpecializationOnly;
    }

    private void LogTypeEntryForType(TypeSymbol type) {
        type = type.originalDefinition;

        if (_typeTableIndexes.ContainsKey(type))
            return;

        _typeTableIndexes.Add(type, (uint)_typeTableIndexes.Count);

        _typeTableCount++;

        _typeTableSize +=
            4 +                                                                 // Type Entry Size
            4 +                                                                 // Name Size
            (uint)type.metadataName.Length +                                    // Name
            2 +                                                                 // Arity
            1 +                                                                 // Flags
            4 +                                                                 // Namespace Name Size
            (uint)(type.containingNamespace?.metadataName?.Length ?? 0) +       // Namespace Name
            4 +                                                                 // Assembly Identity Index
            1 +                                                                 // IsNested
            4;                                                                  // Containing Type Index

        if (type.containingSymbol is TypeSymbol containingType)
            LogTypeEntryForType(containingType);

        if (type.containingAssembly is null || _assemblyTableIndexes.ContainsKey(type.containingAssembly.identity))
            return;

        _assemblyTableIndexes.Add(type.containingAssembly.identity, _assemblyTableCount);

        _assemblyTableCount++;

        _assemblyTableSize +=
            4 +                                                                 // Assembly Identity Size
            (uint)type.containingAssembly.identity.GetDisplayName(fullKey: true).Length;    // Assembly Identity
    }

    private byte[] CreateTypeKindAndInfo(TypeSymbol type) {
        var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);

        if (type.specialType != SpecialType.None && type.specialType.CanEncodeToTemplateMetadata()) {
            writer.Write((byte)1);
            writer.Write((byte)type.specialType);
        } else if (type is ArrayTypeSymbol arrayType) {
            writer.Write((byte)2);
            writer.Write(CreateTypeKindAndInfo(arrayType.elementType));
        } else if (type is PointerTypeSymbol pointerType) {
            writer.Write((byte)3);
            writer.Write(CreateTypeKindAndInfo(pointerType.pointedAtType));
        } else if (type is TemplateParameterSymbol templateParameter) {
            writer.Write((byte)4);
            writer.Write((byte)templateParameter.templateParameterKind);
            writer.Write((ushort)templateParameter.ordinal);
        } else if (type is FunctionPointerTypeSymbol functionPointerType) {
            writer.Write((byte)5);
            writer.Write((ushort)functionPointerType.signature.parameterCount + 1);

            foreach (var parameterType in functionPointerType.signature.GetParameterTypes())
                writer.Write(CreateTypeKindAndInfo(parameterType.type));

            writer.Write(CreateTypeKindAndInfo(functionPointerType.signature.returnType));
        } else if (type is FunctionTypeSymbol functionType) {
            writer.Write((byte)6);
            writer.Write((ushort)functionType.signature.parameterCount + 1);

            foreach (var parameterType in functionType.signature.GetParameterTypes())
                writer.Write(CreateTypeKindAndInfo(parameterType.type));

            writer.Write(CreateTypeKindAndInfo(functionType.signature.returnType));
        } else if (type is ConstructedNamedTypeSymbol constructedType) {
            writer.Write((byte)7);
            LogTypeEntryForType(constructedType.constructedFrom);
            writer.Write(_typeTableIndexes[constructedType.constructedFrom]);

            foreach (var templateArgument in constructedType.templateArguments) {
                if (templateArgument.isType)
                    writer.Write(CreateTypeKindAndInfo(templateArgument.type.type));
                else
                    WriteConstantValueValue(writer, templateArgument.constant);
            }
        } else {
            Debug.Assert((object)type == type.originalDefinition);
            LogTypeEntryForType(type);
            writer.Write((byte)8);
            writer.Write(_typeTableIndexes[type]);
        }

        return stream.ToArray();
    }

    private uint CreateMethodIndex(MethodSymbol method) {
        method = method.originalDefinition;
        LogMethodEntryForMethod(method);
        return _methodTableIndexes[method];
    }

    private uint LogMethodEntryForMethod(MethodSymbol method) {
        method = method.originalDefinition;

        if (_methodTableIndexes.TryGetValue(method, out var value))
            return value;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);

        LogTypeEntryForType(method.containingType);

        writer.Write((uint)0);
        Debug.Assert((uint)method.metadataName.Length == Encoding.UTF8.GetBytes(method.metadataName).Length);
        writer.Write((uint)method.metadataName.Length);
        writer.Write(Encoding.UTF8.GetBytes(method.metadataName));
        writer.Write((ushort)method.GetArity());
        writer.Write(_typeTableIndexes[method.containingType]);
        writer.Write(GetMethodAttributes(method));
        writer.Write(CreateMethodFlags(method));
        writer.Write((ushort)method.arity);

        foreach (var templateParameter in method.templateParameters) {
            Debug.Assert((uint)templateParameter.metadataName.Length == Encoding.UTF8.GetBytes(templateParameter.metadataName).Length);
            writer.Write((uint)templateParameter.metadataName.Length);
            writer.Write(Encoding.UTF8.GetBytes(templateParameter.metadataName));
            writer.Write(CreateTemplateParameterFlags(templateParameter));
            writer.Write(CreateGenericParameterFlags(templateParameter));
            writer.Write(CreateTypeKindAndInfo(templateParameter.underlyingType.type));

            if (templateParameter.defaultValue is not null)
                writer.Write(CreateTemplateParameterDefaultValue(templateParameter));
        }

        writer.Write(CreateReturnFlags(method));

        if (method.containingType.specialType == SpecialType.Nullable)
            writer.Write((byte)0);
        else
            writer.Write(CreateTypeKindAndInfo(method.returnType));

        writer.Write((ushort)method.parameterCount);

        foreach (var parameter in method.parameters) {
            Debug.Assert((uint)parameter.metadataName.Length == Encoding.UTF8.GetBytes(parameter.metadataName).Length);
            writer.Write((uint)parameter.metadataName.Length);
            writer.Write(Encoding.UTF8.GetBytes(parameter.metadataName));
            writer.Write(CreateParameterFlags(parameter));
            writer.Write(CreateParameterAttributes(parameter));
            writer.Write(CreateTypeKindAndInfo(parameter.type));

            if (parameter.explicitDefaultConstantValue is not null || parameter.outDefaultValue is not null) {
                Debug.Assert(parameter.explicitDefaultConstantValue is null || parameter.outDefaultValue is null);

                if (parameter.explicitDefaultConstantValue is not null)
                    WriteConstantValueValue(writer, parameter.explicitDefaultConstantValue);
                else
                    WriteConstantValueValue(writer, parameter.outDefaultValue);
            }
        }

        writer.Write((ushort)method.templateConstraints.Length);

        foreach (var constraint in method.templateConstraints) {
            var ir = CreateIR(constraint);
            writer.Write((uint)ir.Length + 4);
            writer.Write(ir);
        }

        writer.BaseStream.Seek(0, SeekOrigin.Begin);
        writer.Write((uint)writer.BaseStream.Length);

        _methodTableCount++;
        _methodTableSize += (uint)writer.BaseStream.Length;

        var index = (uint)_methodTableIndexes.Count;
        _methodTableIndexes.Add(method, index);

        _methodEntries.Add(method, stream.ToArray());

        return index;
    }

    private static void WriteConstantValueValue(BinaryWriter writer, ConstantValue constant) {
        Debug.Assert(constant.value is not null);

        switch (constant.specialType) {
            case SpecialType.String:
                var stringValue = Encoding.UTF8.GetBytes((string)constant.value);
                writer.Write((uint)stringValue.Length);
                writer.Write(stringValue);
                break;
            case SpecialType.Bool:
                writer.Write((bool)constant.value);
                break;
            case SpecialType.WinBool:
                writer.Write((int)constant.value);
                break;
            case SpecialType.Char:
                writer.Write((char)constant.value);
                break;
            case SpecialType.Int8:
                writer.Write((sbyte)constant.value);
                break;
            case SpecialType.UInt8:
                writer.Write((byte)constant.value);
                break;
            case SpecialType.Int16:
                writer.Write((short)constant.value);
                break;
            case SpecialType.UInt16:
                writer.Write((ushort)constant.value);
                break;
            case SpecialType.Int32:
                writer.Write((int)constant.value);
                break;
            case SpecialType.UInt32:
                writer.Write((uint)constant.value);
                break;
            case SpecialType.Int:
            case SpecialType.Int64:
                writer.Write((long)constant.value);
                break;
            case SpecialType.UInt64:
                writer.Write((ulong)constant.value);
                break;
            case SpecialType.Float32:
                writer.Write((float)constant.value);
                break;
            case SpecialType.Decimal:
            case SpecialType.Float64:
                writer.Write((double)constant.value);
                break;
            case SpecialType.IntPtr:
                writer.Write((IntPtr)constant.value);
                break;
            case SpecialType.UIntPtr:
                writer.Write((UIntPtr)constant.value);
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(constant.specialType);
        }
    }

    private byte[] CreateIR(BoundNode node) {
        var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);

        BoundNodeEncoder.Encode(node, writer, this);

        return stream.ToArray();
    }

    private void Write(Stream stream) {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        // Header

        const uint HeaderSize = 12;

        writer.Write("BLTM"u8);
        writer.Write(MajorVersion);
        writer.Write(MinorVersion);
        writer.Write(HeaderSize);

        // Assembly Table

        writer.Write(_assemblyTableSize);
        writer.Write(_assemblyTableCount);

        foreach (var (assemblyIdentity, _) in _assemblyTableIndexes) {
            var displayName = assemblyIdentity.GetDisplayName(fullKey: true);

            var entrySize =
                4 +
                (uint)displayName.Length;

            writer.Write(entrySize);
            Debug.Assert((uint)displayName.Length == Encoding.UTF8.GetBytes(displayName).Length);
            writer.Write(Encoding.UTF8.GetBytes(displayName));
        }

        // Type Table

        writer.Write(_typeTableSize);
        writer.Write(_typeTableCount);

        foreach (var (type, _) in _typeTableIndexes) {
            var entrySize =
                4 +
                4 +
                (uint)type.metadataName.Length +
                2 +
                1 +
                4 +
                (uint)(type.containingNamespace?.metadataName?.Length ?? 0) +
                4 +
                1 +
                4;

            writer.Write(entrySize);
            Debug.Assert((uint)type.metadataName.Length == Encoding.UTF8.GetBytes(type.metadataName).Length);
            writer.Write((uint)type.metadataName.Length);
            writer.Write(Encoding.UTF8.GetBytes(type.metadataName));
            writer.Write((ushort)type.GetArity());
            writer.Write(CreateTypeFlags(type));

            if (type.containingNamespace is not null) {
                Debug.Assert((uint)type.containingNamespace.metadataName.Length == Encoding.UTF8.GetBytes(type.containingNamespace.metadataName).Length);
                writer.Write((uint)type.containingNamespace.metadataName.Length);
                writer.Write(Encoding.UTF8.GetBytes(type.containingNamespace.metadataName));
            } else {
                writer.Write((uint)0);
            }

            if (type.containingAssembly is null) {
                // We put a bogus value here to ensure the reader doesn't attempt to read the assembly
                // We just "assume" that there will never be this many assemblies
                Debug.Assert(0xFFFFFFFF > _assemblyTableCount);
                writer.Write(0xFFFFFFFF);
            } else {
                writer.Write(_assemblyTableIndexes[type.containingAssembly.identity]);
            }

            if (type.containingSymbol is TypeSymbol containingType) {
                writer.Write(true);
                writer.Write(_typeTableIndexes[containingType]);
            } else {
                writer.Write(false);
                writer.Write((uint)0);
            }
        }

        // Method Table

        writer.Write(_methodTableSize);
        writer.Write(_methodTableCount);

        foreach (var methodEntry in _methodEntries)
            writer.Write(methodEntry.Value);

        // Type Definition Table

        writer.Write(_typeDefinitionTableSize);
        writer.Write(_typeDefinitionTableCount);

        foreach (var typeDefEntry in _typeDefEntries)
            writer.Write(typeDefEntry.Value);

        // Template Table

        writer.Write(_templateTableSize);
        writer.Write(_templateTableCount);

        foreach (var (type, firstBoundEntry, boundEntryCount) in _templatesNeedingEntries) {
            var entrySize = 4 + 2 + 4 + 4 + 4 * boundEntryCount;
            writer.Write(entrySize);
            writer.Write((ushort)0); // No flags currently
            writer.Write(_typeTableIndexes[type]);
            writer.Write(boundEntryCount);

            for (uint j = 0; j < boundEntryCount; j++)
                writer.Write(j + firstBoundEntry);
        }

        // Bound Table

        writer.Write(_boundTableSize);
        writer.Write(_boundTableCount);

        foreach (var entry in _boundEntries) {
            writer.Write((uint)entry.Item2.Length + 8);
            writer.Write(_methodTableIndexes[entry.Item1]);
            writer.Write(entry.Item2);
        }
    }

    private uint GetMethodAttributes(MethodSymbol method) {
        var flags = (uint)Executor.GetMethodAttributes(method);

        // Just the flags not covered by the Executor
        // This just includes constructor related ones that are automatically added by the reflection API

        if (method.hasSpecialName)
            flags |= (uint)MethodAttributes.SpecialName;

        if (method.hasRuntimeSpecialName)
            flags |= (uint)MethodAttributes.RTSpecialName;

        return flags;
    }
}
