using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using static Buckle.CodeAnalysis.Display.DisplayTextSegment;

namespace Buckle.CodeAnalysis.Display;

/// <summary>
/// Prints a <see cref="Symbol" />.
/// </summary>
public static class SymbolDisplay {
    /// <summary>
    /// Generates a rich text representation of a single <see cref="Symbol" />.
    /// </summary>
    /// <param name="symbol"><see cref="Symbol" /> to convert to rich text.</param>
    /// <returns>New <see cref="DisplayText" /> representing the <see cref="Symbol" />.</returns>
    internal static DisplayText DisplaySymbol(Symbol symbol) {
        var text = new DisplayText();
        DisplaySymbol(text, symbol);
        return text;
    }

    /// <summary>
    /// Adds a single <see cref="Symbol" /> to an existing <see cref="DisplayText" />.
    /// </summary>
    /// <param name="text"><see cref="DisplayText" /> to add to.</param>
    /// <param name="symbol"><see cref="Symbol" /> to add (not modified).</param>
    public static void DisplaySymbol(DisplayText text, ISymbol symbol, bool includeVariableTypes = false) {
        switch (symbol.kind) {
            case SymbolKind.Method:
                DisplayMethod(text, (MethodSymbol)symbol);
                break;
            case SymbolKind.LocalVariable:
            case SymbolKind.GlobalVariable:
            case SymbolKind.Parameter:
                DisplayVariable(text, (VariableSymbol)symbol, includeVariableTypes);
                break;
            case SymbolKind.Field:
                DisplayField(text, (FieldSymbol)symbol);
                break;
            case SymbolKind.Type:
                DisplayType(text, (TypeSymbol)symbol);
                break;
            default:
                throw new BelteInternalException($"DisplaySymbol: unexpected symbol '{symbol.kind}'");
        }
    }

    private static void DisplayContainedNames(DisplayText text, ISymbol symbol) {
        var currentSymbol = symbol as Symbol;

        while (currentSymbol.containingType != null) {
            text.Write(CreateType(currentSymbol.containingType.name));
            text.Write(CreatePunctuation(SyntaxKind.PeriodToken));

            currentSymbol = currentSymbol.containingType;
        }
    }

    private static void DisplayField(DisplayText text, FieldSymbol symbol) {
        DisplayModifiers(text, symbol);

        DisplayText.DisplayNode(text, symbol.type);
        text.Write(CreateSpace());
        text.Write(CreateIdentifier(symbol.name));
    }

    private static void DisplayType(DisplayText text, TypeSymbol symbol) {
        DisplayModifiers(text, symbol);

        if (symbol is NamedTypeSymbol n) {
            if (n is ClassSymbol)
                text.Write(CreateKeyword(SyntaxKind.ClassKeyword));
            else if (n is StructSymbol)
                text.Write(CreateKeyword(SyntaxKind.StructKeyword));
            else
                text.Write(CreateKeyword("type"));

            text.Write(CreateSpace());

            DisplayContainedNames(text, symbol);
            text.Write(CreateIdentifier(symbol.name));

            if (!n.templateParameters.IsEmpty) {
                text.Write(CreatePunctuation(SyntaxKind.LessThanToken));
                var first = true;

                foreach (var templateParameter in n.templateParameters) {
                    if (first)
                        first = false;
                    else
                        text.Write(CreatePunctuation(", "));

                    DisplaySymbol(text, templateParameter);
                }

                text.Write(CreatePunctuation(SyntaxKind.GreaterThanToken));
            }

            if (n is ClassSymbol c) {
                text.Write(CreateSpace());
                text.Write(CreateKeyword(SyntaxKind.ExtendsKeyword));
                text.Write(CreateSpace());
                DisplayText.DisplayNode(text, c.baseType);
            }
        } else {
            text.Write(CreateType(symbol.name));
        }
    }

    private static void DisplayVariable(DisplayText text, VariableSymbol symbol, bool includeVariableTypes) {
        if (includeVariableTypes) {
            DisplayText.DisplayNode(text, symbol.type);
            text.Write(CreateSpace());
        }

        text.Write(CreateIdentifier(symbol.name));
    }

    private static void DisplayMethod(DisplayText text, MethodSymbol symbol) {
        DisplayModifiers(text, symbol);

        if (symbol.type != null) {
            DisplayText.DisplayNode(text, symbol.type);
            text.Write(CreateSpace());
        }

        DisplayContainedNames(text, symbol);
        text.Write(CreateIdentifier(symbol.name));
        text.Write(CreatePunctuation(SyntaxKind.OpenParenToken));

        for (var i = 0; i < symbol.parameters.Length; i++) {
            if (i > 0) {
                text.Write(CreatePunctuation(SyntaxKind.CommaToken));
                text.Write(CreateSpace());
            }

            DisplayText.DisplayNode(text, symbol.parameters[i].type);
            text.Write(CreateSpace());
            DisplaySymbol(text, symbol.parameters[i]);
        }

        text.Write(CreatePunctuation(SyntaxKind.CloseParenToken));
    }

    private static void DisplayModifiers(DisplayText text, Symbol symbol) {
        if (symbol.accessibility == Accessibility.Public) {
            text.Write(CreateKeyword(SyntaxKind.PublicKeyword));
            text.Write(CreateSpace());
        } else if (symbol.accessibility == Accessibility.Private) {
            text.Write(CreateKeyword(SyntaxKind.PrivateKeyword));
            text.Write(CreateSpace());
        }

        if (symbol.isStatic) {
            text.Write(CreateKeyword(SyntaxKind.StaticKeyword));
            text.Write(CreateSpace());
        }
    }
}
