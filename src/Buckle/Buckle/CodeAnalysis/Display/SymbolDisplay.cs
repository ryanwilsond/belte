using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using static Buckle.CodeAnalysis.Display.DisplayTextSegment;

namespace Buckle.CodeAnalysis.Display;

/// <summary>
/// Prints a <see cref="Symbol" />.
/// </summary>
internal static class SymbolDisplay {
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
    internal static void DisplaySymbol(DisplayText text, Symbol symbol) {
        switch (symbol.kind) {
            case SymbolKind.Function:
                DisplayFunction(text, (FunctionSymbol)symbol);
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
                throw new BelteInternalException($"WriteTo: unexpected symbol '{symbol.kind}'");
        }
    }

    private static void DisplayField(DisplayText text, FieldSymbol symbol) {
        DisplayText.DisplayNode(text, symbol.type);
        text.Write(CreateSpace());
        text.Write(CreateIdentifier(symbol.name));
    }

    private static void DisplayType(DisplayText text, TypeSymbol symbol) {
        if (symbol is StructSymbol) {
            text.Write(CreateKeyword(SyntaxKind.StructKeyword));
            text.Write(CreateSpace());
            text.Write(CreateIdentifier(symbol.name));
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

    private static void DisplayFunction(DisplayText text, FunctionSymbol symbol) {
        DisplayText.DisplayNode(text, symbol.type);
        text.Write(CreateSpace());
        text.Write(CreateIdentifier(symbol.name));
        text.Write(CreatePunctuation(SyntaxKind.OpenParenToken));

        for (int i=0; i<symbol.parameters.Length; i++) {
            if (i > 0) {
                text.Write(CreatePunctuation(SyntaxKind.CommaToken));
                text.Write(CreateSpace());
            }

            DisplaySymbol(text, symbol.parameters[i]);
        }

        text.Write(CreatePunctuation(SyntaxKind.CloseParenToken));
    }
}
