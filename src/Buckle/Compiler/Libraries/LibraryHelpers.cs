using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
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
    internal static readonly CompilationOptions LibraryOptions
        = new CompilationOptions(BuildMode.None, OutputKind.DynamicallyLinkedLibrary);

    private static SpecialOrKnownType.Boxed _lazyStringList;
    private static SpecialOrKnownType.Boxed _lazyStringArray;

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

    /// <summary>
    /// Creates a compilation containing all of the built-in libraries.
    /// </summary>
    public static Compilation LoadLibraries(BuildMode buildMode = BuildMode.None) {
        var assembly = Assembly.GetExecutingAssembly();
        var syntaxTrees = new List<SyntaxTree>();

        foreach (var libraryName in assembly.GetManifestResourceNames()) {
            if (libraryName.StartsWith("Compiler.Resources"))
                continue;

            // TODO Remove this, temp
            if (libraryName != "Compiler.Object.blt" &&
                libraryName != "Compiler.Vec2.blt" &&
                libraryName != "Compiler.Text.blt" &&
                libraryName != "Compiler.Rect.blt" &&
                libraryName != "Compiler.Texture.blt" &&
                libraryName != "Compiler.FRect.blt" &&
                libraryName != "Compiler.Vec4.blt" &&
                libraryName != "Compiler.Sound.blt" &&
                libraryName != "Compiler.Sprite.blt") {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(libraryName);
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd().TrimEnd();

            var syntaxTree = SyntaxTree.Load(libraryName, text);
            syntaxTrees.Add(syntaxTree);
        }

        var options = new CompilationOptions(buildMode, LibraryOptions.outputKind);
        var corLibrary = Compilation.Create("CorLibrary", options, syntaxTrees.ToArray());
        corLibrary.GetDiagnostics();

        return corLibrary;
    }

    internal static void DeclareLibrariesInNamespace(
        PooledDictionary<ReadOnlyMemory<char>, object> builder,
        CompilationOptions options) {
        AddTypesToBuilder(StandardLibrary.GetTypes());

        // TODO Consider separating OutputKind from ProjectType
        if (options.outputKind == OutputKind.GraphicsApplication)
            AddTypesToBuilder(GraphicsLibrary.GetTypes());

        void AddTypesToBuilder(IEnumerable<NamedTypeSymbol> types) {
            foreach (var type in types) {
                CodeAnalysis.ImmutableArrayExtensions.AddToMultiValueDictionaryBuilder(
                    builder,
                    type.name.AsMemory(),
                    type
                );
            }
        }
    }

    internal static string BuildMapKey(MethodSymbol method) {
        var stringBuilder = new StringBuilder();
        stringBuilder.Append(method.containingType.name);
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

        static char GetNameCharacter(TypeSymbol type) {
            if (type.typeKind == TypeKind.Array)
                return 'A';

            return char.ToUpper(type.name.First());
        }
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
            DeclarationModifiers.Public | modifiers
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

        return new SynthesizedFinishedNamedTypeSymbol(namedType, null, builder.ToImmutableAndFree());
    }

    internal static SynthesizedFinishedMethodSymbol StaticMethod(string name, SpecialOrKnownType type) {
        return Method(name, type, false, [], DeclarationModifiers.Static);
    }

    internal static SynthesizedFinishedMethodSymbol StaticMethod(
        string name,
        SpecialOrKnownType type,
        IEnumerable<(string name, SpecialOrKnownType type)> parameters) {
        return Method(
            name,
            type,
            false,
            parameters.Select<(string name, SpecialOrKnownType type),
                              (string, SpecialOrKnownType, bool, object, RefKind)>(
                p => (p.name, p.type, false, null, RefKind.None)
            ),
            DeclarationModifiers.Static
        );
    }

    internal static SynthesizedFinishedMethodSymbol StaticMethod(
        string name,
        SpecialOrKnownType type,
        bool isNullable,
        IEnumerable<(string name, SpecialOrKnownType type)> parameters) {
        return Method(
            name,
            type,
            isNullable,
            parameters.Select<(string name, SpecialOrKnownType type),
                              (string, SpecialOrKnownType, bool, object, RefKind)>(
                p => (p.name, p.type, false, null, RefKind.None)
            ),
            DeclarationModifiers.Static
        );
    }

    internal static SynthesizedFinishedMethodSymbol StaticMethod(
        string name,
        SpecialOrKnownType type,
        IEnumerable<(string name, SpecialOrKnownType type, bool isNullable)> parameters) {
        return Method(
            name,
            type,
            false,
            parameters.Select<(string name, SpecialOrKnownType type, bool isNullable),
                              (string, SpecialOrKnownType, bool, object, RefKind)>(
                p => (p.name, p.type, p.isNullable, null, RefKind.None)
            ),
            DeclarationModifiers.Static
        );
    }

    internal static SynthesizedFinishedMethodSymbol StaticMethod(
        string name,
        SpecialOrKnownType type,
        IEnumerable<(string name, SpecialOrKnownType type, bool isNullable, object defaultValue)> parameters) {
        return Method(
            name,
            type,
            false,
            parameters.Select<(string name, SpecialOrKnownType type, bool isNullable, object defaultValue),
                              (string, SpecialOrKnownType, bool, object, RefKind)>(
                p => (p.name, p.type, p.isNullable, p.defaultValue, RefKind.None)
            ),
            DeclarationModifiers.Static
        );
    }

    internal static SynthesizedFinishedMethodSymbol StaticMethod(
        string name,
        SpecialOrKnownType type,
        bool isNullable,
        IEnumerable<(string name, SpecialOrKnownType type, bool isNullable)> parameters) {
        return Method(
            name,
            type,
            isNullable,
            parameters.Select<(string name, SpecialOrKnownType type, bool isNullable),
                              (string, SpecialOrKnownType, bool, object, RefKind)>(
                p => (p.name, p.type, p.isNullable, null, RefKind.None)
            ),
            DeclarationModifiers.Static
        );
    }

    internal static SynthesizedFinishedMethodSymbol Method(
        string name,
        SpecialOrKnownType type,
        bool isNullable,
        IEnumerable<(string name, SpecialOrKnownType type, bool isNullable, object defaultValue)> parameters) {
        return Method(
            name,
            type,
            isNullable,
            parameters.Select<(string name, SpecialOrKnownType type, bool isNullable, object defaultValue),
                              (string, SpecialOrKnownType, bool, object, RefKind)>(
                p => (p.name, p.type, p.isNullable, p.defaultValue, RefKind.None)
            ),
            DeclarationModifiers.None
        );
    }

    internal static SynthesizedFinishedMethodSymbol Method(
        string name,
        SpecialOrKnownType type,
        IEnumerable<(string name, SpecialOrKnownType type, bool isNullable)> parameters) {
        return Method(
            name,
            type,
            false,
            parameters.Select<(string name, SpecialOrKnownType type, bool isNullable),
                              (string, SpecialOrKnownType, bool, object, RefKind)>(
                p => (p.name, p.type, p.isNullable, null, RefKind.None)
            ),
            DeclarationModifiers.None
        );
    }

    internal static SynthesizedFinishedMethodSymbol Method(
        string name,
        SpecialOrKnownType type,
        bool isNullable,
        IEnumerable<(string name, SpecialOrKnownType type, bool isNullable)> parameters) {
        return Method(
            name,
            type,
            isNullable,
            parameters.Select<(string name, SpecialOrKnownType type, bool isNullable),
                              (string, SpecialOrKnownType, bool, object, RefKind)>(
                p => (p.name, p.type, p.isNullable, null, RefKind.None)
            ),
            DeclarationModifiers.None
        );
    }

    internal static SynthesizedFinishedMethodSymbol Method(
        string name,
        SpecialOrKnownType type,
        IEnumerable<(string name, SpecialOrKnownType type)> parameters) {
        return Method(
            name,
            type,
            false,
            parameters.Select<(string name, SpecialOrKnownType type),
                              (string, SpecialOrKnownType, bool, object, RefKind)>(
                p => (p.name, p.type, false, null, RefKind.None)
            ),
            DeclarationModifiers.None
        );
    }

    internal static SynthesizedFinishedMethodSymbol Method(
        string name,
        SpecialOrKnownType type,
        bool isNullable,
        IEnumerable<(string name, SpecialOrKnownType type, bool isNullable, object defaultValue, RefKind refKind)> parameters,
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
        var i = 0;

        foreach (var parameter in parameters) {
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
            i++;
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
            ArrayTypeSymbol.CreateSZArray(new TypeWithAnnotations(CorLibrary.GetNullableType(elementType)))
        );
    }
}
