using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;
using static Buckle.CodeAnalysis.Display.DisplayTextSegment;

namespace Buckle.CodeAnalysis.Display;

/// <summary>
/// Extensions on the <see cref="Compilation" /> class, adding the ability to emit the tree to a display.
/// </summary>
public static class CompilationExtensions {
    /// <summary>
    /// Emits the parse tree of the compilation.
    /// </summary>
    /// <param name="text">Out.</param>
    public static void EmitTree(this Compilation compilation, DisplayText text) {
        var program = compilation.boundProgram;
        var isFirst = true;

        foreach (var type in program.types) {
            if (isFirst)
                isFirst = false;
            else
                text.WriteLine();

            EmitTree(compilation, type, text);
        }

        foreach (var type in program.nestedTypes) {
            foreach (var nestedType in type.Value) {
                if (isFirst)
                    isFirst = false;
                else
                    text.WriteLine();

                EmitTree(compilation, nestedType, text);
            }
        }

        foreach (var pair in program.methodBodies) {
            if (isFirst)
                isFirst = false;
            else
                text.WriteLine();

            EmitTree(compilation, pair.Key, text);
        }
    }

    /// <summary>
    /// Emits the parse tree of a single <see cref="Symbol" />.
    /// </summary>
    /// <param name="symbol"><see cref="Symbol" /> to be the root of the <see cref="SyntaxTree" /> displayed.</param>
    /// <param name="text">Out.</param>
    public static void EmitTree(this Compilation compilation, ISymbol symbol, DisplayText text) {
        var program = compilation.boundProgram;
        EmitTree(symbol, text, program);
    }

    public static ImmutableArray<IDataContainerSymbol> GetMethodLocals(IMethodSymbol method) {
        if (method is SourceMemberMethodSymbol s) {
            return s.TryGetBodyBinder().next.locals.Where((x, i) => i % 2 == 0)
                .ToImmutableArray().CastArray<IDataContainerSymbol>();
        } else {
            return [];
        }
    }

    internal static void EmitTree(
        ISymbol symbol,
        DisplayText text,
        BoundProgram program,
        bool compact = false) {
        switch (symbol.kind) {
            case SymbolKind.Namespace: {
                    var ns = (NamespaceSymbol)symbol;
                    SymbolDisplay.AppendToDisplayText(text, symbol, SymbolDisplayFormat.Everything);
                    WriteMembers(ns, compact);
                }

                break;
            case SymbolKind.NamedType: {
                    var type = (NamedTypeSymbol)symbol;
                    SymbolDisplay.AppendToDisplayText(text, symbol, SymbolDisplayFormat.Everything);
                    WriteMembers(type, compact);
                }

                break;
            case SymbolKind.Method:
                var method = (MethodSymbol)symbol;
                SymbolDisplay.AppendToDisplayText(text, method, SymbolDisplayFormat.BoundDisplayFormat);

                if (!method.isAbstract && !method.isExtern &&
                    program.TryGetMethodBodyIncludingParents(method, out var body, useOriginalDefinitions: true)) {
                    text.Write(CreateSpace());
                    DisplayText.DisplayNode(text, body);
                } else {
                    text.Write(CreatePunctuation(SyntaxKind.SemicolonToken));
                    text.WriteLine();
                }

                break;
            case SymbolKind.Field: {
                    var field = (FieldSymbol)symbol;
                    SymbolDisplay.AppendToDisplayText(text, field, SymbolDisplayFormat.BoundDisplayFormat);
                    var type = field.type.StrippedType();

                    if (type is NamedTypeSymbol s && s is not PrimitiveTypeSymbol)
                        WriteMembers(s, compact);
                    else
                        text.WriteLine();
                }

                break;
            case SymbolKind.Local: {
                    var local = (DataContainerSymbol)symbol;
                    SymbolDisplay.AppendToDisplayText(text, local, SymbolDisplayFormat.BoundDisplayFormat);
                    var type = local.type.StrippedType();

                    if (type is NamedTypeSymbol s && s is not PrimitiveTypeSymbol)
                        WriteMembers(s, compact);
                    else
                        text.WriteLine();
                }

                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(symbol.kind);
        }

        void WriteMembers(NamespaceOrTypeSymbol typeOrNamespace, bool compact = false) {
            var members = typeOrNamespace.GetMembers();

            text.Write(CreateSpace());
            text.Write(CreatePunctuation(SyntaxKind.OpenBraceToken));
            text.Write(CreateSpace());
            text.indent++;

            if (typeOrNamespace is SourceNamedTypeSymbol n && n.typeKind == TypeKind.Enum) {
                text.WriteLine();
                SymbolDisplay.AppendToDisplayText(
                    text,
                    n.enumValueField,
                    SymbolDisplayFormat.CompactBoundDisplayFormat
                );

                if (!compact)
                    text.WriteLine();
            }

            foreach (var member in members) {
                if (compact && member.kind == SymbolKind.Method)
                    continue;

                text.WriteLine();
                SymbolDisplay.AppendToDisplayText(text, member, SymbolDisplayFormat.CompactBoundDisplayFormat);

                if (member is FieldSymbol f && f.hasConstantValue) {
                    text.Write(CreateSpace());
                    text.Write(CreatePunctuation(SyntaxKind.EqualsToken));
                    text.Write(CreateSpace());
                    DisplayText.DisplayConstant(text, f.constantValue);
                }

                if (!compact)
                    text.WriteLine();
            }

            text.indent--;

            if (compact)
                text.WriteLine();

            text.Write(CreatePunctuation(SyntaxKind.CloseBraceToken));
            text.WriteLine();
        }
    }
}
