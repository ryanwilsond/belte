using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Text;
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

        if (local.isGlobal) {
            _context.AddOrUpdateGlobal(local, value);
            return;
        }

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
            BoundKind.ThisExpression => EvaluateThisExpression((BoundThisExpression)node),
            BoundKind.BaseExpression => EvaluateBaseExpression((BoundBaseExpression)node),
            BoundKind.DataContainerExpression => EvaluateDataContainerExpression((BoundDataContainerExpression)node),
            BoundKind.StackSlotExpression => EvaluateStackSlotExpression((BoundStackSlotExpression)node),
            BoundKind.FieldSlotExpression => EvaluateFieldSlotExpression((BoundFieldSlotExpression)node, abort),
            BoundKind.CastExpression => EvaluateCastExpression((BoundCastExpression)node, abort),
            BoundKind.AssignmentOperator => EvaluateAssignmentOperator((BoundAssignmentOperator)node, CodeGenerator.UseKind.UsedAsValue, abort),
            BoundKind.UnaryOperator => EvaluateUnaryOperator((BoundUnaryOperator)node, abort),
            BoundKind.BinaryOperator => EvaluateBinaryOperator((BoundBinaryOperator)node, abort),
            BoundKind.AsOperator => EvaluateAsOperator((BoundAsOperator)node, abort),
            BoundKind.IsOperator => EvaluateIsOperator((BoundIsOperator)node, abort),
            BoundKind.ConditionalOperator => EvaluateConditionalOperator((BoundConditionalOperator)node, abort),
            BoundKind.NullAssertOperator => EvaluateNullAssertOperator((BoundNullAssertOperator)node, abort),
            BoundKind.CallExpression => EvaluateCallExpression((BoundCallExpression)node, CodeGenerator.UseKind.UsedAsValue, abort),
            BoundKind.ObjectCreationExpression => EvaluateObjectCreationExpression((BoundObjectCreationExpression)node, abort),
            BoundKind.ArrayCreationExpression => EvaluateArrayCreationExpression((BoundArrayCreationExpression)node, abort),
            BoundKind.ArrayAccessExpression => EvaluateArrayAccessExpression((BoundArrayAccessExpression)node, abort),
            BoundKind.TypeExpression => EvaluateTypeExpression(),
            BoundKind.TypeOfExpression => EvaluateTypeOfExpression((BoundTypeOfExpression)node),
            BoundKind.MethodGroup => EvaluateMethodGroup((BoundMethodGroup)node),
            _ => throw ExceptionUtilities.UnexpectedValue(node.kind),
        };
    }

    private EvaluatorValue EvaluateThisExpression(BoundThisExpression node) {
        var value = _stack.Peek().values[0];

        if (CodeGenerator.IsValueType(node.type))
            return value.loc[value.ptr];

        return value;
    }

    private EvaluatorValue EvaluateBaseExpression(BoundBaseExpression node) {
        var value = _stack.Peek().values[0];

        // TODO Should node.type be replaced by method.containingType?
        if (CodeGenerator.IsValueType(node.type))
            return value.loc[value.ptr];

        return value;
    }

    private EvaluatorValue EvaluateTypeOfExpression(BoundTypeOfExpression expression) {
        return EvaluatorValue.Type(expression.type);
    }

    private EvaluatorValue EvaluateMethodGroup(BoundMethodGroup methodGroup) {
        return EvaluatorValue.MethodGroup(methodGroup);
    }

    private EvaluatorValue EvaluateTypeExpression() {
        // This should only ever be called when an invalid expression statement makes it through binding without err
        // because script compilation ignores normal expression statement restrictions.
        //
        // `Console;`
        //
        return EvaluatorValue.None;
    }

    private EvaluatorValue EvaluateDataContainerExpression(BoundDataContainerExpression node) {
        if (!_context.TryGetGlobal(node.dataContainer, out var value))
            throw new BelteInternalException($"Attempted to find global '{node.dataContainer.name}' that doesn't exist");

        return value;
    }

    private EvaluatorValue EvaluateStackSlotExpression(BoundStackSlotExpression node) {
        return _stack.Peek().values[node.slot];
    }

    private EvaluatorValue EvaluateFieldSlotExpression(BoundFieldSlotExpression node, ValueWrapper<bool> abort) {
        var field = node.field;

        var value = EvaluateFieldNoIndirection(node, abort);

        if (field.refKind != RefKind.None)
            return value.loc[value.ptr];

        return value;
    }

    private EvaluatorValue EvaluateFieldNoIndirection(BoundFieldSlotExpression node, ValueWrapper<bool> abort) {
        var field = node.field;

        if (field.isStatic) {
            // TODO static fields
            return EvaluatorValue.None;
        } else {
            var receiver = node.receiver;
            var fieldType = field.type;

            if (CodeGenerator.IsValueType(fieldType) && (object)fieldType == receiver.type) {
                return EvaluateExpression(receiver, abort);
            } else {
                var receiverValue = EvaluateFieldLoadReceiver(receiver, abort);
                return _context.heap[receiverValue.ptr].fields[node.slot];
            }
        }
    }

    private EvaluatorValue EvaluateFieldLoadReceiver(BoundExpression receiver, ValueWrapper<bool> abort) {
        if (CodeGenerator.FieldLoadMustUseRef(receiver) || FieldLoadPrefersRef(receiver)) {
            return EvaluateFieldLoadReceiverAddress(receiver, abort, out var expr)
                ? expr
                : EvaluateReceiverRef(receiver, AddressKind.ReadOnly, abort);
        }

        return EvaluateExpression(receiver, abort);
    }

    private bool EvaluateFieldLoadReceiverAddress(
        BoundExpression receiver,
        ValueWrapper<bool> abort,
        out EvaluatorValue expr) {
        if (receiver is null || !CodeGenerator.IsValueType(receiver.type)) {
            expr = EvaluatorValue.None;
            return false;
        } else if (receiver.kind == BoundKind.CastExpression) {
            var conversion = (BoundCastExpression)receiver;

            if (conversion.conversion.kind == ConversionKind.AnyUnboxing) {
                expr = EvaluateExpression(conversion.operand, abort);
                return true;
            }
        } else if (receiver.kind == BoundKind.FieldSlotExpression) {
            var fieldSlot = (BoundFieldSlotExpression)receiver;
            var field = fieldSlot.field;

            if (!field.isStatic && EvaluateFieldLoadReceiverAddress(fieldSlot.receiver, abort, out var nestedExpr)) {
                expr = EvaluatorValue.Ref(_context.heap[nestedExpr.ptr].fields, fieldSlot.slot);
                return true;
            }
        }

        expr = EvaluatorValue.None;
        return false;
    }

    private bool FieldLoadPrefersRef(BoundExpression receiver) {
        if (!receiver.type.IsVerifierValue())
            return true;

        if (receiver.kind == BoundKind.CastExpression &&
            ((BoundCastExpression)receiver).conversion.kind == ConversionKind.AnyUnboxing) {
            return true;
        }

        if (!HasHome(receiver, AddressKind.ReadOnly))
            return false;

        switch (receiver.kind) {
            case BoundKind.ParameterExpression:
                return ((BoundParameterExpression)receiver).parameter.refKind != RefKind.None;
            case BoundKind.DataContainerExpression:
                return ((BoundDataContainerExpression)receiver).dataContainer.refKind != RefKind.None;
            case BoundKind.FieldAccessExpression:
                var fieldAccess = (BoundFieldAccessExpression)receiver;
                var field = fieldAccess.field;

                if (field.isStatic || field.refKind != RefKind.None)
                    return true;

                return FieldLoadPrefersRef(fieldAccess.receiver);
        }

        return true;
    }

    private EvaluatorValue EvaluateCastExpression(BoundCastExpression node, ValueWrapper<bool> abort) {
        var value = EvaluateExpression(node.operand, abort);

        if (CodeGenerator.IsReferenceType(node.operand.type)) {
            if (node.type.specialType == SpecialType.Nullable) {
                return value;
            } else if (node.type.specialType == SpecialType.String) {
                // _builder.EmitToString();
                // return;
                // TODO Allow direct string cast?
            }
        }

        var isCastable = node.operand.type.specialType == SpecialType.String && node.type.IsPrimitiveType() ||
            node.type.specialType == SpecialType.String && node.operand.type.IsPrimitiveType();

        var involvesRefTypes = !isCastable && (node.operand.type.IsVerifierReference() ||
            (node.type.IsVerifierReference() && node.type.specialType != SpecialType.String));

        switch (node.conversion.kind) {
            case ConversionKind.Identity:
                return value;
            case ConversionKind.Implicit when involvesRefTypes:
            case ConversionKind.ImplicitReference:
            case ConversionKind.AnyBoxing:
                return EvaluateImplicitReferenceConversion(node, value);
            case ConversionKind.Explicit when involvesRefTypes:
            case ConversionKind.ExplicitReference:
            case ConversionKind.AnyUnboxing:
                return value;
            case ConversionKind.Implicit:
            case ConversionKind.Explicit:
                return EvaluateConvertCallOrNumericConversion(node, value);
            default:
                throw ExceptionUtilities.UnexpectedValue(node.conversion.kind);
        }
    }

    private EvaluatorValue EvaluateConvertCallOrNumericConversion(BoundCastExpression node, EvaluatorValue value) {
        var fromType = node.operand.type.specialType;
        var toType = node.type.specialType;

        switch (fromType, toType) {
            case (SpecialType.String, SpecialType.Bool):
                value.@bool = Convert.ToBoolean(value.@string);
                value.kind = ValueKind.Bool;
                break;
            case (SpecialType.String, SpecialType.Int):
                value.int64 = Convert.ToInt64(value.@string);
                value.kind = ValueKind.Int64;
                break;
            case (SpecialType.Decimal, SpecialType.Int):
                value.int64 = Convert.ToInt64(value.@double);
                value.kind = ValueKind.Int64;
                break;
            case (SpecialType.String, SpecialType.Decimal):
                value.@double = Convert.ToDouble(value.@string);
                value.kind = ValueKind.Double;
                break;
            case (SpecialType.Int, SpecialType.Decimal):
                value.@double = Convert.ToDouble(value.int64);
                value.kind = ValueKind.Double;
                break;
            case (SpecialType.Int, SpecialType.String):
                value.@string = Convert.ToString(value.int64);
                value.kind = ValueKind.String;
                break;
            case (SpecialType.Decimal, SpecialType.String):
                value.@string = Convert.ToString(value.@double);
                value.kind = ValueKind.String;
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue((fromType, toType));
        }

        return value;
    }

    private EvaluatorValue EvaluateImplicitReferenceConversion(BoundCastExpression node, EvaluatorValue value) {
        if (node.type.IsArray())
            return EvaluateStaticCast(node.type, value);

        return value;
    }

    private EvaluatorValue EvaluateStaticCast(TypeSymbol to, EvaluatorValue value) {
        var temp = AllocateTemp(to);
        _stack.Peek().values[temp.slot] = value;
        // TODO FreeTemp?
        return value;
    }

    private EvaluatorValue EvaluateObjectCreationExpression(
        BoundObjectCreationExpression node,
        ValueWrapper<bool> abort) {
        if (node.constructor.originalDefinition == CorLibrary.GetWellKnownMember(WellKnownMembers.Nullable_ctor))
            return EvaluateExpression(node.arguments[0], abort);

        var value = CreateObject((NamedTypeSymbol)node.type);

        var method = node.constructor;
        var evaluatedArguments = EvaluateArguments(node.arguments, method.parameters, node.argumentRefKinds, abort);
        InvokeInstanceMethod(method, value, evaluatedArguments, abort);

        return value;
    }

    private EvaluatorValue EvaluateArrayAccessExpression(BoundArrayAccessExpression node, ValueWrapper<bool> abort) {
        var receiver = EvaluateExpression(node.receiver, abort);

        if (receiver.kind == ValueKind.Null)
            throw new BelteNullReferenceException(node.syntax.location);

        var index = (int)EvaluateExpression(node.index, abort).int64;

        if (index >= _context.heap[receiver.ptr].fields.Length)
            throw new BelteIndexOutOfRangeException(node.syntax.location);

        return _context.heap[receiver.ptr].fields[index];
    }

    private EvaluatorValue EvaluateArrayCreationExpression(
        BoundArrayCreationExpression node,
        ValueWrapper<bool> abort) {
        var sizes = node.sizes.Select(s => (int)EvaluateExpression(s, abort).int64);
        var array = CreateArray((ArrayTypeSymbol)node.type.StrippedType(), sizes.ToArray(), 0);
        var index = _context.heap.Allocate(array, _stack, _context);
        return EvaluatorValue.HeapPtr(index);
    }

    private HeapObject CreateArray(ArrayTypeSymbol type, int[] sizes, int depth) {
        var length = sizes[depth];
        var elements = new EvaluatorValue[length];

        if (depth == sizes.Length - 1) {
            for (var i = 0; i < length; i++)
                elements[i] = GetDefaultValue(type.elementType);
        } else {
            for (var i = 0; i < length; i++) {
                var array = CreateArray(type, sizes, depth + 1);
                var index = _context.heap.Allocate(array, _stack, _context);
                elements[i] = EvaluatorValue.HeapPtr(index);
            }
        }

        return new HeapObject(type, elements);
    }

    private EvaluatorValue GetDefaultValue(TypeSymbol type) {
        return type.IsPrimitiveType()
            ? EvaluatorValue.Literal(LiteralUtilities.GetDefaultValue(type.specialType), type.specialType)
            : EvaluatorValue.Null;
    }

    #endregion

    #region Operators

    private EvaluatorValue EvaluateConditionalOperator(BoundConditionalOperator node, ValueWrapper<bool> abort) {
        var condition = EvaluateExpression(node.condition, abort).@bool;

        if (condition)
            return EvaluateExpression(node.trueExpression, abort);
        else
            return EvaluateExpression(node.falseExpression, abort);
    }

    private EvaluatorValue EvaluateNullAssertOperator(BoundNullAssertOperator node, ValueWrapper<bool> abort) {
        if (!node.throwIfNull)
            return EvaluateExpression(node.operand, abort);

        return NullAssertValue(node, abort);
    }

    private EvaluatorValue NullAssertValue(BoundExpression node, ValueWrapper<bool> abort) {
        var value = EvaluateExpression(node, abort);

        if (value.kind == ValueKind.Null)
            throw new BelteNullReferenceException(node.syntax.location);

        return value;
    }

    private EvaluatorValue EvaluateIsOperator(BoundIsOperator node, ValueWrapper<bool> abort) {
        var operand = node.left;
        var value = EvaluateExpression(operand, abort);
        var targetType = node.right.type;

        if (value.kind == ValueKind.Null) {
            value.@bool = node.isNot;
            value.kind = ValueKind.Bool;
            return value;
        }

        if (value.kind == ValueKind.Int64 && targetType.specialType == SpecialType.Int ||
            value.kind == ValueKind.Bool && targetType.specialType == SpecialType.Bool ||
            value.kind == ValueKind.String && targetType.specialType == SpecialType.String ||
            value.kind == ValueKind.Double && targetType.specialType == SpecialType.Decimal ||
            targetType.specialType == SpecialType.Any) {
            value.@bool = !node.isNot;
            value.kind = ValueKind.Bool;
            return value;
        }

        if (value.kind == ValueKind.HeapPtr) {
            var operandType = _context.heap[value.ptr].type;

            if (operandType.InheritsFromIgnoringConstruction((NamedTypeSymbol)targetType)) {
                value.@bool = !node.isNot;
                value.kind = ValueKind.Bool;
                return value;
            }
        }

        value.@bool = node.isNot;
        value.kind = ValueKind.Bool;
        return value;
    }

    private EvaluatorValue EvaluateAsOperator(BoundAsOperator node, ValueWrapper<bool> abort) {
        var operand = node.left;
        var value = EvaluateExpression(operand, abort);

        if (value.kind == ValueKind.Null)
            return value;

        var operandType = operand.type;
        var targetType = node.type;

        if (operandType.InheritsFromIgnoringConstruction((NamedTypeSymbol)targetType))
            return value;

        return EvaluatorValue.Null;
    }

    private EvaluatorValue EvaluateAssignmentOperator(
        BoundAssignmentOperator node,
        CodeGenerator.UseKind useKind,
        ValueWrapper<bool> abort) {
        if (node.left is BoundDataContainerExpression)
            return EvaluateGlobalAssignment(node, useKind, abort);

        var lhs = EvaluateAssignmentPreamble(node, abort, out var lhsUsesStack);
        var value = EvaluateAssignmentValue(node, abort);
        var temp = EvaluateAssignmentDup(node, value, lhsUsesStack);

        lhs.loc[lhs.ptr] = value;

        return EvaluateAssignmentPostfix(node, temp, value, useKind);
    }

    private EvaluatorValue EvaluateGlobalAssignment(
        BoundAssignmentOperator node,
        CodeGenerator.UseKind useKind,
        ValueWrapper<bool> abort) {
        var global = (node.left as BoundDataContainerExpression).dataContainer;
        var value = EvaluateAssignmentValue(node, abort);
        var temp = EvaluateAssignmentDup(node, value, true);

        _context.AddOrUpdateGlobal(global, value);

        return EvaluateAssignmentPostfix(node, temp, value, useKind);
    }

    private EvaluatorValue EvaluateAssignmentPostfix(
        BoundAssignmentOperator node,
        EvaluatorValue temp,
        EvaluatorValue value,
        CodeGenerator.UseKind useKind) {
        if (!temp.Equals(EvaluatorValue.None)) {
            if (useKind == CodeGenerator.UseKind.UsedAsAddress)
                return temp;
            else
                return temp.loc[temp.ptr];

            // TODO FreeTemp?
        }

        if (useKind == CodeGenerator.UseKind.UsedAsValue && node.isRef)
            return value.loc[value.ptr];

        return value;
    }

    private EvaluatorValue EvaluateAssignmentDup(
        BoundAssignmentOperator node,
        EvaluatorValue value,
        bool lhsUsesStack) {
        if (lhsUsesStack) {
            var temp = AllocateTemp(
                node.left.type,
                node.isRef ? LocalSlotConstraints.ByRef : LocalSlotConstraints.None
            );

            _stack.Peek().values[temp.slot] = value;
            return EvaluatorValue.Ref(_stack.Peek().values, temp.slot);
        }

        return EvaluatorValue.None;
    }

    private EvaluatorValue EvaluateAssignmentPreamble(
        BoundAssignmentOperator node,
        ValueWrapper<bool> abort,
        out bool lhsUsesStack) {
        var assignmentTarget = node.left;
        var expr = EvaluatorValue.None;
        lhsUsesStack = false;

        switch (assignmentTarget.kind) {
            case BoundKind.FieldSlotExpression: {
                    var left = (BoundFieldSlotExpression)assignmentTarget;

                    if (left.field.refKind != RefKind.None && !node.isRef) {
                        expr = EvaluateFieldNoIndirection(left, abort);
                        lhsUsesStack = true;
                    } else if (!left.field.isStatic) {
                        expr = EvaluateReceiverRef(left.receiver, AddressKind.Writeable, abort);
                        lhsUsesStack = true;
                    }
                }

                break;
            case BoundKind.StackSlotExpression: {
                    var left = (BoundStackSlotExpression)assignmentTarget;

                    if (GetSymbolRefKind(left.symbol) != RefKind.None && !node.isRef) {
                        expr = EvaluatorValue.Ref(_stack.Peek().values, left.slot);
                        lhsUsesStack = true;
                    }
                }

                break;
            case BoundKind.ArrayAccessExpression: {
                    var left = (BoundArrayAccessExpression)assignmentTarget;
                    var receiver = EvaluateExpression(left.receiver, abort);
                    var index = (int)EvaluateExpression(left.index, abort).int64;
                    expr = EvaluatorValue.Ref(_context.heap[receiver.ptr].fields, index);
                    lhsUsesStack = true;
                }

                break;
            case BoundKind.ThisExpression: {
                    var left = (BoundThisExpression)assignmentTarget;
                    expr = EvaluateAddress(left, AddressKind.Writeable, abort);
                    lhsUsesStack = true;
                }

                break;
            case BoundKind.ConditionalOperator: {
                    var left = (BoundConditionalOperator)assignmentTarget;
                    expr = EvaluateAddress(left, AddressKind.Writeable, abort);
                    lhsUsesStack = true;
                }

                break;
            case BoundKind.CallExpression: {
                    var left = (BoundCallExpression)assignmentTarget;
                    expr = EvaluateCallExpression(left, CodeGenerator.UseKind.UsedAsAddress, abort);
                    lhsUsesStack = true;
                }

                break;
            case BoundKind.AssignmentOperator:
                var assignment = (BoundAssignmentOperator)assignmentTarget;

                if (!assignment.isRef)
                    goto default;

                expr = EvaluateAssignmentOperator(assignment, CodeGenerator.UseKind.UsedAsAddress, abort);
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(assignmentTarget.kind);
        }

        return expr;
    }

    private EvaluatorValue EvaluateAssignmentValue(BoundAssignmentOperator node, ValueWrapper<bool> abort) {
        if (!node.isRef) {
            return EvaluateExpression(node.right, abort);
        } else {
            var lhs = node.left;
            return EvaluateAddress(
                node.right,
                lhs.GetRefKind() is RefKind.RefConst or RefKind.RefConstParameter
                    ? AddressKind.ReadOnlyStrict
                    : AddressKind.Writeable,
                abort
            );
        }
    }

    private EvaluatorValue EvaluateUnaryOperator(BoundUnaryOperator node, ValueWrapper<bool> abort) {
        var operatorKind = node.operatorKind;
        var operand = EvaluateExpression(node.operand, abort);

        switch (operatorKind.Operator()) {
            case UnaryOperatorKind.UnaryMinus:
                if (operatorKind.OperandTypes() == UnaryOperatorKind.Int)
                    operand.int64 = -operand.int64;
                else
                    operand.@double = -operand.@double;

                break;
            case UnaryOperatorKind.LogicalNegation:
                operand.@bool = !operand.@bool;
                break;
            case UnaryOperatorKind.BitwiseComplement:
                operand.int64 = ~operand.int64;
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(operatorKind);
        }

        return operand;
    }

    private EvaluatorValue EvaluateBinaryOperator(BoundBinaryOperator node, ValueWrapper<bool> abort) {
        var operatorKind = node.operatorKind;
        var op = operatorKind.Operator();
        var left = EvaluateExpression(node.left, abort);

        if (left.kind == ValueKind.Null)
            return left;

        if (op == BinaryOperatorKind.ConditionalAnd) {
            if (!left.@bool)
                return left;

            return EvaluateExpression(node.right, abort);
        }

        if (op == BinaryOperatorKind.ConditionalOr) {
            if (left.@bool)
                return left;

            return EvaluateExpression(node.right, abort);
        }

        var right = EvaluateExpression(node.right, abort);

        if (right.kind == ValueKind.Null)
            return right;

        var operandType = operatorKind.OperandTypes();

        if (op is BinaryOperatorKind.Equal or BinaryOperatorKind.NotEqual) {
            if (node.right.IsLiteralNull()) {
                var isNull = left.kind == ValueKind.Null;
                return EvaluatorValue.Literal(isNull == (op == BinaryOperatorKind.Equal), SpecialType.Bool);
            }

            switch (operandType) {
                case BinaryOperatorKind.Int:
                    left.@bool = left.int64 == right.int64;
                    break;
                case BinaryOperatorKind.Decimal:
                    left.@bool = left.@double == right.@double;
                    break;
                case BinaryOperatorKind.Bool:
                    left.@bool = left.@bool == right.@bool;
                    break;
                case BinaryOperatorKind.String:
                    left.@bool = left.@string == right.@string;
                    break;
                case BinaryOperatorKind.Object:
                    left.@bool = left.ptr == right.ptr;
                    break;
                case BinaryOperatorKind.Type:
                    left.@bool = left.type == right.type;
                    break;
            }

            left.kind = ValueKind.Bool;

            if (op == BinaryOperatorKind.NotEqual)
                left.@bool = !left.@bool;

            return left;
        }

        switch (op) {
            case BinaryOperatorKind.Addition:
                if (operandType == BinaryOperatorKind.Int)
                    left.int64 += right.int64;
                else if (operandType == BinaryOperatorKind.String)
                    left.@string += right.@string;
                else
                    left.@double += right.@double;

                break;
            case BinaryOperatorKind.Subtraction:
                if (operandType == BinaryOperatorKind.Int)
                    left.int64 -= right.int64;
                else
                    left.@double -= right.@double;

                break;
            case BinaryOperatorKind.Multiplication:
                if (operandType == BinaryOperatorKind.Int)
                    left.int64 *= right.int64;
                else
                    left.@double *= right.@double;

                break;
            case BinaryOperatorKind.Division:
                if (operandType == BinaryOperatorKind.Int) {
                    if (right.int64 == 0)
                        throw new BelteDivideByZeroException(node.syntax.location);

                    left.int64 /= right.int64;
                } else {
                    if (right.@double == 0)
                        throw new BelteDivideByZeroException(node.syntax.location);

                    left.@double /= right.@double;
                }

                break;
            case BinaryOperatorKind.LessThan:
                if (operandType == BinaryOperatorKind.Int) {
                    left.@bool = left.int64 < right.int64;
                    left.kind = ValueKind.Bool;
                } else {
                    left.@bool = left.@double < right.@double;
                    left.kind = ValueKind.Bool;
                }

                break;
            case BinaryOperatorKind.GreaterThan:
                if (operandType == BinaryOperatorKind.Int) {
                    left.@bool = left.int64 > right.int64;
                    left.kind = ValueKind.Bool;
                } else {
                    left.@bool = left.@double > right.@double;
                    left.kind = ValueKind.Bool;
                }

                break;
            case BinaryOperatorKind.LessThanOrEqual:
                if (operandType == BinaryOperatorKind.Int) {
                    left.@bool = left.int64 <= right.int64;
                    left.kind = ValueKind.Bool;
                } else {
                    left.@bool = left.@double <= right.@double;
                    left.kind = ValueKind.Bool;
                }

                break;
            case BinaryOperatorKind.GreaterThanOrEqual:
                if (operandType == BinaryOperatorKind.Int) {
                    left.@bool = left.int64 >= right.int64;
                    left.kind = ValueKind.Bool;
                } else {
                    left.@bool = left.@double >= right.@double;
                    left.kind = ValueKind.Bool;
                }

                break;
            case BinaryOperatorKind.And:
                if (operandType == BinaryOperatorKind.Int)
                    left.int64 &= right.int64;
                else
                    left.@bool &= right.@bool;

                break;
            case BinaryOperatorKind.Or:
                if (operandType == BinaryOperatorKind.Int)
                    left.int64 |= right.int64;
                else
                    left.@bool |= right.@bool;

                break;
            case BinaryOperatorKind.Xor:
                if (operandType == BinaryOperatorKind.Int)
                    left.int64 ^= right.int64;
                else
                    left.@bool ^= right.@bool;

                break;
            case BinaryOperatorKind.LeftShift:
                left.int64 <<= (int)left.int64;
                break;
            case BinaryOperatorKind.RightShift:
                left.int64 >>= (int)left.int64;
                break;
            case BinaryOperatorKind.UnsignedRightShift:
                left.int64 >>>= (int)left.int64;
                break;
            case BinaryOperatorKind.Modulo:
                if (operandType == BinaryOperatorKind.Int)
                    left.int64 %= right.int64;
                else
                    left.@double %= right.@double;

                break;
            case BinaryOperatorKind.Power:
                if (operandType == BinaryOperatorKind.Int)
                    left.int64 = (long)Math.Pow(left.int64, right.int64);
                else
                    left.@double = Math.Pow(left.@double, right.@double);

                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(operatorKind);
        }

        return left;
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
        var frame = _stack.Peek();
        frame.Resize();
        return frame.layout.AllocateSlot(type, slotConstraints);
    }

    #endregion

    #region Calls

    private EvaluatorValue EvaluateCallExpression(
        BoundCallExpression node,
        CodeGenerator.UseKind useKind,
        ValueWrapper<bool> abort) {
        if (CheckStandardMap(
            node.syntax.location,
            node.method,
            node.receiver,
            node.arguments,
            abort,
            out var result,
            out var printed,
            out var io)) {
            lastOutputWasPrint = printed;
            containsIO = io;

            if (result is EvaluatorValue e) {
                return e;
            } else if (!node.method.returnsVoid) {
                return EvaluatorValue.Literal(result, node.method.returnType.specialType);
            } else {
                return EvaluatorValue.None;
            }
        }

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
            return value.loc[value.ptr];
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
            return value.loc[value.ptr];
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


    private bool CheckStandardMap(
        TextLocation location,
        MethodSymbol method,
        BoundExpression receiver,
        ImmutableArray<BoundExpression> arguments,
        ValueWrapper<bool> abort,
        out object result,
        out bool printed,
        out bool io) {
        printed = false;
        io = false;
        result = null;

        // First check if we are in a graphics project before comparing
        // (otherwise would unnecessarily create the overhead of constructing the Graphics type)
        if (_context.options.outputKind == OutputKind.GraphicsApplication) {
            if (method.containingType.Equals(GraphicsLibrary.Graphics.underlyingNamedType))
                return HandleGraphicsCall(location, method, arguments, abort, out result);
        }

        var mapKey = LibraryHelpers.BuildMapKey(method);

        if (mapKey == "Nullable_get_Value") {
            result = NullAssertValue(receiver, abort);
            return true;
        }

        if (mapKey == "Nullable_get_HasValue") {
            var receiverValue = EvaluateExpression(receiver, abort);
            result = EvaluatorValue.Literal(receiverValue.kind != ValueKind.Null, SpecialType.Bool);
            return true;
        }

        if (method.containingType.Equals(StandardLibrary.Console.underlyingNamedType) ||
            method.containingType.Equals(StandardLibrary.Math.underlyingNamedType) ||
            method.containingType.Equals(StandardLibrary.LowLevel.underlyingNamedType) ||
            method.containingType.Equals(StandardLibrary.Time.underlyingNamedType) ||
            method.containingType.Equals(StandardLibrary.Directory.underlyingNamedType) ||
            method.containingType.Equals(StandardLibrary.File.underlyingNamedType) ||
            method.containingType.Equals(StandardLibrary.String.underlyingNamedType) ||
            method.containingType.Equals(StandardLibrary.Random.underlyingNamedType)) {
            switch (mapKey) {
                case "LowLevel_GetHashCode_O":
                    result = _context.heap[EvaluateExpression(arguments[0], abort).ptr].GetHashCode();
                    return true;
                case "LowLevel_GetTypeName_O":
                    result = _context.heap[EvaluateExpression(arguments[0], abort).ptr].type.name;
                    return true;
                case "Random_RandInt_I?":
                    _lazyRandom ??= new Random();
                    var max = (int)EvaluateExpression(arguments[0], abort).int64;
                    result = Convert.ToInt64(_lazyRandom.Next(max));
                    return true;
                case "LowLevel_ThrowNullConditionException":
                    throw new BelteNullConditionException(location);
                case "Random_Random":
                    _lazyRandom ??= new Random();
                    result = _lazyRandom.NextDouble();
                    return true;
                case "LowLevel_Sort_A?": {
                        var arrayPtr = EvaluateExpression(arguments[0], abort);

                        if (arrayPtr.kind != ValueKind.HeapPtr)
                            return true;

                        var array = _context.heap[arrayPtr.ptr];

                        // TODO
                        // Array.Sort(array.fields, (a, b) => Convert.ToDouble(a.value).CompareTo(Convert.ToDouble(b.value)));
                    }

                    return true;
                case "String_Split_SS": {
                        var args = arguments.Select(a => EvaluateExpression(a, abort).@string).ToArray();
                        var text = args[0];
                        var separator = args[1];
                        var res = text.Split(separator);

                        result = res.Select(r => EvaluatorValue.Literal(r, SpecialType.String)).ToArray();
                    }

                    return true;
                case "Console_Print_S?":
                case "Console_Print_A?":
                case "Console_Print_O?":
                    printed = true;

                    if (arguments[0].type.StrippedType().isObjectType) {
                        var argument = EvaluateExpression(arguments[0], abort);
                        var toStringResult = InvokeStaticMethod(_toStringMethod, [argument], abort);
                        var func = StandardLibrary.EvaluatorMap[mapKey];
                        result = func(toStringResult, null, null);
                        return true;
                    }

                    break;
            }

            var function = StandardLibrary.EvaluatorMap[mapKey];
            var valueArguments = arguments
                .Select(a => EvaluatorValue.Format(EvaluateExpression(a, abort), _context))
                .ToArray();

            switch (mapKey) {
                case "File_Copy_SS":
                    valueArguments[1] = GetFilePath((string)valueArguments[1], location);
                    goto case "Directory_Create_S";
                case "Directory_Create_S":
                case "Directory_Delete_S":
                case "Directory_Exists_S":
                case "File_AppendText_SS":
                case "File_Create_S":
                case "File_Delete_S":
                case "File_Exists_S":
                case "File_ReadText_S":
                case "File_WriteText_SS":
                    valueArguments[0] = GetFilePath((string)valueArguments[0], location);
                    break;
            }

            result = arguments.Length switch {
                0 => function(null, null, null),
                1 => function(valueArguments[0], null, null),
                2 => function(valueArguments[0], valueArguments[1], null),
                3 => function(valueArguments[0], valueArguments[1], valueArguments[2]),
                _ => throw ExceptionUtilities.UnexpectedValue(arguments.Length),
            };

            return true;
        } else {
            return false;
        }
    }

    private static string GetFilePath(string path, TextLocation location) {
        if (File.Exists(path))
            return path;

        var filePath = string.Join(
            Path.DirectorySeparatorChar,
            location.fileName.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).SkipLast(1)
        );

        var appendedPath = Path.Combine(filePath, path);

        if (File.Exists(appendedPath))
            return appendedPath;

        return null;
    }

    private bool HandleGraphicsCall(
        TextLocation location,
        MethodSymbol method,
        ImmutableArray<BoundExpression> arguments,
        ValueWrapper<bool> abort,
        out object result) {
        result = null;
        var mapKey = LibraryHelpers.BuildMapKey(method);

        return true;
    }

    private void StartGraphics(string title, int width, int height, bool usePointClamp, ValueWrapper<bool> abort) {
        _context.maintainThread = true;

        GraphicsHandler.Title = title;
        GraphicsHandler.Width = width;
        GraphicsHandler.Height = height;

        if (_context.graphicsThread is not null && _context.graphicsThread.IsAlive) {
            _context.createWindow = true;
            _context.graphicsHandler?.Exit();
            return;
        }

        _context.graphicsThread = new Thread(() => {
            while (_context.maintainThread) {
                if (_context.createWindow) {
                    _context.createWindow = false;
                    using var graphicsHandler = new GraphicsHandler(this, abort, usePointClamp);
                    _context.graphicsHandler = graphicsHandler;
                    graphicsHandler.Run();
                } else {
                    Thread.Sleep(100);
                }
            }
        }) {
            Name = "Evaluator.GraphicsThread",
            IsBackground = true
        };

        if (OperatingSystem.IsWindows())
            _context.graphicsThread.SetApartmentState(ApartmentState.STA);

        _context.graphicsThread.Start();
    }

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
