using System.IO;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Binding;
using Buckle.Diagnostics;

namespace Buckle.IO;

/// <summary>
/// Prints a <see cref="Symbol" />.
/// </summary>
internal static class SymbolPrinter {
    /// <summary>
    /// Writes a single <see cref="Symbol" />.
    /// </summary>
    /// <param name="symbol"><see cref="Symbol" /> to print (not modified).</param>
    /// <param name="writer">Where to write to (out).</param>
    internal static void WriteTo(this Symbol symbol, TextWriter writer) {
        switch (symbol.type) {
            case SymbolType.Function:
                WriteFunction((FunctionSymbol)symbol, writer);
                break;
            case SymbolType.LocalVariable:
                WriteLocalVariable((LocalVariableSymbol)symbol, writer);
                break;
            case SymbolType.GlobalVariable:
                WriteGlobalVariable((GlobalVariableSymbol)symbol, writer);
                break;
            case SymbolType.Parameter:
                WriteParameter((ParameterSymbol)symbol, writer);
                break;
            case SymbolType.Field:
                WriteField((FieldSymbol)symbol, writer);
                break;
            case SymbolType.Type:
                WriteType((TypeSymbol)symbol,writer);
                break;
            default:
                throw new BelteInternalException($"WriteTo: unexpected symbol '{symbol.type}'");
        }
    }

    private static void WriteField(FieldSymbol symbol, TextWriter writer) {
        BoundNodePrinter.WriteTypeClause(symbol.typeClause, writer);
        writer.WriteSpace();
        writer.WriteIdentifier(symbol.name);
    }

    private static void WriteType(TypeSymbol symbol, TextWriter writer) {
        if (symbol is StructSymbol) {
            writer.WriteKeyword("struct");
            writer.WriteSpace();
            writer.WriteIdentifier(symbol.name);
        } else {
            BoundNodePrinter.WriteTypeClause(new BoundTypeClause(symbol), writer);
        }
    }

    private static void WriteParameter(ParameterSymbol symbol, TextWriter writer) {
        BoundNodePrinter.WriteTypeClause(symbol.typeClause, writer);
        writer.WriteSpace();
        writer.WriteIdentifier(symbol.name);
    }

    private static void WriteGlobalVariable(GlobalVariableSymbol symbol, TextWriter writer) {
        BoundNodePrinter.WriteTypeClause(symbol.typeClause, writer);
        writer.WriteSpace();
        writer.WriteIdentifier(symbol.name);
    }

    private static void WriteLocalVariable(LocalVariableSymbol symbol, TextWriter writer) {
        BoundNodePrinter.WriteTypeClause(symbol.typeClause, writer);
        writer.WriteSpace();
        writer.WriteIdentifier(symbol.name);
    }

    private static void WriteFunction(FunctionSymbol symbol, TextWriter writer) {
        BoundNodePrinter.WriteTypeClause(symbol.typeClause, writer);
        writer.WriteSpace();
        writer.WriteIdentifier(symbol.name);
        writer.WritePunctuation("(");

        for (int i=0; i<symbol.parameters.Length; i++) {
            if (i > 0)
                writer.WritePunctuation(", ");

            symbol.parameters[i].WriteTo(writer);
        }

        writer.WritePunctuation(") ");
    }
}
