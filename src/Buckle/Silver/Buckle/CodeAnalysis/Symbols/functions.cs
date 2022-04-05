using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace Buckle.CodeAnalysis.Symbols {
    internal static class BuiltinFunctions {
        public static readonly FunctionSymbol Print = new FunctionSymbol(
            "print", ImmutableArray.Create(new ParameterSymbol("text", TypeSymbol.String)), TypeSymbol.Void);
        public static readonly FunctionSymbol Input = new FunctionSymbol(
            "input", ImmutableArray<ParameterSymbol>.Empty, TypeSymbol.String);

        internal static IEnumerable<FunctionSymbol> GetAll()
            => typeof(BuiltinFunctions).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(FunctionSymbol))
            .Select(f => (FunctionSymbol)f.GetValue(null));
    }

    internal sealed class ParameterSymbol : VariableSymbol {
        public override SymbolType type => SymbolType.Parameter;

        public ParameterSymbol(string name, TypeSymbol lType) : base(name, true, lType) { }
    }

    internal sealed class FunctionSymbol : Symbol {
        public ImmutableArray<ParameterSymbol> parameters { get; }
        public TypeSymbol lType { get; }
        public override SymbolType type => SymbolType.Function;

        public FunctionSymbol(string name, ImmutableArray<ParameterSymbol> parameters_, TypeSymbol lType_)
            : base(name) {
            lType = lType_;
            parameters = parameters_;
        }
    }
}
