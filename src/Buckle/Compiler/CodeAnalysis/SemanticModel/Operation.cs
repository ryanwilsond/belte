using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

internal abstract partial class Operation : IOperation {
    // protected static readonly IOperation Unset = new EmptyOperation(semanticModel: null, syntax: null, isImplicit: true);
    protected static readonly IOperation Unset = null;
    private readonly SemanticModel _owningSemanticModelOpt;

    private IOperation _parentDoNotAccessDirectly;

    private protected Operation(SemanticModel? semanticModel, SyntaxNode syntax, bool isImplicit) {
        _owningSemanticModelOpt = semanticModel;

        this.syntax = syntax;
        this.isImplicit = isImplicit;

        _parentDoNotAccessDirectly = Unset;
    }

    public IOperation parent => _parentDoNotAccessDirectly;

    public bool isImplicit { get; }

    public abstract OperationKind kind { get; }

    public SyntaxNode syntax { get; }

    public abstract ITypeSymbol type { get; }

    internal abstract ConstantValue operationConstantValue { get; }

    public Optional<object> constantValue {
        get {
            if (operationConstantValue is null)
                return default;

            return new Optional<object>(operationConstantValue.value);
        }
    }

    // IEnumerable<IOperation> IOperation.children => childOperations;

    public IOperation.OperationList childOperations => new IOperation.OperationList(this);

    internal abstract int childOperationsCount { get; }

    internal abstract IOperation GetCurrent(int slot, int index);

    internal abstract (bool hasNext, int nextSlot, int nextIndex) MoveNext(int previousSlot, int previousIndex);

    internal abstract (bool hasNext, int nextSlot, int nextIndex) MoveNextReversed(int previousSlot, int previousIndex);

    // SemanticModel IOperation.semanticModel => _owningSemanticModelOpt?.containingPublicModelOrSelf;
    SemanticModel IOperation.semanticModel => throw new NotImplementedException();

    internal SemanticModel owningSemanticModel => _owningSemanticModelOpt;

    // public abstract void Accept(OperationVisitor visitor);

    // public abstract TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument);

    private protected void SetParentOperation(IOperation parent) {
        _parentDoNotAccessDirectly = parent;
    }

    public static T SetParentOperation<T>(T operation, IOperation parent) where T : IOperation {
        (operation as Operation)?.SetParentOperation(parent);
        return operation;
    }

    public static ImmutableArray<T> SetParentOperation<T>(ImmutableArray<T> operations, IOperation? parent) where T : IOperation {
        if (operations.Length == 0)
            return operations;

        foreach (var operation in operations)
            SetParentOperation(operation, parent);

        return operations;
    }

    private string GetDebuggerDisplay() => $"{GetType().Name} Type: {(type is null ? "null" : type)}";
}
