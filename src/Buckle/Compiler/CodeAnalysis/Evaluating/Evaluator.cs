using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Buckle.Libraries.Standard;
using Buckle.Utilities;
using Shared;
using static Buckle.Utilities.MethodUtilities;

namespace Buckle.CodeAnalysis.Evaluating;

/// <summary>
/// Evaluates BoundStatements inline similar to an interpreter.
/// </summary>
internal sealed class Evaluator {
    private readonly BoundProgram _program;
    private readonly string[] _arguments;
    private readonly Dictionary<MethodSymbol, BoundBlockStatement> _methods =
        new Dictionary<MethodSymbol, BoundBlockStatement>();
    private readonly Dictionary<IVariableSymbol, IEvaluatorObject> _globals;
    private readonly Stack<Dictionary<IVariableSymbol, IEvaluatorObject>> _locals =
        new Stack<Dictionary<IVariableSymbol, IEvaluatorObject>>();
    private readonly Dictionary<IVariableSymbol, IEvaluatorObject> _classLocalBuffer =
        new Dictionary<IVariableSymbol, IEvaluatorObject>();
    private readonly Stack<EvaluatorObject> _enclosingTypes = new Stack<EvaluatorObject>();

    private EvaluatorObject _lastValue;
    private Random _random;
    // private bool _classLocalBufferOnStack;
    private bool _hasValue;
    private int _templateConstantDepth;

    /// <summary>
    /// Creates an <see cref="Evaluator" /> that can evaluate a <see cref="BoundProgram" /> (provided globals).
    /// </summary>
    /// <param name="program"><see cref="BoundProgram" />.</param>
    /// <param name="globals">Globals.</param>
    /// <param name="arguments">Runtime arguments.</param>
    internal Evaluator(
        BoundProgram program, Dictionary<IVariableSymbol, IEvaluatorObject> globals, string[] arguments) {
        diagnostics = new BelteDiagnosticQueue();
        exceptions = new List<Exception>();
        _arguments = arguments;
        _program = program;
        _globals = globals;
        _locals.Push(new Dictionary<IVariableSymbol, IEvaluatorObject>());
        _templateConstantDepth = 0;

        var current = program;

        while (current != null) {
            foreach (var (method, body) in current.methodBodies)
                _methods.Add(method, body);

            current = current.previous;
        }
    }

    /// <summary>
    /// If the last output to the terminal was a `Print`, and not a `PrintLine`, meaning the caller might want to write
    /// an extra line to prevent formatting problems.
    /// </summary>
    internal bool lastOutputWasPrint { get; private set; }

    /// <summary>
    /// All thrown exceptions during evaluation.
    /// </summary>
    internal List<Exception> exceptions { get; set; }

    /// <summary>
    /// Diagnostics specific to the <see cref="Evaluator" />.
    /// </summary>
    internal BelteDiagnosticQueue diagnostics { get; set; }

    /// <summary>
    /// Evaluate the provided <see cref="BoundProgram" />.
    /// </summary>
    /// <param name="abort">External flag used to cancel evaluation.</param>
    /// <param name="hasValue">If the evaluation had a returned result.</param>
    /// <returns>Result of <see cref="BoundProgram" /> (if applicable).</returns>
    internal object Evaluate(ValueWrapper<bool> abort, out bool hasValue) {
        if (_program.entryPoint is null) {
            hasValue = false;
            return null;
        }

        var body = LookupMethod(_methods, _program.entryPoint);

        if (_program.entryPoint.parameters.Length == 1) {
            var args = ImmutableArray.CreateBuilder<BoundConstant>();

            foreach (var arg in _arguments)
                args.Add(new BoundConstant(arg));

            var list = EvaluateObjectCreationExpression(new BoundObjectCreationExpression(
                _program.entryPoint.parameters[0].type,
                (_program.entryPoint.parameters[0].type.typeSymbol as NamedTypeSymbol).constructors[2],
                [new BoundInitializerListExpression(
                    new BoundConstant(args.ToImmutable()),
                    new BoundType(
                        TypeSymbol.String,
                        dimensions: 1,
                        sizes: [new BoundLiteralExpression(_arguments.Length)]
                    )
                )]
            ), abort);

            _locals.Push(new Dictionary<IVariableSymbol, IEvaluatorObject>() {
                [_program.entryPoint.parameters[0]] = list
            });
        }

        var result = EvaluateStatement(body, abort, out _);
        hasValue = _hasValue;

        return Value(result, true);
    }

    private object GetVariableValue(VariableSymbol variable, bool traceCollections = false) {
        var value = Get(variable);

        try {
            return Value(value, traceCollections);
        } catch (BelteInternalException) {
            throw new BelteEvaluatorException(
                $"Reference cannot be deferred (what it was referencing was likely redefined)"
            );
        }
    }

    private bool TryGet(
        Dictionary<IVariableSymbol, IEvaluatorObject> variables,
        VariableSymbol variable,
        out EvaluatorObject evaluatorObject) {
        if (variables.Count > 0 && variables.TryGetValue(variable, out var result)) {
            evaluatorObject = result as EvaluatorObject;
            return true;
        }

        evaluatorObject = null;
        return false;
    }

    private EvaluatorObject Get(VariableSymbol variable, Dictionary<IVariableSymbol, IEvaluatorObject> scope = null) {
        if (scope != null) {
            if (TryGet(scope, variable, out var evaluatorObject))
                return evaluatorObject;
        }

        if (variable.kind == SymbolKind.GlobalVariable) {
            if (TryGet(_globals, variable, out var evaluatorObject))
                return evaluatorObject;
        } else {
            foreach (var frame in _locals) {
                if (TryGet(frame, variable, out var evaluatorObject))
                    return evaluatorObject;
            }
        }

        throw new BelteInternalException($"Get: '{variable.name}' was not found in any accessible scopes");
    }

    private Dictionary<object, object> DictionaryValue(Dictionary<Symbol, EvaluatorObject> value) {
        var dictionary = new Dictionary<object, object>();

        foreach (var pair in value) {
            if (pair.Key is FieldSymbol)
                dictionary.Add(pair.Key.name, Value(pair.Value, true));
        }

        return dictionary;
    }

    private object[] CollectionValue(EvaluatorObject[] value) {
        var builder = new List<object>();

        foreach (var item in value)
            builder.Add(Value(item, true));

        return builder.ToArray();
    }

    private object Value(IEvaluatorObject value, bool traceCollections = false) {
        if (value.isReference)
            return GetVariableValue(value.reference, traceCollections);
        else if (value.value is EvaluatorObject)
            return Value(value.value as EvaluatorObject, traceCollections);
        else if (value.value is EvaluatorObject[] && traceCollections)
            return CollectionValue(value.value as EvaluatorObject[]);
        else if (traceCollections && value.value is null && value.members != null)
            return DictionaryValue(value.members);
        else
            return value.value;
    }

    private EvaluatorObject Copy(IEvaluatorObject value) {
        if (value.reference != null && value.isExplicitReference == false)
            return Copy(Get(value.reference));
        else if (value.reference != null)
            return new EvaluatorObject(value.reference, isExplicitReference: true);
        else if (value.members != null)
            return new EvaluatorObject(Copy(value.members));
        else
            return new EvaluatorObject(value.value);
    }

    private Dictionary<Symbol, EvaluatorObject> Copy(Dictionary<Symbol, EvaluatorObject> members) {
        var newMembers = new Dictionary<Symbol, EvaluatorObject>();

        foreach (var member in members)
            newMembers.Add(member.Key, Copy(member.Value));

        return newMembers;
    }

    private void Create(VariableSymbol left, EvaluatorObject right) {
        if (left.kind == SymbolKind.GlobalVariable) {
            var set = false;

            foreach (var global in _globals) {
                if (global.Key.name == left.name) {
                    _globals.Remove(global.Key);
                    _globals[left] = Copy(right);
                    set = true;

                    break;
                }
            }

            if (!set)
                _globals[left] = Copy(right);
        } else {
            var locals = _locals.Peek();
            var set = false;

            foreach (var local in locals) {
                if (local.Key.name == left.name) {
                    locals.Remove(local.Key);
                    locals[left] = Copy(right);
                    set = true;

                    break;
                }
            }

            if (!set)
                locals[left] = Copy(right);
        }
    }

    private void Assign(EvaluatorObject left, EvaluatorObject right) {
        while (right.isReference && !right.isExplicitReference)
            right = Get(right.reference);

        while (left.isReference && !left.isExplicitReference)
            left = Get(left.reference);

        if (right.isExplicitReference) {
            left.reference = right.reference;

            return;
        } else if (left.isExplicitReference) {
            while (left.isReference)
                left = Get(left.reference);
        }

        if (right.members is null)
            left.members = null;

        if (right.value is null && right.members != null)
            left.members = Copy(right.members);
        else
            left.value = Value(right);
    }

    private void EnterClassScope(EvaluatorObject @class, bool updateLocals = true) {
        foreach (var member in @class.members) {
            if (member.Key is FieldSymbol or ParameterSymbol) {
                var variable = member.Key as VariableSymbol;
                // If the symbol is already present it could be outdated and should be replaced
                // If it isn't outdated no harm in replacing it
                _classLocalBuffer.Remove(variable);
                _classLocalBuffer.Add(variable, member.Value);
            }
        }

        _enclosingTypes.Push(@class);

        if (updateLocals) {
            _locals.Push(_classLocalBuffer);
            // _classLocalBufferOnStack = true;
        }
    }

    private void ExitClassScope() {
        _enclosingTypes.Pop();
    }

    private EvaluatorObject EvaluateCast(EvaluatorObject value, BoundType type) {
        var valueValue = Value(value);

        if (value.members != null)
            return value;

        if (valueValue is EvaluatorObject[] v) {
            var builder = new List<EvaluatorObject>();
            var castedValue = v;

            foreach (var item in castedValue)
                builder.Add(EvaluateCast(item, type.ChildType()));

            valueValue = builder.ToArray();
        } else {
            valueValue = EvaluateValueCast(valueValue, type);
        }

        return new EvaluatorObject(valueValue);
    }

    private object EvaluateValueCast(object value, BoundType type) {
        return CastUtilities.Cast(value, type);
    }

    private EvaluatorObject EvaluateStatement(
        BoundBlockStatement statement, ValueWrapper<bool> abort, out bool hasReturn, bool insideTry = false) {
        _hasValue = false;
        hasReturn = false;

        try {
            var labelToIndex = new Dictionary<BoundLabel, int>();

            for (var i = 0; i < statement.statements.Length; i++) {
                if (statement.statements[i] is BoundLabelStatement l)
                    labelToIndex.Add(l.label, i + 1);
            }

            var index = 0;

            while (index < statement.statements.Length) {
                if (abort)
                    throw new BelteThreadException();

                var s = statement.statements[index];

                switch (s.kind) {
                    case BoundNodeKind.BlockStatement:
                        // TODO This is a temporary fix to nested blocks existing; SHOULD never occur after lowering
                        EvaluateStatement((BoundBlockStatement)s, abort, out hasReturn, insideTry: true);
                        index++;
                        break;
                    case BoundNodeKind.NopStatement:
                        index++;
                        break;
                    case BoundNodeKind.ExpressionStatement:
                        EvaluateExpressionStatement((BoundExpressionStatement)s, abort);
                        index++;
                        break;
                    case BoundNodeKind.LocalDeclarationStatement:
                        EvaluateLocalDeclarationStatement((BoundLocalDeclarationStatement)s, abort);
                        index++;
                        break;
                    case BoundNodeKind.GotoStatement:
                        var gs = (BoundGotoStatement)s;
                        index = labelToIndex[gs.label];
                        break;
                    case BoundNodeKind.ConditionalGotoStatement:
                        var cgs = (BoundConditionalGotoStatement)s;
                        var condition = (bool)Value(EvaluateExpression(cgs.condition, abort));

                        if (condition == cgs.jumpIfTrue)
                            index = labelToIndex[cgs.label];
                        else
                            index++;

                        break;
                    case BoundNodeKind.LabelStatement:
                        index++;
                        break;
                    case BoundNodeKind.TryStatement:
                        EvaluateTryStatement((BoundTryStatement)s, abort, out var returned);

                        if (returned) {
                            hasReturn = true;
                            return _lastValue;
                        }

                        index++;

                        break;
                    case BoundNodeKind.ReturnStatement:
                        var returnStatement = (BoundReturnStatement)s;
                        _lastValue = returnStatement.expression is null
                            ? new EvaluatorObject()
                            : Copy(EvaluateExpression(returnStatement.expression, abort));

                        _hasValue = returnStatement.expression is not null and not BoundEmptyExpression;
                        hasReturn = true;

                        return _lastValue;
                    default:
                        throw new BelteInternalException($"EvaluateStatement: unexpected statement '{s.kind}'");
                }
            }

            return _lastValue;
        } catch (Exception e) when (e is not BelteInternalException) {
            if (e is BelteThreadException || abort)
                return new EvaluatorObject();

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

            return new EvaluatorObject();
        }
    }

    private void EvaluateExpressionStatement(BoundExpressionStatement statement, ValueWrapper<bool> abort) {
        _lastValue = EvaluateExpression(statement.expression, abort);
    }

    private void EvaluateTryStatement(BoundTryStatement statement, ValueWrapper<bool> abort, out bool hasReturn) {
        hasReturn = false;

        try {
            EvaluateStatement(statement.body, abort, out hasReturn, true);
        } catch (Exception e) when (e is not BelteException) {
            if (statement.catchBody != null && !hasReturn)
                EvaluateStatement(statement.catchBody, abort, out hasReturn);
            else
                throw;
        } finally {
            if (statement.finallyBody != null && !hasReturn)
                EvaluateStatement(statement.finallyBody, abort, out hasReturn);
        }
    }

    private void EvaluateLocalDeclarationStatement(
        BoundLocalDeclarationStatement statement,
        ValueWrapper<bool> abort) {
        var value = EvaluateExpression(statement.declaration.initializer, abort);
        _lastValue = null;
        Create(statement.declaration.variable, value);
    }

    private EvaluatorObject EvaluateExpression(BoundExpression node, ValueWrapper<bool> abort) {
        if (node.constantValue != null)
            return EvaluateConstantExpression(node, abort);

        switch (node.kind) {
            case BoundNodeKind.LiteralExpression:
                if (node is BoundInitializerListExpression il)
                    return new EvaluatorObject(EvaluateInitializerListExpression(il, abort));
                else
                    goto default;
            case BoundNodeKind.VariableExpression:
                return EvaluateVariableExpression((BoundVariableExpression)node, abort);
            case BoundNodeKind.AssignmentExpression:
                return EvaluateAssignmentExpression((BoundAssignmentExpression)node, abort);
            case BoundNodeKind.UnaryExpression:
                return EvaluateUnaryExpression((BoundUnaryExpression)node, abort);
            case BoundNodeKind.BinaryExpression:
                return EvaluateBinaryExpression((BoundBinaryExpression)node, abort);
            case BoundNodeKind.TernaryExpression:
                return EvaluateTernaryExpression((BoundTernaryExpression)node, abort);
            case BoundNodeKind.CallExpression:
                return EvaluateCallExpression((BoundCallExpression)node, abort);
            case BoundNodeKind.CastExpression:
                return EvaluateCastExpression((BoundCastExpression)node, abort);
            case BoundNodeKind.IndexExpression:
                return EvaluateIndexExpression((BoundIndexExpression)node, abort);
            case BoundNodeKind.ReferenceExpression:
                return EvaluateReferenceExpression((BoundReferenceExpression)node, abort);
            case BoundNodeKind.TypeOfExpression:
                return EvaluateTypeOfExpression((BoundTypeOfExpression)node, abort);
            case BoundNodeKind.EmptyExpression:
                return new EvaluatorObject();
            case BoundNodeKind.ObjectCreationExpression:
                return EvaluateObjectCreationExpression((BoundObjectCreationExpression)node, abort);
            case BoundNodeKind.MemberAccessExpression:
                return EvaluateMemberAccessExpression((BoundMemberAccessExpression)node, abort);
            case BoundNodeKind.ThisExpression:
                return EvaluateThisExpression((BoundThisExpression)node, abort);
            case BoundNodeKind.Type:
                return EvaluateType((BoundType)node, abort);
            default:
                throw new BelteInternalException($"EvaluateExpression: unexpected node '{node.kind}'");
        }
    }

    private EvaluatorObject EvaluateType(BoundType node, ValueWrapper<bool> abort) {
        if (node.arity == 0)
            return EvaluateTypeCore(node, abort);

        var locals = new Dictionary<IVariableSymbol, IEvaluatorObject>();
        var typeSymbol = node.typeSymbol as NamedTypeSymbol;

        for (var i = 0; i < node.templateArguments.Length; i++) {
            EvaluatorObject value;

            if (node.templateArguments[i].isConstant)
                value = new EvaluatorObject(node.templateArguments[i].constant.value);
            else
                value = EvaluateType(node.templateArguments[i].type, abort);

            locals.Add(typeSymbol.templateParameters[i], value);
        }

        _locals.Push(locals);
        _templateConstantDepth++;

        return EvaluateTypeCore(node, abort);
    }

    private EvaluatorObject EvaluateTypeCore(BoundType node, ValueWrapper<bool> abort) {
        var members = new Dictionary<Symbol, EvaluatorObject>();

        if (node.typeSymbol is not NamedTypeSymbol)
            return new EvaluatorObject(members: members);

        var typeMembers = (node.type.typeSymbol as NamedTypeSymbol).members;
        var templateArgumentIndex = 0;

        foreach (var templateArgument in typeMembers.Where(t => t is ParameterSymbol)) {
            EvaluatorObject value;

            if (node.type.templateArguments[templateArgumentIndex].isConstant)
                value = new EvaluatorObject(node.type.templateArguments[templateArgumentIndex].constant.value);
            else
                value = EvaluateType(node.type.templateArguments[templateArgumentIndex].type, abort);

            templateArgumentIndex++;
            members.Add(templateArgument, value);
        }

        return new EvaluatorObject(members: members);
    }

    private EvaluatorObject EvaluateThisExpression(BoundThisExpression _, ValueWrapper<bool> _1) {
        return _enclosingTypes.Peek();
    }

    private EvaluatorObject EvaluateMemberAccessExpression(BoundMemberAccessExpression node, ValueWrapper<bool> abort) {
        var operand = EvaluateExpression(node.left, abort);

        if (operand.isReference) {
            do {
                operand = Get(operand.reference, operand.referenceScope);
            } while (operand.isReference == true);
        }

        if (node.type == BoundType.MethodGroup) {
            EnterClassScope(operand, true);
            return null;
        }

        if (node.right is BoundType)
            return operand.members[node.right.type.typeSymbol];

        var member = (node.right as BoundVariableExpression).variable;
        return operand.members[member];
    }

    private EvaluatorObject EvaluateObjectCreationExpression(
        BoundObjectCreationExpression node,
        ValueWrapper<bool> abort) {
        if (node.viaConstructor) {
            var core = EvaluateTypeCore(node.type, abort);
            var members = new Dictionary<Symbol, EvaluatorObject>();
            var typeMembers = (node.type.typeSymbol as NamedTypeSymbol).members;

            foreach (var member in core.members)
                members.Add(member.Key, member.Value);

            foreach (var member in typeMembers.Where(t => t is not ParameterSymbol)) {
                var value = new EvaluatorObject();

                if (member is FieldSymbol f) {
                    if (f.isReference) {
                        value.isReference = true;
                        value.isExplicitReference = true;
                    }
                }

                members.Add(member, value);
            }

            var newObject = new EvaluatorObject(members);

            EnterClassScope(newObject);

            // structs don't have any methods, so no constructors
            if (node.type.typeSymbol is ClassSymbol)
                InvokeMethod(node.constructor, node.arguments, abort);

            ExitClassScope();

            return newObject;
        } else {
            var array = new EvaluatorObject(value: null);
            var sizes = new List<int>();

            // TODO There is probably a more efficient algorithm for this
            foreach (var size in node.type.sizes) {
                var sizeValue = (int)Value(EvaluateExpression(size, abort));

                foreach (var element in IterateElements(array)) {
                    var members = new List<EvaluatorObject>();

                    for (var i = 0; i < sizeValue; i++)
                        members.Add(new EvaluatorObject(value: null));

                    element.value = members.ToArray();
                }
            }

            return array;
        }

        List<EvaluatorObject> IterateElements(EvaluatorObject evaluatorObject) {
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

    private EvaluatorObject EvaluateTypeOfExpression(BoundTypeOfExpression _, ValueWrapper<bool> _1) {
        // TODO Implement typeof and type types
        // ? Would just settings EvaluatorObject.value to a BoundType work?
        return new EvaluatorObject();
    }

    private EvaluatorObject EvaluateReferenceExpression(BoundReferenceExpression node, ValueWrapper<bool> abort) {
        if (node.expression is BoundVariableExpression v) {
            Dictionary<IVariableSymbol, IEvaluatorObject> referenceScope;

            if (v.variable.kind == SymbolKind.GlobalVariable)
                referenceScope = _globals;
            else
                referenceScope = _locals.Peek();

            return new EvaluatorObject(v.variable, isExplicitReference: true, referenceScope: referenceScope);
        } else {
            return EvaluateExpression(node.expression, abort);
        }
    }

    private EvaluatorObject EvaluateIndexExpression(BoundIndexExpression node, ValueWrapper<bool> abort) {
        var variable = EvaluateExpression(node.expression, abort);
        var index = EvaluateExpression(node.index, abort);
        var array = (EvaluatorObject[])Value(variable);
        var indexValue = (int)Value(index);

        return array[indexValue];
    }

    private EvaluatorObject[] EvaluateInitializerListExpression(
        BoundInitializerListExpression node,
        ValueWrapper<bool> abort) {
        var builder = new List<EvaluatorObject>();

        foreach (var item in node.items) {
            var value = EvaluateExpression(item, abort);
            builder.Add(value);
        }

        return builder.ToArray();
    }

    private EvaluatorObject EvaluateCastExpression(BoundCastExpression node, ValueWrapper<bool> abort) {
        var value = EvaluateExpression(node.expression, abort);

        return EvaluateCast(value, node.type);
    }

    private EvaluatorObject EvaluateCallExpression(BoundCallExpression node, ValueWrapper<bool> abort) {
        if (node.method == BuiltinMethods.RandInt) {
            var max = (int)Value(EvaluateExpression(node.arguments[0], abort));
            _random ??= new Random();
            return new EvaluatorObject(_random.Next(max));
        } else if (node.method == BuiltinMethods.ValueAny ||
            node.method == BuiltinMethods.ValueBool ||
            node.method == BuiltinMethods.ValueDecimal ||
            node.method == BuiltinMethods.ValueInt ||
            node.method == BuiltinMethods.ValueString) {
            var value = EvaluateExpression(node.arguments[0], abort);
            var hasNoMembers = value.isReference ? Get(value.reference).members is null : value.members is null;

            if (Value(value) is null && hasNoMembers)
                throw new NullReferenceException();

            if (hasNoMembers)
                return new EvaluatorObject(Value(value));
            else
                return Copy(value);
        } else if (node.method == BuiltinMethods.HasValueAny ||
            node.method == BuiltinMethods.HasValueBool ||
            node.method == BuiltinMethods.HasValueDecimal ||
            node.method == BuiltinMethods.HasValueInt ||
            node.method == BuiltinMethods.HasValueString) {
            var value = EvaluateExpression(node.arguments[0], abort);
            var hasNoMembers = value.isReference ? Get(value.reference).members is null : value.members is null;

            if (Value(value) is null && hasNoMembers)
                return new EvaluatorObject(false);

            return new EvaluatorObject(true);
        } else if (node.method == BuiltinMethods.Hex) {
            var value = (int)Value(EvaluateExpression(node.arguments[0], abort));
            var addPrefix = (bool)Value(EvaluateExpression(node.arguments[1], abort));
            var hex = addPrefix ? $"0x{value.ToString("X")}" : value.ToString("X");

            return new EvaluatorObject(hex);
        } else if (node.method == BuiltinMethods.Ascii) {
            var value = (string)Value(EvaluateExpression(node.arguments[0], abort));

            if (value.Length != 1)
                throw new ArgumentException("String passed into `Ascii` method must be of length 1");

            return new EvaluatorObject((int)char.Parse(value));
        } else if (node.method == BuiltinMethods.Char) {
            var value = (int)Value(EvaluateExpression(node.arguments[0], abort));
            return new EvaluatorObject(((char)value).ToString());
        } else if (node.method == BuiltinMethods.Length) {
            var value = Value(EvaluateExpression(node.arguments[0], abort));

            if (value is object[] v)
                return new EvaluatorObject(v.Length);
            else
                return new EvaluatorObject(value: null);
        } else {
            if (CheckStandardMap(node.method, node.arguments, abort, out var result, out var printed)) {
                lastOutputWasPrint = printed;
                return new EvaluatorObject(result);
            }

            return InvokeMethod(node.method, node.arguments, abort, node.expression);
        }
    }

    private EvaluatorObject InvokeMethod(
        MethodSymbol method,
        ImmutableArray<BoundExpression> arguments,
        ValueWrapper<bool> abort,
        BoundExpression expression = null) {
        var locals = new Dictionary<IVariableSymbol, IEvaluatorObject>();

        for (var i = 0; i < arguments.Length; i++) {
            var parameter = method.parameters[i];
            var value = EvaluateExpression(arguments[i], abort);

            while (!parameter.type.isReference && value.isReference)
                value = Get(value.reference);

            locals.Add(parameter, Copy(value));
        }

        _locals.Push(locals);

        var statement = LookupMethod(_methods, method);
        var templateConstantDepth = _templateConstantDepth;
        var enteredScope = false;

        if (expression != null) {
            var possibleScope = EvaluateExpression(expression, abort);

            // On an expression such as 'myInstance.Method()', we need to enter the 'myInstance' class scope
            // in case 'Method' uses 'this'
            // If what we get here is not a reference, it is a static accession and the needed scoped members have
            // already been pushed by 'EvaluateType'.
            if (possibleScope != null && possibleScope.isReference) {
                while (possibleScope.isReference && !possibleScope.isExplicitReference)
                    possibleScope = Get(possibleScope.reference);

                if (possibleScope.members.Count > 0) {
                    EnterClassScope(possibleScope);
                    enteredScope = true;
                }
            }
        }

        var result = EvaluateStatement(statement, abort, out _);

        while (_templateConstantDepth > templateConstantDepth) {
            _templateConstantDepth--;
            _locals.Pop();
        }

        _locals.Pop();

        if (enteredScope)
            ExitClassScope();

        // TODO Make sure removing this doesn't lead to an accumulation of unnecessary locals on the stack
        // if (_classLocalBufferOnStack)
        //     _locals.Pop();

        return result;
    }

    private EvaluatorObject EvaluateConstantExpression(BoundExpression expression, ValueWrapper<bool> _) {
        return EvaluateCast(EvaluateBoundConstant(expression.constantValue), expression.type);
    }

    private EvaluatorObject EvaluateBoundConstant(BoundConstant constant) {
        if (constant.value is ImmutableArray<BoundConstant> ia) {
            var builder = new List<EvaluatorObject>();

            foreach (var item in ia)
                builder.Add(EvaluateBoundConstant(item));

            return new EvaluatorObject(builder.ToArray());
        } else {
            return new EvaluatorObject(constant.value);
        }
    }

    private EvaluatorObject EvaluateVariableExpression(BoundVariableExpression expression, ValueWrapper<bool> _) {
        return new EvaluatorObject(expression.variable);
    }

    private EvaluatorObject EvaluateAssignmentExpression(
        BoundAssignmentExpression expression, ValueWrapper<bool> abort) {
        var left = EvaluateExpression(expression.left, abort);
        var right = EvaluateExpression(expression.right, abort);
        Assign(left, right);

        return right;
    }

    private EvaluatorObject EvaluateUnaryExpression(BoundUnaryExpression expression, ValueWrapper<bool> abort) {
        var operand = EvaluateExpression(expression.operand, abort);
        var operandValue = Value(operand);

        if (operandValue is null)
            return new EvaluatorObject();

        operandValue = EvaluateValueCast(operandValue, expression.op.operandType);

        switch (expression.op.opKind) {
            case BoundUnaryOperatorKind.NumericalIdentity:
                if (expression.operand.type.typeSymbol == TypeSymbol.Int)
                    return new EvaluatorObject((int)operandValue);
                else
                    return new EvaluatorObject((double)operandValue);
            case BoundUnaryOperatorKind.NumericalNegation:
                if (expression.operand.type.typeSymbol == TypeSymbol.Int)
                    return new EvaluatorObject(-(int)operandValue);
                else
                    return new EvaluatorObject(-(double)operandValue);
            case BoundUnaryOperatorKind.BooleanNegation:
                return new EvaluatorObject(!(bool)operandValue);
            case BoundUnaryOperatorKind.BitwiseCompliment:
                return new EvaluatorObject(~(int)operandValue);
            default:
                throw new BelteInternalException($"EvaluateUnaryExpression: unknown unary operator '{expression.op}'");
        }
    }

    private EvaluatorObject EvaluateTernaryExpression(BoundTernaryExpression expression, ValueWrapper<bool> abort) {
        var left = EvaluateExpression(expression.left, abort);
        var leftValue = Value(left);
        leftValue = EvaluateValueCast(leftValue, expression.op.leftType);

        switch (expression.op.opKind) {
            case BoundTernaryOperatorKind.Conditional:
                // This is so unused sides do not get evaluated (incase they would throw)
                if ((bool)leftValue)
                    return EvaluateExpression(expression.center, abort);
                else
                    return EvaluateExpression(expression.right, abort);
            default:
                throw new BelteInternalException(
                    $"EvaluateTernaryExpression: unknown ternary operator '{expression.op}'"
                );
        }
    }

    private EvaluatorObject EvaluateBinaryExpression(BoundBinaryExpression expression, ValueWrapper<bool> abort) {
        var left = EvaluateExpression(expression.left, abort);
        var leftValue = Value(left);

        // Only evaluates right side if necessary
        if (expression.op.opKind == BoundBinaryOperatorKind.ConditionalAnd) {
            if (leftValue is null || !(bool)leftValue)
                return new EvaluatorObject(false);

            var shortCircuitRight = EvaluateExpression(expression.right, abort);
            var shortCircuitRightValue = Value(shortCircuitRight);

            if (shortCircuitRightValue is null || !(bool)shortCircuitRightValue)
                return new EvaluatorObject(false);

            return new EvaluatorObject(true);
        }

        if (expression.op.opKind == BoundBinaryOperatorKind.ConditionalOr) {
            if (leftValue != null && (bool)leftValue)
                return new EvaluatorObject(true);

            var shortCircuitRight = EvaluateExpression(expression.right, abort);
            var shortCircuitRightValue = Value(shortCircuitRight);

            if (shortCircuitRightValue != null && (bool)shortCircuitRightValue)
                return new EvaluatorObject(true);

            return new EvaluatorObject(false);
        }

        var right = EvaluateExpression(expression.right, abort);
        var rightValue = Value(right);

        if (leftValue is null || rightValue is null)
            return new EvaluatorObject();

        var expressionType = expression.type.typeSymbol;
        var leftType = expression.left.type.typeSymbol;

        leftValue = EvaluateValueCast(leftValue, expression.left.type);
        rightValue = EvaluateValueCast(rightValue, expression.right.type);

        switch (expression.op.opKind) {
            case BoundBinaryOperatorKind.Addition:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue + (int)rightValue);
                else if (expressionType == TypeSymbol.String)
                    return new EvaluatorObject((string)leftValue + (string)rightValue);
                else
                    return new EvaluatorObject(Convert.ToDouble(leftValue) + Convert.ToDouble(rightValue));
            case BoundBinaryOperatorKind.Subtraction:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue - (int)rightValue);
                else
                    return new EvaluatorObject(Convert.ToDouble(leftValue) - Convert.ToDouble(rightValue));
            case BoundBinaryOperatorKind.Multiplication:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue * (int)rightValue);
                else
                    return new EvaluatorObject(Convert.ToDouble(leftValue) * Convert.ToDouble(rightValue));
            case BoundBinaryOperatorKind.Division:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue / (int)rightValue);
                else
                    return new EvaluatorObject(Convert.ToDouble(leftValue) / Convert.ToDouble(rightValue));
            case BoundBinaryOperatorKind.Power:
                if (expressionType == TypeSymbol.Int) {
                    return new EvaluatorObject((int)Math.Pow((int)leftValue, (int)rightValue));
                } else {
                    return new EvaluatorObject(
                        Convert.ToDouble(Math.Pow(Convert.ToDouble(leftValue), Convert.ToDouble(rightValue)))
                    );
                }
            case BoundBinaryOperatorKind.ConditionalAnd:
                return new EvaluatorObject((bool)leftValue && (bool)rightValue);
            case BoundBinaryOperatorKind.ConditionalOr:
                return new EvaluatorObject((bool)leftValue || (bool)rightValue);
            case BoundBinaryOperatorKind.EqualityEquals:
                return new EvaluatorObject(Equals(leftValue, rightValue));
            case BoundBinaryOperatorKind.EqualityNotEquals:
                return new EvaluatorObject(!Equals(leftValue, rightValue));
            case BoundBinaryOperatorKind.LessThan:
                if (leftType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue < (int)rightValue);
                else
                    return new EvaluatorObject(Convert.ToDouble(leftValue) < Convert.ToDouble(rightValue));
            case BoundBinaryOperatorKind.GreaterThan:
                if (leftType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue > (int)rightValue);
                else
                    return new EvaluatorObject(Convert.ToDouble(leftValue) > Convert.ToDouble(rightValue));
            case BoundBinaryOperatorKind.LessOrEqual:
                if (leftType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue <= (int)rightValue);
                else
                    return new EvaluatorObject(Convert.ToDouble(leftValue) <= Convert.ToDouble(rightValue));
            case BoundBinaryOperatorKind.GreatOrEqual:
                if (leftType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue >= (int)rightValue);
                else
                    return new EvaluatorObject(Convert.ToDouble(leftValue) >= Convert.ToDouble(rightValue));
            case BoundBinaryOperatorKind.LogicalAnd:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue & (int)rightValue);
                else
                    return new EvaluatorObject((bool)leftValue & (bool)rightValue);
            case BoundBinaryOperatorKind.LogicalOr:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue | (int)rightValue);
                else
                    return new EvaluatorObject((bool)leftValue | (bool)rightValue);
            case BoundBinaryOperatorKind.LogicalXor:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue ^ (int)rightValue);
                else
                    return new EvaluatorObject((bool)leftValue ^ (bool)rightValue);
            case BoundBinaryOperatorKind.LeftShift:
                return new EvaluatorObject((int)leftValue << (int)rightValue);
            case BoundBinaryOperatorKind.RightShift:
                return new EvaluatorObject((int)leftValue >> (int)rightValue);
            case BoundBinaryOperatorKind.UnsignedRightShift:
                return new EvaluatorObject((int)leftValue >>> (int)rightValue);
            case BoundBinaryOperatorKind.Modulo:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue % (int)rightValue);
                else
                    return new EvaluatorObject(Convert.ToDouble(leftValue) % Convert.ToDouble(rightValue));
            default:
                throw new BelteInternalException(
                    $"EvaluateBinaryExpression: unknown binary operator '{expression.op}'"
                );
        }
    }

    private bool CheckStandardMap(
        MethodSymbol method,
        ImmutableArray<BoundExpression> arguments,
        ValueWrapper<bool> abort,
        out object result,
        out bool printed) {
        result = null;
        printed = false;

        if (method.containingType == StandardLibrary.Console || method.containingType == StandardLibrary.Math) {
            if (method == StandardLibrary.Console.members[4] || method == StandardLibrary.Console.members[5])
                printed = true;

            result = StandardLibrary.EvaluateMethod(method, EvaluateArgumentsForExternalCall(arguments, abort));
            return true;
        }

        return false;
    }

    private object[] EvaluateArgumentsForExternalCall(
        ImmutableArray<BoundExpression> arguments,
        ValueWrapper<bool> abort) {
        var evaluatedArguments = new List<object>();

        foreach (var argument in arguments)
            evaluatedArguments.Add(Value(EvaluateExpression(argument, abort)));

        return evaluatedArguments.ToArray();
    }
}
