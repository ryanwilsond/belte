using System;
using System.IO;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.IO {

    internal static class SymbolPrinter {
        public static void WriteTo(this Symbol symbol, TextWriter writer) {
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
                    WriteType((TypeSymbol)symbol, writer);
                    break;
                default:
                    throw new Exception($"unexpected symbol '{symbol.type}'");
            }
        }

        private static void WriteType(TypeSymbol symbol, TextWriter writer) {
            writer.WriteType(symbol.name);
        }

        private static void WriteParameter(ParameterSymbol symbol, TextWriter writer) {
            writer.WriteType(symbol.lType.name);
            writer.Write(" ");
            writer.WriteIdentifier(symbol.name);
        }

        private static void WriteGlobalVariable(GlobalVariableSymbol symbol, TextWriter writer) {
            writer.WriteType(symbol.lType.name);
            writer.Write(" ");
            writer.WriteIdentifier(symbol.name);
        }

        private static void WriteLocalVariable(LocalVariableSymbol symbol, TextWriter writer) {
            writer.WriteType(symbol.lType.name);
            writer.Write(" ");
            writer.WriteIdentifier(symbol.name);
        }

        private static void WriteFunction(FunctionSymbol symbol, TextWriter writer) {
            writer.WriteType(symbol.lType.name);
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
}
