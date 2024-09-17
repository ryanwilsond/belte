using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
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
        DisplayContainedNames(text, symbol);
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
            DisplayTemplateParameters(text, n.templateParameters);

            if (n is ClassSymbol c && !n.isStatic) {
                text.Write(CreateSpace());
                text.Write(CreateKeyword(SyntaxKind.ExtendsKeyword));
                text.Write(CreateSpace());
                DisplayText.DisplayNode(text, c.baseType);
            }

            DisplayTemplateConstraints(text, n.templateConstraints);

        } else {
            text.Write(CreateType(symbol.name));
        }
    }

    private static void DisplayTemplateParameters(
        DisplayText text,
        ImmutableArray<ParameterSymbol> templateParameters) {
        if (!templateParameters.IsEmpty) {
            text.Write(CreatePunctuation(SyntaxKind.LessThanToken));
            var first = true;

            foreach (var templateParameter in templateParameters) {
                if (first)
                    first = false;
                else
                    text.Write(CreatePunctuation(", "));

                DisplaySymbol(text, templateParameter);
            }

            text.Write(CreatePunctuation(SyntaxKind.GreaterThanToken));
        }
    }

    private static void DisplayTemplateConstraints(
        DisplayText text,
        ImmutableArray<BoundExpression> templateConstraints) {
        if (!templateConstraints.IsEmpty) {
            text.Write(CreateSpace());
            text.Write(CreateKeyword(SyntaxKind.WhereKeyword));
            text.Write(CreateSpace());
            text.Write(CreatePunctuation(SyntaxKind.OpenBraceToken));
            text.Write(CreateSpace());

            foreach (var constraint in templateConstraints) {
                DisplayText.DisplayNode(text, constraint);
                text.Write(CreatePunctuation(SyntaxKind.SemicolonToken));
                text.Write(CreateSpace());
            }

            text.Write(CreatePunctuation(SyntaxKind.CloseBraceToken));
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

        DisplayTemplateParameters(text, symbol.templateParameters);
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

        DisplayTemplateConstraints(text, symbol.templateConstraints);
    }

    private static void DisplayModifiers(DisplayText text, Symbol symbol) {
        if (symbol.accessibility == Accessibility.Public) {
            text.Write(CreateKeyword(SyntaxKind.PublicKeyword));
            text.Write(CreateSpace());
        } else if (symbol.accessibility == Accessibility.Protected) {
            text.Write(CreateKeyword(SyntaxKind.ProtectedKeyword));
            text.Write(CreateSpace());
        } else if (symbol.accessibility == Accessibility.Private) {
            text.Write(CreateKeyword(SyntaxKind.PrivateKeyword));
            text.Write(CreateSpace());
        }

        if (symbol.isStatic) {
            text.Write(CreateKeyword(SyntaxKind.StaticKeyword));
            text.Write(CreateSpace());
        }

        if (symbol.isSealed) {
            text.Write(CreateKeyword(SyntaxKind.SealedKeyword));
            text.Write(CreateSpace());
        }

        if (symbol.isVirtual) {
            text.Write(CreateKeyword(SyntaxKind.VirtualKeyword));
            text.Write(CreateSpace());
        }

        if (symbol.isAbstract) {
            text.Write(CreateKeyword(SyntaxKind.AbstractKeyword));
            text.Write(CreateSpace());
        }

        if (symbol.isOverride) {
            text.Write(CreateKeyword(SyntaxKind.OverrideKeyword));
            text.Write(CreateSpace());
        }
    }
}
