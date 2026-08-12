using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.Libraries;

public static class LibraryHelpers {
    private static readonly string[] NativeSources = [
        "Compiler.Object.blt",
        "Compiler.Buffer.blt"
    ];

    /// <summary>
    /// Creates a compilation containing all of the built-in libraries.
    /// </summary>
    public static Compilation LoadLibraries(
        BuildMode buildMode = BuildMode.None,
        bool concurrentBuild = false,
        int maxCoreCount = 1,
        bool noStdLib = false,
        int explicitLibraryLevel = 0,
        bool includeAllNativeFiles = false) {
        var assembly = Assembly.GetExecutingAssembly();
        var syntaxTrees = new List<SyntaxTree>();

        foreach (var libraryName in assembly.GetManifestResourceNames()) {
            if (libraryName.StartsWith("Compiler.Resources"))
                continue;

            if (!libraryName.EndsWith(".blt"))
                continue;

            if (!buildMode.Evaluating() && !NativeSources.Contains(libraryName) && !includeAllNativeFiles)
                continue;

            using var stream = assembly.GetManifestResourceStream(libraryName);
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd().TrimEnd();

            var syntaxTree = SyntaxTree.Load(libraryName, text, null);
            syntaxTrees.Add(syntaxTree);
        }

        var options = new CompilationOptions(
            buildMode,
            OutputKind.DynamicallyLinkedLibrary,
            concurrentBuild: concurrentBuild,
            maxCoreCount: maxCoreCount,
            noStdLib: noStdLib,
            // When Evaluating we recompile the standard library from source to have method bodies available
            references: Compiler.ResolveLibraryLevel(explicitLibraryLevel, noStdLib || buildMode.Evaluating())
        );

        var corLibraryCompilation = Compilation.Create(
            "CorLibrary",
            options,
            syntaxTrees.ToArray()
        );

        var belteNamespace = CreateBelteNamespace(corLibraryCompilation, noStdLib);
        var updatedCorLibraryCompilation = corLibraryCompilation.AddNamespace(belteNamespace);
        updatedCorLibraryCompilation.corLibrary.SetBelteNamespace(belteNamespace);

        updatedCorLibraryCompilation.GetDiagnostics();

        return updatedCorLibraryCompilation;
    }

    internal static SpecialOrKnownType GetCharBuffer(Compilation compilation) {
        Debug.Assert(compilation is not null);
        return GenerateArray(compilation, SpecialType.Char).type;
    }

    internal static SpecialOrKnownType GetStringBuffer(Compilation compilation) {
        Debug.Assert(compilation is not null);
        return GenerateArray(compilation, SpecialType.String).type;
    }

    internal static string BuildMapKey(MethodSymbol method) {
        method = method.originalDefinition;
        var containingType = method.containingType;

        var stringBuilder = new StringBuilder();
        stringBuilder.Append(containingType.name);

        if (containingType.specialType != SpecialType.None)
            stringBuilder.Append("<>");

        stringBuilder.Append('_');
        stringBuilder.Append(method.name);

        if (method.parameterCount > 0) {
            stringBuilder.Append('_');

            foreach (var parameter in method.parameters) {
                var type = parameter.type;

                if (type.specialType == SpecialType.Nullable) {
                    stringBuilder.Append(GetNameCharacter(type.GetNullableUnderlyingType()));
                    stringBuilder.Append('?');
                } else {
                    stringBuilder.Append(GetNameCharacter(type));
                }
            }
        }

        return stringBuilder.ToString();

        static string GetNameCharacter(TypeSymbol type) {
            if (type.typeKind == TypeKind.Array)
                return "[";

            if (type is PointerTypeSymbol ptr)
                return char.ToUpper(ptr.pointedAtType.name[0]).ToString() + "*";

            if (type is FunctionPointerTypeSymbol)
                return "F";

            if (type is FunctionTypeSymbol)
                return "Fn";

            var chr = char.ToUpper(type.name[0]);

            if (type.specialType.IsLowLevelNumeric())
                return chr + type.specialType.SizeInBytes().ToString();

            return chr.ToString();
        }
    }

    private static SynthesizedBelteNamespaceSymbol CreateBelteNamespace(Compilation compilation, bool noStdLib) {
        return new SynthesizedBelteNamespaceSymbol(compilation, "Belte", noStdLib);
    }

    internal static SynthesizedFieldSymbol ConstExprField(string name, SpecialOrKnownType type, object constantValue) {
        return new SynthesizedFieldSymbol(
            null,
            type.knownType,
            name,
            isPublic: true,
            isConst: false,
            isFinal: false,
            isConstExpr: true,
            isStatic: true,
            hasConstantValue: true,
            constantValue
        );
    }

    internal static SynthesizedFinishedNamedTypeSymbol Class(
        Compilation compilation,
        string name,
        ImmutableArray<Symbol> members) {
        return Class(compilation, name, members, DeclarationModifiers.None);
    }

    internal static SynthesizedFinishedNamedTypeSymbol Class(
        Compilation compilation,
        string name,
        NamedTypeSymbol baseType,
        ImmutableArray<Symbol> members) {
        return Class(compilation, name, members, DeclarationModifiers.None, baseType);
    }

    internal static SynthesizedFinishedNamedTypeSymbol StaticClass(
        Compilation compilation,
        string name,
        ImmutableArray<Symbol> members) {
        return Class(compilation, name, members, DeclarationModifiers.Static);
    }

    internal static SynthesizedFinishedNamedTypeSymbol Class(
        Compilation compilation,
        string name,
        ImmutableArray<Symbol> members,
        DeclarationModifiers modifiers,
        NamedTypeSymbol baseType = null) {
        Debug.Assert(compilation.corLibrary.belteNamespace is not null);

        var namedType = new SynthesizedSimpleNamedTypeSymbol(
            name,
            TypeKind.Class,
            baseType ?? compilation.GetSpecialType(SpecialType.Object),
            DeclarationModifiers.Public | modifiers,
            compilation.corLibrary.belteNamespace,
            []
        );

        var builder = ArrayBuilder<Symbol>.GetInstance();

        foreach (var member in members) {
            switch (member) {
                case MethodSymbol method:
                    builder.Add(new SynthesizedFinishedMethodSymbol(method, namedType, default));
                    break;
                case NamedTypeSymbol type:
                    builder.Add(new SynthesizedFinishedNamedTypeSymbol(type, namedType, default));
                    break;
                case FieldSymbol field:
                    builder.Add(new SynthesizedFieldSymbol(
                        namedType,
                        field.type,
                        field.name,
                        field.declaredAccessibility == Accessibility.Public,
                        field.isConst,
                        field.isFinal,
                        field.isConstExpr,
                        field.isStatic,
                        field.hasConstantValue,
                        field.constantValue
                    ));
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(member.kind);
            }
        }

        return new SynthesizedFinishedNamedTypeSymbol(
            namedType,
            compilation.corLibrary.belteNamespace,
            builder.ToImmutableAndFree()
        );
    }

    internal static SynthesizedFinishedMethodSymbol StaticMethod(string name, SpecialOrKnownType type) {
        return Method(name, type, false, [], DeclarationModifiers.Static);
    }

    internal static SynthesizedFinishedMethodSymbol StaticMethod(
        string name,
        SpecialOrKnownType type,
        (string name, SpecialOrKnownType type)[] parameters) {
        var length = parameters.Length;
        var result = new (string, bool, SpecialOrKnownType, bool, object, RefKind)[length];

        for (var i = 0; i < length; i++) {
            var p = parameters[i];
            result[i] = (p.name, false, p.type, false, null, RefKind.None);
        }

        return Method(
            name,
            type,
            false,
            result,
            DeclarationModifiers.Static
        );
    }

    internal static SynthesizedFinishedMethodSymbol StaticMethod(
        string name,
        SpecialOrKnownType type,
        (string name, bool isConst, SpecialOrKnownType type)[] parameters) {
        var length = parameters.Length;
        var result = new (string, bool, SpecialOrKnownType, bool, object, RefKind)[length];

        for (var i = 0; i < length; i++) {
            var p = parameters[i];
            result[i] = (p.name, p.isConst, p.type, false, null, RefKind.None);
        }

        return Method(
            name,
            type,
            false,
            result,
            DeclarationModifiers.Static
        );
    }

    internal static SynthesizedFinishedMethodSymbol StaticMethod(
        string name,
        SpecialOrKnownType type,
        bool isNullable,
        (string name, SpecialOrKnownType type)[] parameters) {
        var length = parameters.Length;
        var result = new (string, bool, SpecialOrKnownType, bool, object, RefKind)[length];

        for (var i = 0; i < length; i++) {
            var p = parameters[i];
            result[i] = (p.name, false, p.type, false, null, RefKind.None);
        }

        return Method(
            name,
            type,
            isNullable,
            result,
            DeclarationModifiers.Static
        );
    }

    internal static SynthesizedFinishedMethodSymbol StaticMethod(
        string name,
        SpecialOrKnownType type,
        (string name, SpecialOrKnownType type, bool isNullable)[] parameters) {
        var length = parameters.Length;
        var result = new (string, bool, SpecialOrKnownType, bool, object, RefKind)[length];

        for (var i = 0; i < length; i++) {
            var p = parameters[i];
            result[i] = (p.name, false, p.type, p.isNullable, null, RefKind.None);
        }

        return Method(
            name,
            type,
            false,
            result,
            DeclarationModifiers.Static
        );
    }

    internal static SynthesizedFinishedMethodSymbol StaticMethod(
        string name,
        SpecialOrKnownType type,
        (string name, bool isConst, SpecialOrKnownType type, bool isNullable)[] parameters) {
        var length = parameters.Length;
        var result = new (string, bool, SpecialOrKnownType, bool, object, RefKind)[length];

        for (var i = 0; i < length; i++) {
            var p = parameters[i];
            result[i] = (p.name, p.isConst, p.type, p.isNullable, null, RefKind.None);
        }

        return Method(
            name,
            type,
            false,
            result,
            DeclarationModifiers.Static
        );
    }

    internal static SynthesizedFinishedMethodSymbol StaticMethod(
        string name,
        SpecialOrKnownType type,
        (string name, SpecialOrKnownType type, bool isNullable, object defaultValue)[] parameters) {
        var length = parameters.Length;
        var result = new (string, bool, SpecialOrKnownType, bool, object, RefKind)[length];

        for (var i = 0; i < length; i++) {
            var p = parameters[i];
            result[i] = (p.name, false, p.type, p.isNullable, p.defaultValue, RefKind.None);
        }

        return Method(
            name,
            type,
            false,
            result,
            DeclarationModifiers.Static
        );
    }

    internal static SynthesizedFinishedMethodSymbol StaticMethod(
        string name,
        SpecialOrKnownType type,
        bool isNullable,
        (string name, SpecialOrKnownType type, bool isNullable)[] parameters) {
        var length = parameters.Length;
        var result = new (string, bool, SpecialOrKnownType, bool, object, RefKind)[length];

        for (var i = 0; i < length; i++) {
            var p = parameters[i];
            result[i] = (p.name, false, p.type, p.isNullable, null, RefKind.None);
        }

        return Method(
            name,
            type,
            isNullable,
            result,
            DeclarationModifiers.Static
        );
    }

    internal static SynthesizedFinishedMethodSymbol Method(
        string name,
        SpecialOrKnownType type,
        bool isNullable,
        (string name, bool isConst, SpecialOrKnownType type, bool isNullable, object defaultValue)[] parameters) {
        var length = parameters.Length;
        var result = new (string, bool, SpecialOrKnownType, bool, object, RefKind)[length];

        for (var i = 0; i < length; i++) {
            var p = parameters[i];
            result[i] = (p.name, p.isConst, p.type, p.isNullable, p.defaultValue, RefKind.None);
        }

        return Method(
            name,
            type,
            isNullable,
            result,
            DeclarationModifiers.None
        );
    }

    internal static SynthesizedFinishedMethodSymbol Method(
        string name,
        SpecialOrKnownType type,
        (string name, bool isConst, SpecialOrKnownType type, bool isNullable)[] parameters) {
        var length = parameters.Length;
        var result = new (string, bool, SpecialOrKnownType, bool, object, RefKind)[length];

        for (var i = 0; i < length; i++) {
            var p = parameters[i];
            result[i] = (p.name, p.isConst, p.type, p.isNullable, null, RefKind.None);
        }

        return Method(
            name,
            type,
            false,
            result,
            DeclarationModifiers.None
        );
    }

    internal static SynthesizedFinishedMethodSymbol Method(
        string name,
        SpecialOrKnownType type,
        bool isNullable,
        (string name, bool isConst, SpecialOrKnownType type, bool isNullable)[] parameters) {
        var length = parameters.Length;
        var result = new (string, bool, SpecialOrKnownType, bool, object, RefKind)[length];

        for (var i = 0; i < length; i++) {
            var p = parameters[i];
            result[i] = (p.name, p.isConst, p.type, p.isNullable, null, RefKind.None);
        }

        return Method(
            name,
            type,
            isNullable,
            result,
            DeclarationModifiers.None
        );
    }

    internal static SynthesizedFinishedMethodSymbol Method(
        string name,
        SpecialOrKnownType type,
        (string name, SpecialOrKnownType type)[] parameters) {
        var length = parameters.Length;
        var result = new (string, bool, SpecialOrKnownType, bool, object, RefKind)[length];

        for (var i = 0; i < length; i++) {
            var p = parameters[i];
            result[i] = (p.name, false, p.type, false, null, RefKind.None);
        }

        return Method(
            name,
            type,
            false,
            result,
            DeclarationModifiers.None
        );
    }

    internal static SynthesizedFinishedMethodSymbol Method(
        string name,
        SpecialOrKnownType type,
        bool isNullable,
        (string name, bool isConst, SpecialOrKnownType type, bool isNullable, object defaultValue, RefKind refKind)[] parameters,
        DeclarationModifiers modifiers) {
        var returnTypeWithAnnotations = new TypeWithAnnotations(type.knownType);

        if (isNullable)
            returnTypeWithAnnotations = returnTypeWithAnnotations.SetIsAnnotated();

        var method = new SynthesizedSimpleOrdinaryMethodSymbol(
            name,
            returnTypeWithAnnotations,
            RefKind.None,
            DeclarationModifiers.Public | modifiers
        );

        var builder = ArrayBuilder<ParameterSymbol>.GetInstance();

        for (var i = 0; i < parameters.Length; i++) {
            var parameter = parameters[i];
            var parameterTypeWithAnnotations = new TypeWithAnnotations(parameter.type.knownType);

            if (parameter.isNullable)
                parameterTypeWithAnnotations = parameterTypeWithAnnotations.SetIsAnnotated();

            var constantValue = parameter.defaultValue is null
                ? null
                : new ConstantValue(parameter.defaultValue, parameter.type.specialType);

            var synthesizedParameter = SynthesizedParameterSymbol.Create(
                method,
                parameterTypeWithAnnotations,
                i,
                parameter.refKind,
                parameter.name,
                defaultValue: constantValue,
                isConst: parameter.isConst
            );

            builder.Add(synthesizedParameter);
        }

        return new SynthesizedFinishedMethodSymbol(method, null, builder.ToImmutableAndFree());
    }

    private static SpecialOrKnownType.Boxed GenerateArray(Compilation compilation, SpecialType elementType) {
        return new SpecialOrKnownType.Boxed(
            ArrayTypeSymbol.CreateSZArray(
                compilation.assembly,
                new TypeWithAnnotations(compilation.GetSpecialType(elementType))
            )
        );
    }
}
