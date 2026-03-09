using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using Shared;
using static Buckle.CodeAnalysis.Binding.Binder;

namespace Buckle.CodeAnalysis.Evaluating;

/// <summary>
/// Evaluates BoundStatements inline similar to an interpreter.
/// </summary>
internal sealed class Evaluator {
    private readonly BoundProgram _program;
    private readonly EvaluatorContext _context;
    private readonly Stack<StackFrame> _stack;

    private EvaluatorValue _programObject;
    private EvaluatorValue _lastValue;
    private bool _hasValue;
    private MethodSymbol _lazyToString;
    private Random _lazyRandom;

    /// <summary>
    /// Creates an <see cref="Evaluator" /> that can evaluate a <see cref="BoundProgram" /> (provided globals).
    /// </summary>
    internal Evaluator(BoundProgram program, EvaluatorContext context, string[] _) {
        _context = context;
        _context.typeLayouts = program.typeLayouts;
        _program = program;
        _stack = [];
        exceptions = [];
    }

    /// <summary>
    /// If the last output to the terminal was a `Print`, and not a `PrintLine`, meaning the caller might want to write
    /// an extra line to prevent formatting problems.
    /// </summary>
    internal bool lastOutputWasPrint { get; private set; }

    /// <summary>
    /// If the submission contains File/Directory IO.
    /// </summary>
    internal bool containsIO { get; private set; }

    /// <summary>
    /// All thrown exceptions during evaluation.
    /// </summary>
    internal List<Exception> exceptions { get; set; }

    private MethodSymbol _toStringMethod {
        get {
            if (_lazyToString is null) {
                var toString = CorLibrary.GetSpecialType(SpecialType.Object)
                    .GetMembers(WellKnownMemberNames.ToString).First() as MethodSymbol;

                Interlocked.CompareExchange(ref _lazyToString, toString, null);
            }

            return _lazyToString;
        }
    }

    /// <summary>
    /// Evaluate the provided <see cref="BoundProgram" />.
    /// </summary>
    /// <param name="abort">External flag used to cancel evaluation.</param>
    /// <param name="hasValue">If the evaluation had a returned result.</param>
    /// <returns>Result of <see cref="BoundProgram" /> (if applicable).</returns>
    internal object Evaluate(ValueWrapper<bool> abort, out bool hasValue) {
        var entryPoint = _program.entryPoint;

        if (entryPoint is null) {
            hasValue = false;
            return null;
        }

        var entryPointBody = _program.methodBodies[entryPoint];
        var programType = entryPoint.containingType;
        var result = EvaluatorValue.None;

        if (!programType.isStatic) {
            _programObject = CreateObject(programType);
            var constructor = programType.constructors.Where(c => c.parameterCount == 0).FirstOrDefault();

            if (constructor is not null)
                InvokeInstanceMethod(constructor, _programObject, [], abort);

            if (!entryPoint.isStatic)
                result = InvokeInstanceMethod(entryPoint, _programObject, [], abort);
        }

        if (entryPoint.isStatic)
            result = InvokeStaticMethod(entryPoint, [], abort);

        // Wait until Main finishes before the first call of Update
        if (_context.maintainThread) {
            while (_context.graphicsHandler is null)
                ;
        }

        if (_program.updatePoint is not null)
            _context.graphicsHandler?.SetUpdateHandler(UpdateCaller);

        hasValue = _hasValue;
        return hasValue ? EvaluatorValue.Format(result, _context) : null;
    }

    private EvaluatorValue CreateObject(NamedTypeSymbol type) {
        var layout = _program.typeLayouts[type];
        var heapObject = new HeapObject(type, layout.LocalsInOrder().Length);
        var index = _context.heap.Allocate(heapObject, _stack, _context);
        return EvaluatorValue.HeapPtr(index);
    }

    #region Statements

    private EvaluatorValue EvaluateStatement(
        MethodSymbol method,
        BoundBlockStatement block,
        ValueWrapper<bool> abort) {
        _hasValue = false;
        var insideTry = false;

        try {
            var labelToIndex = new Dictionary<LabelSymbol, int>();

            for (var i = 0; i < block.statements.Length; i++) {
                if (block.statements[i] is BoundLabelStatement l)
                    labelToIndex.Add(l.label, i + 1);
            }

            var index = 0;

            while (index < block.statements.Length) {
                if (abort)
                    throw new BelteThreadException();

                var s = block.statements[index];

                if (s.kind is not BoundKind.ReturnStatement)
                    _lastValue = EvaluatorValue.None;

                switch (s.kind) {
                    case BoundKind.NopStatement:
                        index++;
                        break;
                    case BoundKind.ExpressionStatement:
                        EvaluateExpressionStatement((BoundExpressionStatement)s, abort);
                        index++;
                        break;
                    case BoundKind.LocalDeclarationStatement:
                        EvaluateLocalDeclarationStatement((BoundLocalDeclarationStatement)s, abort);
                        index++;
                        break;
                    case BoundKind.LabelStatement:
                        index++;
                        break;
                    case BoundKind.GotoStatement:
                        var gs = (BoundGotoStatement)s;
                        index = labelToIndex[gs.label];
                        break;
                    case BoundKind.ConditionalGotoStatement:
                        var cgs = (BoundConditionalGotoStatement)s;
                        var condition = EvaluateExpression(cgs.condition, abort);

                        if (condition.kind == ValueKind.Null)
                            throw new BelteNullReferenceException(cgs.condition.syntax.location);

                        if (condition.@bool == cgs.jumpIfTrue)
                            index = labelToIndex[cgs.label];
                        else
                            index++;

                        break;
                    case BoundKind.ReturnStatement:
                        _hasValue = true;

                        if (method.returnsVoid) {
                            if (_lastValue.Equals(EvaluatorValue.None) || !_context.options.isScript)
                                _hasValue = false;

                            return _lastValue;
                        }

                        var returnStatement = (BoundReturnStatement)s;
                        var expression = returnStatement.expression;

                        if (returnStatement.refKind == RefKind.None) {
                            _lastValue = EvaluateExpression(expression, abort);
                        } else {
                            _lastValue = EvaluateAddress(
                                expression,
                                method.refKind == RefKind.RefConst ? AddressKind.ReadOnlyStrict : AddressKind.Writeable,
                                abort
                            );
                        }

                        return _lastValue;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(s.kind);
                }
            }

            return _lastValue;
        } catch (Exception e) {
            if (abort)
                return EvaluatorValue.None;

            if (insideTry)
                throw;

            exceptions.Add(e);
            lastOutputWasPrint = false;
            _hasValue = false;

            if (!_context.options.isScript)
                abort.Value = true;

            return EvaluatorValue.None;
        }
    }

    private void EvaluateLocalDeclarationStatement(BoundLocalDeclarationStatement node, ValueWrapper<bool> abort) {
        var local = node.declaration.dataContainer;
        var value = EvaluateExpression(node.declaration.initializer, abort);

        var frame = _stack.Peek();
        var slot = frame.layout.GetLocal(local).slot;
        frame.values[slot] = value;

        _lastValue = EvaluatorValue.None;
    }

    private void EvaluateExpressionStatement(BoundExpressionStatement node, ValueWrapper<bool> abort) {
        _lastValue = EvaluateExpression(node.expression, abort);
    }

    #endregion

    #region Expressions

    private EvaluatorValue EvaluateExpression(BoundExpression node, ValueWrapper<bool> abort) {
        if (node.constantValue is not null)
            return EvaluatorValue.Literal(node.constantValue.value, node.constantValue.specialType);

        return node.kind switch {
            // BoundKind.ThisExpression => EvaluateThisExpression(),
            // BoundKind.BaseExpression => EvaluateBaseExpression(),
            // BoundKind.CastExpression => EvaluateCastExpression((BoundCastExpression)node, abort),
            // BoundKind.AssignmentOperator => EvaluateAssignmentOperator((BoundAssignmentOperator)node, abort),
            // BoundKind.UnaryOperator => EvaluateUnaryOperator((BoundUnaryOperator)node, abort),
            // BoundKind.BinaryOperator => EvaluateBinaryOperator((BoundBinaryOperator)node, abort),
            // BoundKind.AsOperator => EvaluateAsOperator((BoundAsOperator)node, abort),
            // BoundKind.IsOperator => EvaluateIsOperator((BoundIsOperator)node, abort),
            // BoundKind.ConditionalOperator => EvaluateConditionalOperator((BoundConditionalOperator)node, abort),
            // BoundKind.NullAssertOperator => EvaluateNullAssertOperator((BoundNullAssertOperator)node, abort),
            BoundKind.CallExpression => EvaluateCallExpression((BoundCallExpression)node, CodeGenerator.UseKind.UsedAsValue, abort),
            // BoundKind.ObjectCreationExpression => EvaluateObjectCreationExpression((BoundObjectCreationExpression)node, abort),
            // BoundKind.ArrayCreationExpression => EvaluateArrayCreationExpression((BoundArrayCreationExpression)node, abort),
            // BoundKind.ArrayAccessExpression => EvaluateArrayAccessExpression((BoundArrayAccessExpression)node, abort),
            BoundKind.TypeExpression => EvaluateTypeExpression((BoundTypeExpression)node, abort),
            // BoundKind.TypeOfExpression => EvaluateTypeOfExpression((BoundTypeOfExpression)node, abort),
            // BoundKind.MethodGroup => EvaluateMethodGroup((BoundMethodGroup)node, abort),
            _ => throw ExceptionUtilities.UnexpectedValue(node.kind),
        };
    }

    private EvaluatorValue EvaluateTypeExpression(BoundTypeExpression _, ValueWrapper<bool> _2) {
        // This should only ever be called when an invalid expression statement makes it through binding without err
        // because script compilation ignores normal expression statement restrictions.
        //
        // `Console;`
        //
        return EvaluatorValue.None;
    }

    #endregion

    #region Addresses

    private bool HasHome(BoundExpression expression, AddressKind addressKind) {
        var frame = _stack.Peek();
        return Binder.HasHome(expression, addressKind, frame.layout.symbol, []);
    }

    private EvaluatorValue EvaluateAddress(BoundExpression node, AddressKind addressKind, ValueWrapper<bool> abort) {
        switch (node.kind) {
            case BoundKind.StackSlotExpression:
                return EvaluateStackAddress((BoundStackSlotExpression)node, addressKind, abort);
            case BoundKind.FieldSlotExpression:
                return EvaluateFieldAddress((BoundFieldSlotExpression)node, addressKind, abort);
            case BoundKind.ArrayAccessExpression:
                if (!HasHome(node, addressKind))
                    goto default;

                return EvaluateArrayElementAddress((BoundArrayAccessExpression)node, abort);
            case BoundKind.ThisExpression:
                if (CodeGenerator.IsValueType(node.type)) {
                    if (!HasHome(node, addressKind))
                        goto default;

                    return _stack.Peek().values[0];
                } else {
                    return EvaluatorValue.Ref(_stack.Peek().values, 0);
                }
            case BoundKind.BaseExpression:
                return EvaluatorValue.None;
            case BoundKind.CallExpression:
                var call = (BoundCallExpression)node;

                if (CodeGenerator.UseCallResultAsAddress(call, addressKind))
                    return EvaluateCallExpression(call, CodeGenerator.UseKind.UsedAsAddress, abort);

                goto default;
            case BoundKind.ConditionalOperator:
                if (!HasHome(node, addressKind))
                    goto default;

                return EvaluateConditionalOperatorAddress((BoundConditionalOperator)node, addressKind, abort);
            case BoundKind.ThrowExpression:
                return EvaluateExpression(node, abort);
            default:
                return EvaluateAddressOfTempClone(node, abort);
        }
    }

    private EvaluatorValue EvaluateConditionalOperatorAddress(
        BoundConditionalOperator node,
        AddressKind addressKind,
        ValueWrapper<bool> abort) {
        var condition = EvaluateExpression(node.condition, abort).@bool;

        if (condition)
            return EvaluateAddress(node.trueExpression, addressKind, abort);
        else
            return EvaluateAddress(node.falseExpression, addressKind, abort);
    }

    private EvaluatorValue EvaluateArrayElementAddress(BoundArrayAccessExpression node, ValueWrapper<bool> abort) {
        var receiver = EvaluateExpression(node.receiver, abort);
        var index = (int)EvaluateExpression(node.index, abort).int64;

        if (((ArrayTypeSymbol)node.receiver.type.StrippedType()).isSZArray)
            return EvaluatorValue.Ref(_context.heap[receiver.ptr].fields, index);
        else
            return _context.heap[receiver.ptr].fields[index];
    }

    private EvaluatorValue EvaluateFieldAddress(
        BoundFieldSlotExpression node,
        AddressKind addressKind,
        ValueWrapper<bool> abort) {
        if (!HasHome(node.original, addressKind))
            return EvaluateAddressOfTempClone(node, abort);
        // TODO We don't have static fields yet
        // else if (node.field.isStatic)
        //     return EvaluateStaticFieldAddress(node.field);
        else
            return EvaluateInstanceFieldAddress(node, addressKind, abort);
    }

    private EvaluatorValue EvaluateInstanceFieldAddress(
        BoundFieldSlotExpression node,
        AddressKind addressKind,
        ValueWrapper<bool> abort) {
        var field = node.field;

        var receiver = EvaluateReceiverRef(
            node.receiver,
            field.refKind == RefKind.None
                ? (addressKind == AddressKind.Constrained ? AddressKind.Writeable : addressKind)
                : (addressKind != AddressKind.ReadOnlyStrict ? AddressKind.ReadOnly : addressKind),
            abort
        );


        // TODO what is receiver exactly?
        // if (field.refKind == RefKind.None)
        //     return ;
        return EvaluatorValue.None;
    }

    private EvaluatorValue EvaluateReceiverRef(
        BoundExpression receiver,
        AddressKind addressKind,
        ValueWrapper<bool> abort) {
        var receiverType = receiver.type;

        if (receiverType.IsVerifierReference())
            return EvaluateExpression(receiver, abort);

        return EvaluateAddress(receiver, addressKind, abort);
    }

    private EvaluatorValue EvaluateStackAddress(
        BoundStackSlotExpression node,
        AddressKind addressKind,
        ValueWrapper<bool> abort) {
        if (!HasHome(node.original, addressKind))
            return EvaluateAddressOfTempClone(node, abort);

        return EvaluatorValue.Ref(_stack.Peek().values, node.slot);
    }

    private RefKind GetSymbolRefKind(Symbol symbol) {
        return symbol switch {
            ParameterSymbol p => p.refKind,
            DataContainerSymbol d => d.refKind,
            FieldSymbol f => f.refKind,
            _ => throw ExceptionUtilities.UnexpectedValue(symbol.kind)
        };
    }

    private EvaluatorValue EvaluateAddressOfTempClone(BoundExpression node, ValueWrapper<bool> abort) {
        var value = EvaluateExpression(node, abort);
        var temp = AllocateTemp(node.type);
        var frame = _stack.Peek();
        frame.values[temp.slot] = value;
        return EvaluatorValue.Ref(frame.values, temp.slot);
    }

    private VariableDefinition AllocateTemp(
        TypeSymbol type,
        LocalSlotConstraints slotConstraints = LocalSlotConstraints.None) {
        return _stack.Peek().layout.AllocateSlot(type, slotConstraints);
    }

    #endregion

    #region Calls

    private EvaluatorValue EvaluateCallExpression(
        BoundCallExpression node,
        CodeGenerator.UseKind useKind,
        ValueWrapper<bool> abort) {
        if (node.method.RequiresInstanceReceiver())
            return EvaluateInstanceCallExpression(node, useKind, abort);
        else
            return EvaluateStaticCallExpression(node, useKind, abort);
    }

    private EvaluatorValue EvaluateStaticCallExpression(
        BoundCallExpression node,
        CodeGenerator.UseKind useKind,
        ValueWrapper<bool> abort) {
        var method = node.method;
        var arguments = node.arguments;

        var evaluatedArguments = EvaluateArguments(arguments, method.parameters, node.argumentRefKinds, abort);

        var value = InvokeStaticMethod(method, evaluatedArguments, abort);

        if (useKind == CodeGenerator.UseKind.UsedAsValue && method.refKind != RefKind.None)
            return value.loc.ElementAt(value.ptr);
        else
            return value;
    }

    private EvaluatorValue EvaluateInstanceCallExpression(
        BoundCallExpression node,
        CodeGenerator.UseKind useKind,
        ValueWrapper<bool> abort) {
        var method = node.method;
        var arguments = node.arguments;
        var receiver = node.receiver;

        var thisParameter = EvaluateExpression(receiver, abort);

        if (thisParameter.kind == ValueKind.Null)
            throw new BelteNullReferenceException(receiver.syntax.location);

        var evaluatedArguments = EvaluateArguments(arguments, method.parameters, node.argumentRefKinds, abort);

        if (method.isAbstract || method.isVirtual) {
            var typeToLookup = receiver.kind == BoundKind.BaseExpression
                ? receiver.type.StrippedType()
                : _context.heap[thisParameter.ptr].type.StrippedType();

            var newMethod = typeToLookup
                .GetMembersUnordered()
                .Where(s => s is MethodSymbol m && m.overriddenMethod == method)
                .FirstOrDefault() as MethodSymbol;

            if (newMethod is not null)
                method = newMethod;
        }

        var value = InvokeInstanceMethod(method, thisParameter, evaluatedArguments, abort);

        if (useKind == CodeGenerator.UseKind.UsedAsValue && method.refKind != RefKind.None)
            return value.loc.ElementAt(value.ptr);
        else
            return value;
    }

    private EvaluatorValue[] EvaluateArguments(
        ImmutableArray<BoundExpression> arguments,
        ImmutableArray<ParameterSymbol> parameters,
        ImmutableArray<RefKind> argRefKindsOpt,
        ValueWrapper<bool> abort) {
        var builder = ArrayBuilder<EvaluatorValue>.GetInstance(arguments.Length);

        for (var i = 0; i < arguments.Length; i++) {
            var argRefKind = GetArgumentRefKind(parameters, argRefKindsOpt, i);
            builder.Add(EvaluateArgument(arguments[i], argRefKind, abort));
        }

        return builder.ToArray();
    }

    private static RefKind GetArgumentRefKind(
        ImmutableArray<ParameterSymbol> parameters,
        ImmutableArray<RefKind> argRefKindsOpt,
        int i) {
        RefKind argRefKind;

        if (i < parameters.Length) {
            if (!argRefKindsOpt.IsDefault && i < argRefKindsOpt.Length)
                argRefKind = argRefKindsOpt[i];
            else
                argRefKind = parameters[i].refKind;
        } else {
            argRefKind = RefKind.None;
        }

        return argRefKind;
    }

    private EvaluatorValue EvaluateArgument(BoundExpression argument, RefKind refKind, ValueWrapper<bool> abort) {
        if (refKind == RefKind.None)
            return EvaluateExpression(argument, abort);

        return EvaluateAddress(argument, AddressKind.Writeable, abort);
    }

    private EvaluatorValue InvokeInstanceMethod(
        MethodSymbol method,
        EvaluatorValue thisParameter,
        EvaluatorValue[] arguments,
        ValueWrapper<bool> abort) {
        _program.TryGetMethodBodyIncludingParents(method, out var body);

        var layout = _program.methodLayouts[method];
        var frame = new StackFrame(layout);

        if (!thisParameter.Equals(EvaluatorValue.None))
            frame.values[0] = thisParameter;

        for (var i = 0; i < arguments.Length; i++) {
            var slot = layout.GetLocal(method.parameters[i]).slot;
            frame.values[slot] = arguments[i];
        }

        _stack.Push(frame);

        var result = EvaluateStatement(method, body, abort);

        _stack.Pop();

        return result;
    }

    private EvaluatorValue InvokeStaticMethod(
        MethodSymbol method,
        EvaluatorValue[] arguments,
        ValueWrapper<bool> abort) {
        return InvokeInstanceMethod(method, EvaluatorValue.None, arguments, abort);
    }

    #endregion

    #region Libraries

    private void UpdateCaller(double deltaTime, ValueWrapper<bool> abort) {
        if (_program.updatePoint is null)
            return;

        var argument = EvaluatorValue.Literal(deltaTime, SpecialType.Decimal);
        InvokeInstanceMethod(_program.updatePoint, _programObject, [argument], abort);

        if (exceptions.Count > 0)
            abort.Value = true;
    }

    #endregion
}
