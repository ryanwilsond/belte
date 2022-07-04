using System;
using System.IO;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.IO;

internal static class SymbolPrinter {
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
            case SymbolType.Type:
                BoundNodePrinter.WriteTypeClause(new BoundTypeClause((TypeSymbol)symbol), writer);
                break;
            default:
                throw new Exception($"WriteTo: unexpected symbol '{symbol.type}'");
        }
    }

    private static void WriteParameter(ParameterSymbol symbol, TextWriter writer) {
        BoundNodePrinter.WriteTypeClause(symbol.typeClause, writer);
        writer.Write(" ");
        writer.WriteIdentifier(symbol.name);
    }

    private static void WriteGlobalVariable(GlobalVariableSymbol symbol, TextWriter writer) {
        BoundNodePrinter.WriteTypeClause(symbol.typeClause, writer);
        writer.Write(" ");
        writer.WriteIdentifier(symbol.name);
    }

    private static void WriteLocalVariable(LocalVariableSymbol symbol, TextWriter writer) {
        BoundNodePrinter.WriteTypeClause(symbol.typeClause, writer);
        writer.Write(" ");
        writer.WriteIdentifier(symbol.name);
    }

    private static void WriteFunction(FunctionSymbol symbol, TextWriter writer) {
        BoundNodePrinter.WriteTypeClause(symbol.typeClause, writer);
        writer.Write(" ");
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
