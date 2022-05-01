using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

internal static class BuiltinFunctions {
    public static readonly FunctionSymbol Print = new FunctionSymbol("print",
        ImmutableArray.Create(new ParameterSymbol("text", new BoundTypeClause(TypeSymbol.Any), 0)),
        new BoundTypeClause(TypeSymbol.Void));
    public static readonly FunctionSymbol Input = new FunctionSymbol("input",
        ImmutableArray<ParameterSymbol>.Empty,
        new BoundTypeClause(TypeSymbol.String));
    public static readonly FunctionSymbol Randint = new FunctionSymbol("randint",
        ImmutableArray.Create(new ParameterSymbol("max", new BoundTypeClause(TypeSymbol.Int), 0)),
        new BoundTypeClause(TypeSymbol.Int));

    internal static IEnumerable<FunctionSymbol> GetAll()
        => typeof(BuiltinFunctions).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(FunctionSymbol))
        .Select(f => (FunctionSymbol)f.GetValue(null));
}

internal sealed class ParameterSymbol : LocalVariableSymbol {
    public override SymbolType type => SymbolType.Parameter;
    public int ordinal { get; }

    public ParameterSymbol(string name, BoundTypeClause typeClause, int ordinal_) : base(name, typeClause, null) {
        ordinal = ordinal_;
    }
}

internal sealed class FunctionSymbol : Symbol {
    public ImmutableArray<ParameterSymbol> parameters { get; }
    public BoundTypeClause typeClause { get; }
    public FunctionDeclaration declaration { get; }
    public override SymbolType type => SymbolType.Function;

    public FunctionSymbol(
        string name, ImmutableArray<ParameterSymbol> parameters_,
        BoundTypeClause typeClause_, FunctionDeclaration declaration_ = null)
        : base(name) {
        typeClause = typeClause_;
        parameters = parameters_;
        declaration = declaration_;
    }
}
