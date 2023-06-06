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
    public static void DisplaySymbol(DisplayText text, ISymbol symbol) {
        if (symbol is VariableSymbol v && v.constantValue != null) {
            DisplayText.DisplayConstant(text, v.constantValue);
            return;
        }

        switch (symbol.kind) {
            case SymbolKind.Method:
                DisplayMethod(text, (MethodSymbol)symbol);
                break;
            case SymbolKind.LocalVariable:
                DisplayLocalVariable(text, (LocalVariableSymbol)symbol);
                break;
            case SymbolKind.GlobalVariable:
                DisplayGlobalVariable(text, (GlobalVariableSymbol)symbol);
                break;
            case SymbolKind.Parameter:
                DisplayParameter(text, (ParameterSymbol)symbol);
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
        DisplayText.DisplayNode(text, symbol.type);
        text.Write(CreateSpace());
        text.Write(CreateIdentifier(symbol.name));
    }

    private static void DisplayType(DisplayText text, TypeSymbol symbol) {
        if (symbol is StructSymbol ss) {
            text.Write(CreateKeyword(SyntaxKind.StructKeyword));
            text.Write(CreateSpace());

            DisplayContainedNames(text, symbol);
            text.Write(CreateIdentifier(symbol.name));

            if (!ss.templateParameters.IsEmpty) {
                text.Write(CreatePunctuation(SyntaxKind.LessThanToken));
                var first = true;

                foreach (var templateParameter in ss.templateParameters) {
                    if (first)
                        first = false;
                    else
                        text.Write(CreatePunctuation(", "));

                    DisplaySymbol(text, templateParameter);
                }

                text.Write(CreatePunctuation(SyntaxKind.GreaterThanToken));
            }
        } else if (symbol is ClassSymbol cs) {
            text.Write(CreateKeyword(SyntaxKind.ClassKeyword));
            text.Write(CreateSpace());

            DisplayContainedNames(text, symbol);
            text.Write(CreateIdentifier(symbol.name));

            if (!cs.templateParameters.IsEmpty) {
                text.Write(CreatePunctuation(SyntaxKind.LessThanToken));
                var first = true;

                foreach (var templateParameter in cs.templateParameters) {
                    if (first)
                        first = false;
                    else
                        text.Write(CreatePunctuation(", "));

                    DisplaySymbol(text, templateParameter);
                }

                text.Write(CreatePunctuation(SyntaxKind.GreaterThanToken));
            }
        } else {
            text.Write(CreateType(symbol.name));
        }
    }

    private static void DisplayParameter(DisplayText text, ParameterSymbol symbol) {
        DisplayText.DisplayNode(text, symbol.type);
        text.Write(CreateSpace());
        text.Write(CreateIdentifier(symbol.name));
    }

    private static void DisplayGlobalVariable(DisplayText text, GlobalVariableSymbol symbol) {
        DisplayText.DisplayNode(text, symbol.type);
        text.Write(CreateSpace());
        text.Write(CreateIdentifier(symbol.name));
    }

    private static void DisplayLocalVariable(DisplayText text, LocalVariableSymbol symbol) {
        DisplayText.DisplayNode(text, symbol.type);
        text.Write(CreateSpace());
        text.Write(CreateIdentifier(symbol.name));
    }

    private static void DisplayMethod(DisplayText text, MethodSymbol symbol) {
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

            DisplaySymbol(text, symbol.parameters[i]);
        }

        text.Write(CreatePunctuation(SyntaxKind.CloseParenToken));
    }
}
