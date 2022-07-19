using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

internal static class BuiltinFunctions {
    internal static readonly FunctionSymbol Print = new FunctionSymbol("Print",
        ImmutableArray.Create(new ParameterSymbol("text", BoundTypeClause.NullableAny, 0)),
        new BoundTypeClause(TypeSymbol.Void));
    internal static readonly FunctionSymbol PrintLine = new FunctionSymbol("PrintLine",
        ImmutableArray.Create(new ParameterSymbol("text", BoundTypeClause.NullableAny, 0)),
        new BoundTypeClause(TypeSymbol.Void));
    internal static readonly FunctionSymbol Input = new FunctionSymbol("Input",
        ImmutableArray<ParameterSymbol>.Empty, BoundTypeClause.String);
    internal static readonly FunctionSymbol Randint = new FunctionSymbol("RandInt",
        ImmutableArray.Create(new ParameterSymbol("max", BoundTypeClause.NullableInt, 0)), BoundTypeClause.Int);
    internal static readonly FunctionSymbol Value = new FunctionSymbol("Value",
        ImmutableArray.Create(new ParameterSymbol("value", BoundTypeClause.NullableAny, 0)), BoundTypeClause.Any);
    internal static readonly FunctionSymbol HasValue = new FunctionSymbol("HasValue",
        ImmutableArray.Create(new ParameterSymbol("value", BoundTypeClause.NullableAny, 0)), BoundTypeClause.Bool);

    internal static IEnumerable<FunctionSymbol> GetAll()
        => typeof(BuiltinFunctions).GetFields(BindingFlags.NonPublic | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(FunctionSymbol))
        .Select(f => (FunctionSymbol)f.GetValue(null));
}

internal sealed class ParameterSymbol : LocalVariableSymbol {
    internal override SymbolType type => SymbolType.Parameter;
    internal int ordinal { get; }

    internal ParameterSymbol(string name, BoundTypeClause typeClause, int ordinal_) : base(name, typeClause, null) {
        ordinal = ordinal_;
    }
}

internal sealed class FunctionSymbol : Symbol {
    internal ImmutableArray<ParameterSymbol> parameters { get; }
    internal BoundTypeClause typeClause { get; }
    internal FunctionDeclaration declaration { get; }
    internal override SymbolType type => SymbolType.Function;

    internal FunctionSymbol(
        string name, ImmutableArray<ParameterSymbol> parameters_,
        BoundTypeClause typeClause_, FunctionDeclaration declaration_ = null)
        : base(name) {
        typeClause = typeClause_;
        parameters = parameters_;
        declaration = declaration_;
    }
}
