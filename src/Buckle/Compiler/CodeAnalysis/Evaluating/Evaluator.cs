using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Text;
using Shared;

namespace Buckle.CodeAnalysis.Evaluating;

/// <summary>
/// Evaluates BoundStatements inline similar to an interpreter.
/// </summary>
internal sealed class Evaluator {
    private static readonly Symbol HiddenSpriteData = new SynthesizedLabelSymbol("texture");
    private static readonly Symbol HiddenTextData = new SynthesizedLabelSymbol("spriteFont");

    private readonly BoundProgram _program;
    private readonly EvaluatorContext _context;
    private readonly Stack<Dictionary<Symbol, EvaluatorObject>> _locals;
    private readonly Stack<EvaluatorObject> _enclosingTypes;

    private EvaluatorObject _lastValue;
    private bool _hasValue;
    private MethodSymbol _lazyToString;
    private Random _lazyRandom;

    /// <summary>
    /// Creates an <see cref="Evaluator" /> that can evaluate a <see cref="BoundProgram" /> (provided globals).
    /// </summary>
    /// <param name="program"><see cref="BoundProgram" />.</param>
    /// <param name="globals">Globals.</param>
    /// <param name="arguments">Runtime arguments.</param>
    internal Evaluator(
        BoundProgram program,
        EvaluatorContext context,
        string[] arguments) {
        _context = context;
        _program = program;
        _enclosingTypes = new Stack<EvaluatorObject>();
        _locals = new Stack<Dictionary<Symbol, EvaluatorObject>>();
        _locals.Push([]);
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

    private MethodSymbol toStringMethod {
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

        if (programType.isStatic) {
            EnterClassScope(new EvaluatorObject([], entryPoint.containingType));
        } else {
            var programObject = CreateObject(programType);
            EnterClassScope(programObject);
            var constructor = programType.constructors.Where(c => c.parameterCount == 0).FirstOrDefault();

            if (constructor is not null)
                InvokeMethod(constructor, [], abort);
        }

        var result = EvaluateStatement(entryPointBody, abort, out _);

        // Wait until Main finishes before the first call of Update
        if (_context.maintainThread) {
            while (_context.graphicsHandler is null)
                ;
        }

        if (_program.updatePoint is not null)
            _context.graphicsHandler?.SetUpdateHandler(UpdateCaller);

        hasValue = _hasValue;
        return hasValue ? Value(result, true) : null;
    }

    #region Internal Model

    private object Value(EvaluatorObject value, bool traceCollections = false) {
        if (value.isReference)
            return GetVariableValue(value.reference, traceCollections);
        else if (value.value is EvaluatorObject e)
            return Value(e, traceCollections);
        else if (value.value is EvaluatorObject[] && traceCollections)
            return CollectionValue(value.value as EvaluatorObject[]);
        else if (traceCollections && value.value is null && value.members is not null)
            return DictionaryValue(value.members, value.type);
        else
            return value.value;
    }

    private Dictionary<object, object> DictionaryValue(
        Dictionary<Symbol, EvaluatorObject> value,
        TypeSymbol containingType) {
        var dictionary = new Dictionary<object, object>();

        foreach (var pair in value) {
            if (pair.Key is FieldSymbol) {
                var name = pair.Key.containingType.Equals(containingType)
                    ? pair.Key.name
                    : $"{pair.Key.containingType.name}.{pair.Key.name}";

                dictionary.Add(name, Value(pair.Value, true));
            }
        }

        return dictionary;
    }

    private object[] CollectionValue(EvaluatorObject[] value) {
        var builder = new object[value.Length];

        for (var i = 0; i < value.Length; i++)
            builder[i] = Value(value[i], true);

        return builder;
    }

    private object GetVariableValue(Symbol variable, bool traceCollections = false) {
        var value = Get(variable);

        try {
            return Value(value, traceCollections);
        } catch (BelteInternalException) {
            throw new BelteEvaluatorException(
                $"Reference cannot be deferred (what it was referencing was likely redefined)"
            );
        }
    }

    private void Create(DataContainerSymbol symbol, EvaluatorObject value) {
        if (symbol.isGlobal) {
            _context.AddOrUpdateSymbol(symbol, Copy(value));
        } else {
            var locals = _locals.Peek();
            var set = false;

            foreach (var local in locals) {
                if (local.Key.name == symbol.name) {
                    locals.Remove(local.Key);
                    locals[symbol] = Copy(value);
                    set = true;
                    break;
                }
            }

            if (!set)
                locals[symbol] = Copy(value);
        }
    }

    private EvaluatorObject Copy(EvaluatorObject evaluatorObject) {
        if (evaluatorObject.reference is not null && !evaluatorObject.isExplicitReference)
            return Copy(Get(evaluatorObject.reference));
        else if (evaluatorObject.reference is not null)
            return new EvaluatorObject(evaluatorObject.reference, evaluatorObject.type, isExplicitReference: true);
        else if (evaluatorObject.members is not null)
            return new EvaluatorObject(Copy(evaluatorObject.members), evaluatorObject.type);
        else
            return new EvaluatorObject(evaluatorObject.value, evaluatorObject.type);
    }

    private Dictionary<Symbol, EvaluatorObject> Copy(Dictionary<Symbol, EvaluatorObject> members) {
        var newMembers = new Dictionary<Symbol, EvaluatorObject>();

        foreach (var member in members)
            newMembers.Add(member.Key, Copy(member.Value));

        return newMembers;
    }

    private EvaluatorObject Get(Symbol symbol) {
        if (symbol is DataContainerSymbol d && d.isGlobal) {
            if (_context.TryGetSymbol(d, out var evaluatorObject))
                return evaluatorObject;
        } else {
            foreach (var frame in _locals) {
                if (frame.TryGetValue(symbol, out var evaluatorObject))
                    return evaluatorObject;
            }
        }

        throw ExceptionUtilities.Unreachable();
    }

    private void Assign(EvaluatorObject left, EvaluatorObject right) {
        right = Dereference(right, false);
        left = Dereference(left, false);

        if (right.isExplicitReference) {
            left.reference = right.reference;
            left.isReference = true;
            return;
        } else if (left.isExplicitReference) {
            left = Dereference(left);
        }

        if (right.members is null)
            left.members = null;

        if (right.value is null && right.members != null)
            left.members = Copy(right.members);
        else
            left.value = Value(right);

        left.type = right.type;
    }

    private EvaluatorObject GetFromScopeWithFallback(
        DataContainerSymbol variable,
        Dictionary<IDataContainerSymbol, EvaluatorObject> scope) {
        if (scope.TryGetValue(variable, out var evaluatorObject))
            return evaluatorObject;

        return Get(variable);
    }

    private EvaluatorObject Dereference(EvaluatorObject reference, bool dereferenceOnExplicit = true) {
        while (reference.isReference) {
            if (!dereferenceOnExplicit && reference.isExplicitReference)
                break;

            reference = Get(reference.reference);
        }

        return reference;
    }

    private EvaluatorObject CreateObject(NamedTypeSymbol type) {
        var members = new Dictionary<Symbol, EvaluatorObject>();
        var typeMembers = type.GetMembers();

        foreach (var field in typeMembers.Where(f => f is FieldSymbol).Select(f => f as FieldSymbol)) {
            var value = new EvaluatorObject(null, field.type);

            if (field.refKind != RefKind.None) {
                value.isReference = true;
                value.isExplicitReference = true;
            }

            members.Add(field, value);
        }

        // TODO Is this still necessary with the new symbol system?
        // var trueType = ClarifyType(type);
        return new EvaluatorObject(members, type);
    }

    private void EnterClassScope(EvaluatorObject @class) {
        var classLocalBuffer = new Dictionary<Symbol, EvaluatorObject>();

        foreach (var member in @class.members) {
            if (member.Key is FieldSymbol f) {
                // If the symbol is already present it could be outdated and should be replaced
                // If it isn't outdated no harm in replacing it
                classLocalBuffer.Remove(f);
                classLocalBuffer.Add(f, member.Value);
            }
        }

        _enclosingTypes.Push(@class);
        _locals.Push(classLocalBuffer);
    }

    private void ExitClassScope() {
        _enclosingTypes.Pop();
        _locals.Pop();
    }

    #endregion

    #region Statements

    private EvaluatorObject EvaluateStatement(
        BoundBlockStatement block,
        ValueWrapper<bool> abort,
        out bool hasReturn,
        bool insideTry = false) {
        _hasValue = false;
        hasReturn = false;

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
                    _lastValue = null;

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
                        var conditionValue = (bool)Value(condition);

                        if (conditionValue == cgs.jumpIfTrue)
                            index = labelToIndex[cgs.label];
                        else
                            index++;

                        break;
                    case BoundKind.ReturnStatement:
                        var returnStatement = (BoundReturnStatement)s;

                        if (returnStatement.expression is not null) {
                            _lastValue = Copy(EvaluateExpression(returnStatement.expression, abort));
                            _hasValue = true;
                        } else if (_lastValue is not null) {
                            _hasValue = true;
                        } else {
                            _hasValue = false;
                        }

                        hasReturn = true;

                        return _lastValue;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(s.kind);
                }
            }

            return _lastValue;
        } catch (Exception e) {
            if (abort)
                return EvaluatorObject.Null;

            if (insideTry)
                throw;

            exceptions.Add(e);
            lastOutputWasPrint = false;
            _hasValue = false;

            if (!Console.IsOutputRedirected) {
                // TODO Move this logic to the Repl
                if (Console.CursorLeft != 0)
                    Console.WriteLine();

                var previous = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($"Unhandled exception ({e.GetType()}): ");
                Console.ForegroundColor = previous;
                Console.WriteLine(e.Message);
            }

            return EvaluatorObject.Null;
        }
    }

    private void EvaluateExpressionStatement(BoundExpressionStatement statement, ValueWrapper<bool> abort) {
        _lastValue = EvaluateExpression(statement.expression, abort);
    }

    private void EvaluateLocalDeclarationStatement(BoundLocalDeclarationStatement statement, ValueWrapper<bool> abort) {
        var value = EvaluateExpression(statement.declaration.initializer, abort);
        var local = statement.declaration.dataContainer;
        _lastValue = null;

        if (local.isRef)
            value.isExplicitReference = true;

        Create(local, value);
    }

    #endregion

    #region Expressions

    private EvaluatorObject EvaluateExpression(BoundExpression expression, ValueWrapper<bool> abort) {
        if (expression.constantValue is not null)
            return EvaluateConstant(expression.constantValue);

        return expression.kind switch {
            BoundKind.ThisExpression => EvaluateThisExpression(),
            BoundKind.BaseExpression => EvaluateBaseExpression(),
            BoundKind.CastExpression => EvaluateCastExpression((BoundCastExpression)expression, abort),
            BoundKind.DataContainerExpression => EvaluateDataContainerExpression((BoundDataContainerExpression)expression),
            BoundKind.ParameterExpression => EvaluateParameterExpression((BoundParameterExpression)expression),
            BoundKind.FieldAccessExpression => EvaluateFieldAccessExpression((BoundFieldAccessExpression)expression, abort),
            BoundKind.AssignmentOperator => EvaluateAssignmentOperator((BoundAssignmentOperator)expression, abort),
            BoundKind.UnaryOperator => EvaluateUnaryOperator((BoundUnaryOperator)expression, abort),
            BoundKind.BinaryOperator => EvaluateBinaryOperator((BoundBinaryOperator)expression, abort),
            BoundKind.NullAssertOperator => EvaluateNullAssertOperator((BoundNullAssertOperator)expression, abort),
            BoundKind.AsOperator => EvaluateAsOperator((BoundAsOperator)expression, abort),
            BoundKind.IsOperator => EvaluateIsOperator((BoundIsOperator)expression, abort),
            BoundKind.ConditionalOperator => EvaluateConditionalOperator((BoundConditionalOperator)expression, abort),
            BoundKind.CallExpression => EvaluateCallExpression((BoundCallExpression)expression, abort),
            BoundKind.ObjectCreationExpression => EvaluateObjectCreationExpression((BoundObjectCreationExpression)expression, abort),
            BoundKind.ArrayCreationExpression => EvaluateArrayCreationExpression((BoundArrayCreationExpression)expression, abort),
            BoundKind.InitializerList => EvaluateInitializerList((BoundInitializerList)expression, abort),
            BoundKind.ArrayAccessExpression => EvaluateArrayAccessExpression((BoundArrayAccessExpression)expression, abort),
            BoundKind.TypeExpression => EvaluateTypeExpression((BoundTypeExpression)expression, abort),
            BoundKind.TypeOfExpression => EvaluateTypeOfExpression((BoundTypeOfExpression)expression, abort),
            BoundKind.MethodGroup => EvaluateMethodGroup((BoundMethodGroup)expression, abort),
            _ => throw ExceptionUtilities.UnexpectedValue(expression.kind),
        };
    }

    private EvaluatorObject EvaluateConstant(ConstantValue constantValue) {
        // TODO is this clarity worth the performance loss?
        if (constantValue.specialType == SpecialType.None)
            return new EvaluatorObject(constantValue.value, null);

        var type = CorLibrary.GetSpecialType(constantValue.specialType);
        return new EvaluatorObject(constantValue.value, type);
    }

    private EvaluatorObject EvaluateTypeOfExpression(BoundTypeOfExpression expression, ValueWrapper<bool> _) {
        return new EvaluatorObject(expression.sourceType, expression.type);
    }

    private EvaluatorObject EvaluateMethodGroup(BoundMethodGroup methodGroup, ValueWrapper<bool> _) {
        return new EvaluatorObject(methodGroup, methodGroup.type);
    }

    private EvaluatorObject EvaluateInitializerList(BoundInitializerList node, ValueWrapper<bool> abort) {
        var builder = new EvaluatorObject[node.items.Length];

        for (var i = 0; i < node.items.Length; i++)
            builder[i] = EvaluateExpression(node.items[i], abort);

        return new EvaluatorObject(builder, node.type);
    }

    private EvaluatorObject EvaluateTypeExpression(BoundTypeExpression _, ValueWrapper<bool> _2) {
        // This should only ever be called when an invalid expression statement makes it through binding without err
        // because script compilation ignores normal expression statement restrictions.
        //
        // `Console;`
        //
        return EvaluatorObject.Null;
    }

    private EvaluatorObject EvaluateArrayAccessExpression(BoundArrayAccessExpression node, ValueWrapper<bool> abort) {
        var receiver = EvaluateExpression(node.receiver, abort);
        var index = EvaluateExpression(node.index, abort);
        var array = (EvaluatorObject[])Value(receiver);
        var indexValue = Convert.ToInt32(Value(index));
        return array[indexValue];
    }

    private EvaluatorObject EvaluateArrayCreationExpression(BoundArrayCreationExpression node, ValueWrapper<bool> _) {
        var array = EvaluatorObject.Null;
        var arrayType = (ArrayTypeSymbol)node.type.StrippedType();

        // TODO There is probably a more efficient algorithm for this
        foreach (var size in node.sizes) {
            foreach (var element in IterateElements(array)) {
                var members = new EvaluatorObject[size];

                for (var i = 0; i < size; i++) {
                    // TODO This will use a default when default expression is added for primitives instead of null
                    members[i] = EvaluatorObject.Null;
                }

                element.value = members;
            }
        }

        return array;

        static List<EvaluatorObject> IterateElements(EvaluatorObject evaluatorObject) {
            if (evaluatorObject.value is null) {
                return [evaluatorObject];
            } else {
                var objects = new List<EvaluatorObject>();

                foreach (var subElement in evaluatorObject.value as EvaluatorObject[])
                    objects.AddRange(IterateElements(subElement));

                return objects;
            }
        }
    }

    private EvaluatorObject EvaluateObjectCreationExpression(
        BoundObjectCreationExpression node,
        ValueWrapper<bool> abort) {
        var newObject = CreateObject((NamedTypeSymbol)node.type);
        InvokeMethodWithResolvedReceiver(node.constructor, node.arguments, newObject, abort);
        return newObject;
    }

    private EvaluatorObject EvaluateCallExpression(BoundCallExpression expression, ValueWrapper<bool> abort) {
        if (CheckStandardMap(
            expression.method,
            expression.arguments,
            abort,
            out var result,
            out var printed,
            out var io)) {
            lastOutputWasPrint = printed;
            containsIO = io;

            if (result is EvaluatorObject e)
                return e;
            else if (!expression.method.returnsVoid)
                return new EvaluatorObject(result, expression.method.returnType);
            else
                return null;
        }

        return InvokeMethod(expression.method, expression.arguments, expression.receiver, abort);
    }

    private EvaluatorObject InvokeMethod(
        MethodSymbol method,
        ImmutableArray<BoundExpression> arguments,
        ValueWrapper<bool> abort) {
        return InvokeMethodWithResolvedReceiver(method, arguments, null, abort);
    }

    private EvaluatorObject InvokeMethodWithResolvedReceiver(
        MethodSymbol method,
        ImmutableArray<BoundExpression> arguments,
        EvaluatorObject receiver,
        ValueWrapper<bool> abort) {
        if (method.isAbstract || method.isVirtual) {
            var type = Dereference(receiver).type;
            var newMethod = type
                .GetMembersUnordered()
                .Where(s => s is MethodSymbol m && m.overriddenMethod == method)
                .FirstOrDefault() as MethodSymbol;

            if (newMethod is not null)
                method = newMethod;
        }

        var locals = new Dictionary<Symbol, EvaluatorObject>();

        if (method.callingConvention == CallingConvention.Template) ;
        // AddTemplatesToLocals(method.templateParameters, method.templateArguments, locals, abort);

        for (var i = 0; i < arguments.Length; i++) {
            var parameter = method.parameters[i];
            var value = EvaluateExpression(arguments[i], abort);

            while (parameter.refKind != RefKind.None && value.isReference)
                value = Get(value.reference);

            locals.Add(parameter, Copy(value));
        }

        _locals.Push(locals);

        _program.TryGetMethodBodyIncludingParents(method, out var statement);
        // var templateConstantDepth = _templateConstantDepth;
        var enteredScope = false;

        // !
        if (receiver is not null /*&& (receiver.isReference || expression is BoundObjectCreationExpression)*/) {
            // On an expression such as 'myInstance.Method()', we need to enter the 'myInstance' class scope
            // in case 'Method' uses 'this'
            // If what we get here is not a reference, it is a static accession and the needed scoped members have
            // already been pushed by 'EvaluateType'.
            receiver = Dereference(receiver);

            if (receiver.members is not null) {
                EnterClassScope(receiver);
                enteredScope = true;
            }
        }

        var result = EvaluateStatement(statement, abort, out _);

        // while (_templateConstantDepth > templateConstantDepth) {
        //     _templateConstantDepth--;
        //     _locals.Pop();
        // }

        _locals.Pop();

        if (enteredScope)
            ExitClassScope();

        return result;
    }

    private EvaluatorObject InvokeMethod(
        MethodSymbol method,
        ImmutableArray<BoundExpression> arguments,
        BoundExpression receiver,
        ValueWrapper<bool> abort) {
        var receiverObject = default(EvaluatorObject);

        // Have to check receiver too because constructors don't have receivers
        if (receiver is not null && method.callingConvention == CallingConvention.HasThis) {
            receiverObject = EvaluateExpression(receiver, abort);
            var dereferencedReceiver = Dereference(receiverObject);

            if (dereferencedReceiver.members is null)
                throw new NullReferenceException();
        }

        return InvokeMethodWithResolvedReceiver(method, arguments, receiverObject, abort);
    }

    private EvaluatorObject EvaluateDataContainerExpression(BoundDataContainerExpression expression) {
        return new EvaluatorObject(expression.dataContainer, expression.dataContainer.type);
    }

    private EvaluatorObject EvaluateConditionalOperator(
        BoundConditionalOperator expression,
        ValueWrapper<bool> abort) {
        var condition = EvaluateExpression(expression.condition, abort);
        var conditionValue = Value(condition);

        if ((bool)conditionValue)
            return EvaluateExpression(expression.trueExpression, abort);
        else
            return EvaluateExpression(expression.falseExpression, abort);
    }

    private EvaluatorObject EvaluateNullAssertOperator(
        BoundNullAssertOperator expression,
        ValueWrapper<bool> abort) {
        var value = EvaluateExpression(expression.operand, abort);
        var dereferenced = Dereference(value);

        if (dereferenced.members is null &&
            Value(dereferenced) is null &&
            expression.operand.type.specialType != SpecialType.Type) {
            throw new NullReferenceException();
        }

        return value;
    }

    private EvaluatorObject EvaluateAsOperator(BoundAsOperator expression, ValueWrapper<bool> abort) {
        var left = EvaluateExpression(expression.left, abort);
        var leftValue = Value(left);
        var dereferenced = Dereference(left);

        if (dereferenced.members is null)
            return new EvaluatorObject(leftValue, expression.type);

        if (dereferenced.type.InheritsFromIgnoringConstruction((NamedTypeSymbol)expression.right.type))
            return left;

        return EvaluatorObject.Null;
    }

    private EvaluatorObject EvaluateIsOperator(BoundIsOperator expression, ValueWrapper<bool> abort) {
        var left = EvaluateExpression(expression.left, abort);
        var right = expression.right;
        var leftValue = Value(left);
        var dereferenced = Dereference(left);
        var isNot = expression.isNot;

        if (right.IsLiteralNull()) {
            if (dereferenced.members is null && leftValue is null &&
                (expression.left.type.specialType != SpecialType.Type || left.type is null)) {
                return new EvaluatorObject(!isNot, expression.type);
            }

            return new EvaluatorObject(isNot, expression.type);
        }

        if (leftValue is null && dereferenced.members is null)
            return new EvaluatorObject(isNot, expression.type);

        if (dereferenced.members is null) {
            var isTrue = (right.type.StrippedType().specialType == SpecialType.Any) ||
                (SpecialTypeExtensions.SpecialTypeFromLiteralValue(leftValue) == right.type.StrippedType().specialType);

            return new EvaluatorObject(isNot ^ isTrue, expression.type);
        }

        if (dereferenced.type.InheritsFromIgnoringConstruction((NamedTypeSymbol)expression.right.type))
            return new EvaluatorObject(!isNot, expression.type);
        else
            return new EvaluatorObject(isNot, expression.type);
    }

    private EvaluatorObject EvaluateUnaryOperator(BoundUnaryOperator expression, ValueWrapper<bool> abort) {
        var operand = EvaluateExpression(expression.operand, abort);
        var operandValue = Value(operand);
        var opKind = expression.operatorKind.Operator();

        if (operandValue is null)
            return EvaluatorObject.Null;

        var expressionType = expression.type.specialType;
        object result;

        switch (opKind) {
            case UnaryOperatorKind.UnaryPlus:
                return operand;
            case UnaryOperatorKind.UnaryMinus:
                result = expressionType == SpecialType.Int ? -(long)operandValue : -Convert.ToDouble(operandValue);
                break;
            case UnaryOperatorKind.LogicalNegation:
                result = !(bool)operandValue;
                break;
            case UnaryOperatorKind.BitwiseComplement:
                result = ~(long)operandValue;
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(expression.operatorKind);
        }

        return new EvaluatorObject(result, expression.type);
    }

    private EvaluatorObject EvaluateBinaryOperator(BoundBinaryOperator expression, ValueWrapper<bool> abort) {
        var left = EvaluateExpression(expression.left, abort);
        var leftValue = Value(left);
        var opKind = expression.operatorKind.OperatorWithConditional();

        if (opKind == BinaryOperatorKind.ConditionalAnd) {
            if (leftValue is null || !(bool)leftValue)
                return new EvaluatorObject(false, expression.type);

            var shortCircuitRight = EvaluateExpression(expression.right, abort);
            var shortCircuitRightValue = Value(shortCircuitRight);

            if (shortCircuitRightValue is null || !(bool)shortCircuitRightValue)
                return new EvaluatorObject(false, expression.type);

            return new EvaluatorObject(true, expression.type);
        }

        if (opKind == BinaryOperatorKind.ConditionalOr) {
            if (leftValue != null && (bool)leftValue)
                return new EvaluatorObject(true, expression.type);

            var shortCircuitRight = EvaluateExpression(expression.right, abort);
            var shortCircuitRightValue = Value(shortCircuitRight);

            if (shortCircuitRightValue != null && (bool)shortCircuitRightValue)
                return new EvaluatorObject(true, expression.type);

            return new EvaluatorObject(false, expression.type);
        }

        var right = EvaluateExpression(expression.right, abort);
        var rightValue = Value(right);

        if (opKind is BinaryOperatorKind.Equal or BinaryOperatorKind.NotEqual) {
            if (expression.left.type.specialType == SpecialType.Type) {
                if ((leftValue as BoundTypeExpression).type.Equals((rightValue as BoundTypeExpression).type))
                    return new EvaluatorObject(opKind == BinaryOperatorKind.Equal, expression.type);
                else
                    return new EvaluatorObject(opKind == BinaryOperatorKind.NotEqual, expression.type);
            }

            if (expression.left.type.specialType == SpecialType.Object) {
                // Reference equality
                var refEquals = left.reference == right.reference;
                var positive = opKind == BinaryOperatorKind.Equal;
                return new EvaluatorObject(positive == refEquals, expression.type);
            }
        }

        if (leftValue is null || rightValue is null)
            return EvaluatorObject.Null;

        var expressionType = expression.type.StrippedType().specialType;
        var leftType = expression.left.type.StrippedType().specialType;
        object result;

        switch (opKind) {
            case BinaryOperatorKind.Addition:
                if (expressionType == SpecialType.Int)
                    result = (long)leftValue + (long)rightValue;
                else if (expressionType == SpecialType.String)
                    result = (string)leftValue + (string)rightValue;
                else
                    result = Convert.ToDouble(leftValue) + Convert.ToDouble(rightValue);

                break;
            case BinaryOperatorKind.Subtraction:
                if (expressionType == SpecialType.Int)
                    result = (long)leftValue - (long)rightValue;
                else
                    result = Convert.ToDouble(leftValue) - Convert.ToDouble(rightValue);

                break;
            case BinaryOperatorKind.Multiplication:
                if (expressionType == SpecialType.Int)
                    result = (long)leftValue * (long)rightValue;
                else
                    result = Convert.ToDouble(leftValue) * Convert.ToDouble(rightValue);

                break;
            case BinaryOperatorKind.Division:
                if (expressionType == SpecialType.Int)
                    result = (long)leftValue / (long)rightValue;
                else
                    result = Convert.ToDouble(leftValue) / Convert.ToDouble(rightValue);

                break;
            case BinaryOperatorKind.Equal:
                result = Equals(leftValue, rightValue);
                break;
            case BinaryOperatorKind.NotEqual:
                result = !Equals(leftValue, rightValue);
                break;
            case BinaryOperatorKind.LessThan:
                if (leftType == SpecialType.Int)
                    result = (long)leftValue < (long)rightValue;
                else
                    result = Convert.ToDouble(leftValue) < Convert.ToDouble(rightValue);

                break;
            case BinaryOperatorKind.GreaterThan:
                if (leftType == SpecialType.Int)
                    result = (long)leftValue > (long)rightValue;
                else
                    result = Convert.ToDouble(leftValue) > Convert.ToDouble(rightValue);

                break;
            case BinaryOperatorKind.LessThanOrEqual:
                if (leftType == SpecialType.Int)
                    result = (long)leftValue <= (long)rightValue;
                else
                    result = Convert.ToDouble(leftValue) <= Convert.ToDouble(rightValue);

                break;
            case BinaryOperatorKind.GreaterThanOrEqual:
                if (leftType == SpecialType.Int)
                    result = (long)leftValue >= (long)rightValue;
                else
                    result = Convert.ToDouble(leftValue) >= Convert.ToDouble(rightValue);

                break;
            case BinaryOperatorKind.And:
                if (expressionType == SpecialType.Int)
                    result = (long)leftValue & (long)rightValue;
                else
                    result = (bool)leftValue & (bool)rightValue;

                break;
            case BinaryOperatorKind.Or:
                if (expressionType == SpecialType.Int)
                    result = (long)leftValue | (long)rightValue;
                else
                    result = (bool)leftValue | (bool)rightValue;

                break;
            case BinaryOperatorKind.Xor:
                if (expressionType == SpecialType.Int)
                    result = (long)leftValue ^ (long)rightValue;
                else
                    result = (bool)leftValue ^ (bool)rightValue;

                break;
            case BinaryOperatorKind.LeftShift:
                result = (long)leftValue << Convert.ToInt32(rightValue);
                break;
            case BinaryOperatorKind.RightShift:
                result = (long)leftValue >> Convert.ToInt32(rightValue);
                break;
            case BinaryOperatorKind.UnsignedRightShift:
                result = (long)leftValue >>> Convert.ToInt32(rightValue);
                break;
            case BinaryOperatorKind.Modulo:
                if (expressionType == SpecialType.Int)
                    result = (long)leftValue % (long)rightValue;
                else
                    result = Convert.ToDouble(leftValue) % Convert.ToDouble(rightValue);

                break;
            case BinaryOperatorKind.Power:
                if (expressionType == SpecialType.Int)
                    result = Math.Pow((long)leftValue, (long)rightValue);
                else
                    result = Math.Pow(Convert.ToDouble(leftValue), Convert.ToDouble(rightValue));

                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(expression.operatorKind);
        }

        return new EvaluatorObject(result, expression.type);
    }

    private EvaluatorObject EvaluateAssignmentOperator(
        BoundAssignmentOperator expression,
        ValueWrapper<bool> abort) {
        var left = EvaluateExpression(expression.left, abort);
        var right = EvaluateExpression(expression.right, abort);

        if (expression.isRef)
            right.isExplicitReference = true;

        Assign(left, right);
        return right;
    }

    private EvaluatorObject EvaluateParameterExpression(BoundParameterExpression expression) {
        return new EvaluatorObject(expression.parameter, expression.parameter.type);
    }

    private EvaluatorObject EvaluateFieldAccessExpression(
        BoundFieldAccessExpression expression,
        ValueWrapper<bool> abort) {
        var operand = Dereference(EvaluateExpression(expression.receiver, abort), true);
        return operand.members[expression.field];
    }

    private EvaluatorObject EvaluateThisExpression() {
        return _enclosingTypes.Peek();
    }

    private EvaluatorObject EvaluateBaseExpression() {
        return _enclosingTypes.Peek();
    }

    private EvaluatorObject EvaluateCastExpression(BoundCastExpression expression, ValueWrapper<bool> abort) {
        var value = EvaluateExpression(expression.operand, abort);
        return EvaluateCast(value, expression.operand.type, expression.type);
    }

    private EvaluatorObject EvaluateCast(EvaluatorObject value, TypeSymbol source, TypeSymbol target) {
        var dereferenced = Dereference(value);
        target = target.StrippedType();
        source = source.StrippedType();

        if (dereferenced.members is null) {
            var valueValue = Value(value);

            if (target.specialType != SpecialType.Nullable && value is null)
                throw new NullReferenceException();

            if (source.Equals(target, TypeCompareKind.IgnoreNullability))
                return value;

            return new EvaluatorObject(LiteralUtilities.Cast(valueValue, target), target);
        }

        if (dereferenced.type.InheritsFromIgnoringConstruction((NamedTypeSymbol)target))
            return value;

        throw new InvalidCastException();
    }

    #endregion

    private bool CheckStandardMap(
        MethodSymbol method,
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
        if (_context.options.outputKind == OutputKind.Graphics) {
            if (method.containingType.Equals(GraphicsLibrary.Graphics.underlyingNamedType))
                return HandleGraphicsCall(method, arguments, abort, out result);
        }

        if (method.containingType.Equals(StandardLibrary.Console.underlyingNamedType) ||
            method.containingType.Equals(StandardLibrary.Math.underlyingNamedType) ||
            method.containingType.Equals(StandardLibrary.LowLevel.underlyingNamedType) ||
            method.containingType.Equals(StandardLibrary.Time.underlyingNamedType) ||
            method.containingType.Equals(StandardLibrary.Directory.underlyingNamedType) ||
            method.containingType.Equals(StandardLibrary.File.underlyingNamedType) ||
            method.containingType.Equals(StandardLibrary.Random.underlyingNamedType)) {
            var mapKey = LibraryHelpers.BuildMapKey(method);

            if (mapKey == "LowLevel_GetHashCode_O") {
                result = Dereference(EvaluateExpression(arguments[0], abort)).GetHashCode();
                return true;
            } else if (mapKey == "LowLevel_GetTypeName_O") {
                result = EvaluateExpression(arguments[0], abort).type.name;
                return true;
            }

            if (mapKey == "Random_RandInt_I?") {
                _lazyRandom ??= new Random();
                var max = Value(EvaluateExpression(arguments[0], abort));
                result = Convert.ToInt64(_lazyRandom.Next(Convert.ToInt32(max)));
                return true;
            }

            var function = StandardLibrary.EvaluatorMap[mapKey];

            if (mapKey == "Console_Print_S?" || mapKey == "Console_Print_A?" || mapKey == "Console_Print_O?")
                printed = true;

            if (mapKey == "Console_Print_O?" || mapKey == "Console_PrintLine_O?") {
                // Special case where we inject a call to Object.ToString before calling the Console method
                var toStringResult = InvokeMethod(toStringMethod, [], arguments[0], abort);
                result = function(Value(toStringResult), null, null);
                return true;
            }

            var valueArguments = arguments.Select(a => Value(EvaluateExpression(a, abort))).ToArray();

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

    private bool HandleGraphicsCall(
        MethodSymbol method,
        ImmutableArray<BoundExpression> arguments,
        ValueWrapper<bool> abort,
        out object result) {
        result = null;
        var mapKey = LibraryHelpers.BuildMapKey(method);

        if (mapKey == "Graphics_Initialize_SII") {
            var valueArguments = arguments.Select(a => Value(EvaluateExpression(a, abort))).ToArray();

            StartGraphics(
                (string)valueArguments[0],
                Convert.ToInt32(valueArguments[1]),
                Convert.ToInt32(valueArguments[2]),
                abort
            );
        }

        if (mapKey == "Graphics_LoadSprite_SVVI" ||
            mapKey == "Graphics_DrawSprite_S?" ||
            mapKey == "Graphics_DrawText_T?" ||
            mapKey == "Graphics_LoadText_SSVDDIII" ||
            mapKey == "Graphics_GetKey_S" ||
            mapKey == "Graphics_DrawRect_R?" ||
            mapKey == "Graphics_StopDraw_I?") {
            // Wait until window is created to future Graphics calls are valid
            if (_context.graphicsThread is not null && _context.graphicsThread.IsAlive) {
                while (_context.graphicsHandler?.GraphicsDevice is null)
                    ;
            } else {
                return true;
            }
        } else {
            return true;
        }

        if (mapKey == "Graphics_LoadSprite_SVVI") {
            var evaluatedArguments = arguments.Select(a => EvaluateExpression(a, abort)).ToArray();
            var path = (string)Value(evaluatedArguments[0]);

            if (!File.Exists(path))
                return true;

            var spriteType = CorLibrary.GetSpecialType(SpecialType.Sprite);
            var sprite = CreateObject(spriteType);
            var spriteFields = spriteType.GetMembers().Where(f => f is FieldSymbol).ToArray();
            sprite.members[spriteFields[0]] = evaluatedArguments[1];
            sprite.members[spriteFields[1]] = evaluatedArguments[2];
            sprite.members[spriteFields[2]] = evaluatedArguments[3];
            sprite.members[HiddenSpriteData] = new EvaluatorObject(_context.graphicsHandler?.LoadSprite(path), null);

            result = sprite;
        } else if (mapKey == "Graphics_DrawSprite_S?") {
            var argument = Dereference(EvaluateExpression(arguments[0], abort));

            if (argument.members.Count < 4)
                return true;

            var spriteType = CorLibrary.GetSpecialType(SpecialType.Sprite);
            var spriteFields = spriteType.GetMembers().Where(f => f is FieldSymbol).ToArray();
            var posX = Convert.ToSingle(argument.members[spriteFields[0]].members.Values.ElementAt(0).value);
            var posY = Convert.ToSingle(argument.members[spriteFields[0]].members.Values.ElementAt(1).value);
            var scaleX = Convert.ToSingle(argument.members[spriteFields[1]].members.Values.ElementAt(0).value);
            var scaleY = Convert.ToSingle(argument.members[spriteFields[1]].members.Values.ElementAt(1).value);
            var rotation = Convert.ToInt32(argument.members[spriteFields[2]].value);
            var texture = (Texture2D)argument.members[HiddenSpriteData].value;

            if (texture is null)
                return true;

            if (_context.options.isScript) {
                result = _context.graphicsHandler?.AddAction(
                    () => { _context.graphicsHandler?.DrawSprite(texture, posX, posY, scaleX, scaleY, rotation); }
                );
            } else {
                _context.graphicsHandler?.DrawSprite(texture, posX, posY, scaleX, scaleY, rotation);
            }
        } else if (mapKey == "Graphics_StopDraw_I?") {
            var argument = Value(EvaluateExpression(arguments[0], abort));

            if (argument is null)
                return true;

            _context.graphicsHandler?.RemoveAction(Convert.ToInt32(argument));
        } else if (mapKey == "Graphics_LoadText_SSVDDIII") {
            var evaluatedArguments = arguments.Select(a => EvaluateExpression(a, abort)).ToArray();
            var path = (string)Value(evaluatedArguments[1]);

            if (!File.Exists(path))
                return true;

            var textType = CorLibrary.GetSpecialType(SpecialType.Text);
            var text = CreateObject(textType);
            var textFields = textType.GetMembers().Where(f => f is FieldSymbol).ToArray();

            var fontSize = Convert.ToSingle(Value(evaluatedArguments[3]));

            text.members[textFields[0]] = evaluatedArguments[0];
            text.members[textFields[1]] = evaluatedArguments[1];
            text.members[textFields[2]] = evaluatedArguments[2];
            text.members[textFields[3]] = evaluatedArguments[3];
            text.members[textFields[4]] = evaluatedArguments[4];
            text.members[textFields[5]] = evaluatedArguments[5];
            text.members[textFields[6]] = evaluatedArguments[6];
            text.members[textFields[7]] = evaluatedArguments[7];

            text.members[HiddenTextData] = new EvaluatorObject(
                _context.graphicsHandler?.LoadText(path, fontSize),
                null
            );

            result = text;
        } else if (mapKey == "Graphics_DrawText_T?") {
            var argument = Dereference(EvaluateExpression(arguments[0], abort));

            if (argument.members.Count < 9)
                return true;

            var textType = CorLibrary.GetSpecialType(SpecialType.Text);
            var textFields = textType.GetMembers().Where(f => f is FieldSymbol).ToArray();

            var text = (string)argument.members[textFields[0]].value;
            var posX = Convert.ToSingle(argument.members[textFields[2]].members.Values.ElementAt(0).value);
            var posY = Convert.ToSingle(argument.members[textFields[2]].members.Values.ElementAt(1).value);
            var r = Convert.ToInt32(argument.members[textFields[5]].value);
            var g = Convert.ToInt32(argument.members[textFields[6]].value);
            var b = Convert.ToInt32(argument.members[textFields[7]].value);
            var spriteFont = (DynamicSpriteFont)argument.members[HiddenTextData].value;

            if (spriteFont is null)
                return true;

            if (_context.options.isScript) {
                result = _context.graphicsHandler?.AddAction(
                    () => { _context.graphicsHandler?.DrawText(spriteFont, text, posX, posY, r, g, b); }
                );
            } else {
                _context.graphicsHandler?.DrawText(spriteFont, text, posX, posY, r, g, b);
            }
        } else if (mapKey == "Graphics_GetKey_S") {
            var argument = (string)Value(EvaluateExpression(arguments[0], abort));
            result = _context.graphicsHandler?.GetKey(argument);
        } else if (mapKey == "Graphics_DrawRect_R?") {
            var argument = Dereference(EvaluateExpression(arguments[0], abort));

            if (argument.members.Count < 7)
                return true;

            var rectType = CorLibrary.GetSpecialType(SpecialType.Rect);
            var rectFields = rectType.GetMembers().Where(f => f is FieldSymbol).ToArray();
            var x = Convert.ToInt32(argument.members[rectFields[0]].value);
            var y = Convert.ToInt32(argument.members[rectFields[1]].value);
            var w = Convert.ToInt32(argument.members[rectFields[2]].value);
            var h = Convert.ToInt32(argument.members[rectFields[3]].value);
            var r = Convert.ToInt32(argument.members[rectFields[4]].value);
            var g = Convert.ToInt32(argument.members[rectFields[5]].value);
            var b = Convert.ToInt32(argument.members[rectFields[6]].value);

            if (_context.options.isScript) {
                result = _context.graphicsHandler?.AddAction(
                    () => { _context.graphicsHandler?.DrawRect(x, y, w, h, r, g, b); }
                );
            } else {
                _context.graphicsHandler?.DrawRect(x, y, w, h, r, g, b);
            }
        }

        return true;
    }

    private void StartGraphics(string title, int width, int height, ValueWrapper<bool> abort) {
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
                    using var graphicsHandler = new GraphicsHandler(abort);
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

#if _WINDOWS
        _context.graphicsThread.SetApartmentState(ApartmentState.STA);
#endif

        _context.graphicsThread.Start();
    }

    private void UpdateCaller(double deltaTime, ValueWrapper<bool> abort) {
        if (_program.updatePoint is null)
            return;

        // It's easier to just convert deltaTime into a bound node to take advantage of InvokeMethod
        // TODO Consider refactoring to not require backtracking to a bound node only to be evaluated immediately
        var argument = new BoundLiteralExpression(
            null,
            new ConstantValue(deltaTime),
            CorLibrary.GetSpecialType(SpecialType.Decimal)
        );

        InvokeMethod(_program.updatePoint, [argument], abort);
    }
}
