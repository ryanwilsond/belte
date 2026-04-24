using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.Libraries;

public static class LibraryHelpers {
    private static readonly string[] ReducedStdLibFiles = [
        "Compiler.Object.blt",
        "Compiler.ReducedEnumerator.blt",
        "Compiler.Exception.blt"
    ];

    private static readonly string[] ReducedStdLibExclude = [
        "Compiler.Enumerator.blt"
    ];

    private static readonly string[] StdLibExclude = [
        "Compiler.ReducedEnumerator.blt"
    ];

    private static SynthesizedBelteNamespaceSymbol _belteNamespace;
    private static SpecialOrKnownType.Boxed _lazyStringList;
    private static SpecialOrKnownType.Boxed _lazyStringArray;
    private static SpecialOrKnownType.Boxed _lazyAnyArray;
    private static SpecialOrKnownType.Boxed _lazyCharArray;

    internal static NamespaceSymbol BelteNamespace => _belteNamespace;

    internal static SpecialOrKnownType CharArray {
        get {
            if (_lazyCharArray is null)
                Interlocked.CompareExchange(ref _lazyCharArray, GenerateArray(SpecialType.Char), null);

            return _lazyCharArray.type;
        }
    }

    internal static SpecialOrKnownType StringList {
        get {
            if (_lazyStringList is null)
                Interlocked.CompareExchange(ref _lazyStringList, GenerateStringList(), null);

            return _lazyStringList.type;
        }
    }

    internal static SpecialOrKnownType StringArray {
        get {
            if (_lazyStringArray is null)
                Interlocked.CompareExchange(ref _lazyStringArray, GenerateArray(SpecialType.String), null);

            return _lazyStringArray.type;
        }
    }

    internal static SpecialOrKnownType AnyArray {
        get {
            if (_lazyAnyArray is null)
                Interlocked.CompareExchange(ref _lazyAnyArray, GenerateArray(SpecialType.Any), null);

            return _lazyAnyArray.type;
        }
    }

    /// <summary>
    /// Creates a compilation containing all of the built-in libraries.
    /// </summary>
    public static Compilation LoadLibraries(
        BuildMode buildMode = BuildMode.None,
        bool concurrentBuild = false,
        int maxCoreCount = 1,
        bool reducedStdLib = false) {
        var assembly = Assembly.GetExecutingAssembly();
        var syntaxTrees = new List<SyntaxTree>();

        foreach (var libraryName in assembly.GetManifestResourceNames()) {
            if (libraryName.StartsWith("Compiler.Resources"))
                continue;

            if (!libraryName.EndsWith(".blt"))
                continue;

            if (reducedStdLib) {
                if (!ReducedStdLibFiles.Contains(libraryName) || ReducedStdLibExclude.Contains(libraryName))
                    continue;
            } else {
                if (StdLibExclude.Contains(libraryName))
                    continue;
            }

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
            noStdLib: reducedStdLib
        );

        if (reducedStdLib)
            CorLibrary.SetReducedState();

        var corLibrary = Compilation.Create("CorLibrary", options, syntaxTrees.ToArray());
        CreateBelteNamespace(reducedStdLib);
        corLibrary = corLibrary.AddNamespace(BelteNamespace);
        corLibrary.GetDiagnostics();

        return corLibrary;
    }

    internal static string BuildMapKey(MethodSymbol method) {
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

            return char.ToUpper(type.name[0]).ToString();
        }
    }

    private static void CreateBelteNamespace(bool reducedStdLib) {
        _belteNamespace = new SynthesizedBelteNamespaceSymbol("Belte", reducedStdLib);
    }

    internal static SynthesizedFieldSymbol ConstExprField(string name, SpecialOrKnownType type, object constantValue) {
        return new SynthesizedFieldSymbol(null, type.knownType, name, true, false, true, true, true, constantValue);
    }

    internal static SynthesizedFinishedNamedTypeSymbol StaticClass(string name, ImmutableArray<Symbol> members) {
        return Class(name, members, DeclarationModifiers.Static);
    }

    internal static SynthesizedFinishedNamedTypeSymbol Class(
        string name,
        ImmutableArray<Symbol> members,
        DeclarationModifiers modifiers) {
        var namedType = new SynthesizedSimpleNamedTypeSymbol(
            name,
            TypeKind.Class,
            CorLibrary.GetSpecialType(SpecialType.Object),
            DeclarationModifiers.Public | modifiers,
            BelteNamespace,
            []
        );

        var builder = ArrayBuilder<Symbol>.GetInstance();

        foreach (var member in members) {
            switch (member) {
                case MethodSymbol method:
                    builder.Add(new SynthesizedFinishedMethodSymbol(method, namedType, null));
                    break;
                case NamedTypeSymbol type:
                    builder.Add(new SynthesizedFinishedNamedTypeSymbol(type, namedType, null));
                    break;
                case FieldSymbol field:
                    builder.Add(new SynthesizedFieldSymbol(
                        namedType,
                        field.type,
                        field.name,
                        field.declaredAccessibility == Accessibility.Public,
                        field.isConst,
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

        return new SynthesizedFinishedNamedTypeSymbol(namedType, BelteNamespace, builder.ToImmutableAndFree());
    }

    internal static SynthesizedFinishedMethodSymbol StaticMethod(string name, SpecialOrKnownType type) {
        return Method(name, type, false, [], DeclarationModifiers.Static);
    }

    internal static SynthesizedFinishedMethodSymbol StaticMethod(
        string name,
        SpecialOrKnownType type,
        (string name, SpecialOrKnownType type)[] parameters) {
        var length = parameters.Length;
        var result = new (string, SpecialOrKnownType, bool, object, RefKind)[length];

        for (var i = 0; i < length; i++) {
            var p = parameters[i];
            result[i] = (p.name, p.type, false, null, RefKind.None);
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
        var result = new (string, SpecialOrKnownType, bool, object, RefKind)[length];

        for (var i = 0; i < length; i++) {
            var p = parameters[i];
            result[i] = (p.name, p.type, false, null, RefKind.None);
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
        var result = new (string, SpecialOrKnownType, bool, object, RefKind)[length];

        for (var i = 0; i < length; i++) {
            var p = parameters[i];
            result[i] = (p.name, p.type, p.isNullable, null, RefKind.None);
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
        var result = new (string, SpecialOrKnownType, bool, object, RefKind)[length];

        for (var i = 0; i < length; i++) {
            var p = parameters[i];
            result[i] = (p.name, p.type, p.isNullable, p.defaultValue, RefKind.None);
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
        var result = new (string, SpecialOrKnownType, bool, object, RefKind)[length];

        for (var i = 0; i < length; i++) {
            var p = parameters[i];
            result[i] = (p.name, p.type, p.isNullable, null, RefKind.None);
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
        (string name, SpecialOrKnownType type, bool isNullable, object defaultValue)[] parameters) {
        var length = parameters.Length;
        var result = new (string, SpecialOrKnownType, bool, object, RefKind)[length];

        for (var i = 0; i < length; i++) {
            var p = parameters[i];
            result[i] = (p.name, p.type, p.isNullable, p.defaultValue, RefKind.None);
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
        (string name, SpecialOrKnownType type, bool isNullable)[] parameters) {
        var length = parameters.Length;
        var result = new (string, SpecialOrKnownType, bool, object, RefKind)[length];

        for (var i = 0; i < length; i++) {
            var p = parameters[i];
            result[i] = (p.name, p.type, p.isNullable, null, RefKind.None);
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
        (string name, SpecialOrKnownType type, bool isNullable)[] parameters) {
        var length = parameters.Length;
        var result = new (string, SpecialOrKnownType, bool, object, RefKind)[length];

        for (var i = 0; i < length; i++) {
            var p = parameters[i];
            result[i] = (p.name, p.type, p.isNullable, null, RefKind.None);
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
        var result = new (string, SpecialOrKnownType, bool, object, RefKind)[length];

        for (var i = 0; i < length; i++) {
            var p = parameters[i];
            result[i] = (p.name, p.type, false, null, RefKind.None);
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
        (string name, SpecialOrKnownType type, bool isNullable, object defaultValue, RefKind refKind)[] parameters,
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
                defaultValue: constantValue
            );

            builder.Add(synthesizedParameter);
        }

        return new SynthesizedFinishedMethodSymbol(method, null, builder.ToImmutableAndFree());
    }

    private static SpecialOrKnownType.Boxed GenerateStringList() {
        return new SpecialOrKnownType.Boxed(new ConstructedNamedTypeSymbol(
            CorLibrary.GetSpecialType(SpecialType.List),
            [new TypeOrConstant(CorLibrary.GetSpecialType(SpecialType.String))]
        ));
    }

    private static SpecialOrKnownType.Boxed GenerateArray(SpecialType elementType) {
        return new SpecialOrKnownType.Boxed(
            ArrayTypeSymbol.CreateSZArray(new TypeWithAnnotations(CorLibrary.GetSpecialType(elementType)))
        );
    }
}
