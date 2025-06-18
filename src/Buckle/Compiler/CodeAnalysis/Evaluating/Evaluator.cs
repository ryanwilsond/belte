using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Text;
using Shared;

namespace Buckle.CodeAnalysis.Evaluating;

/// <summary>
/// Evaluates BoundStatements inline similar to an interpreter.
/// </summary>
internal sealed class Evaluator {
    private static readonly Symbol HiddenTextData = new SynthesizedLabelSymbol("spriteFont");
    private static readonly Symbol HiddenTextureData = new SynthesizedLabelSymbol("texture2D");
    private static readonly Symbol HiddenSoundData = new SynthesizedLabelSymbol("soundInstance");

    private readonly BoundProgram _program;
    private readonly EvaluatorContext _context;
    private readonly Stack<Dictionary<Symbol, EvaluatorObject>> _locals;
    private readonly Stack<EvaluatorObject> _enclosingTypes;

    private EvaluatorObject _programObject;
    private EvaluatorObject _lastValue;
    private bool _hasValue;
    private MethodSymbol _lazyToString;
    private Random _lazyRandom;

    /// <summary>
    /// Creates an <see cref="Evaluator" /> that can evaluate a <see cref="BoundProgram" /> (provided globals).
    /// </summary>
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

        if (programType.isStatic) {
            _programObject = EvaluatorObject.GetInstance([], entryPoint.containingType);
        } else {
            _programObject = CreateObject(programType);
            var constructor = programType.constructors.Where(c => c.parameterCount == 0).FirstOrDefault();

            if (constructor is not null)
                InvokeMethodWithResolvedReceiver(constructor, [], _programObject, abort, []);
        }

        var result = InvokeMethodWithResolvedReceiver(entryPoint, [], _programObject, abort, []);

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
        if (value.reference is not null)
            return Value(value.reference, traceCollections);
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

    private void Create(DataContainerSymbol symbol, EvaluatorObject value) {
        if (symbol.isGlobal) {
            // _context.AddOrUpdateSymbol(symbol, CopyIfRef(value));
            _context.AddOrUpdateSymbol(symbol, Copy(value));
        } else {
            var locals = _locals.Peek();
            var set = false;

            foreach (var local in locals) {
                if (local.Key.name == symbol.name) {
                    // if (!locals[local.Key].isPersistent)
                    //     locals[local.Key].Free();

                    locals.Remove(local.Key);
                    // locals[symbol] = CopyIfRef(value);
                    locals[symbol] = Copy(value);
                    set = true;
                    break;
                }
            }

            if (!set)
                // locals[symbol] = CopyIfRef(value);
                locals[symbol] = Copy(value);
        }
    }

    private EvaluatorObject CopyIfRef(EvaluatorObject evaluatorObject) {
        if (!evaluatorObject.isReference)
            return evaluatorObject;

        return Copy(evaluatorObject);
    }

    private EvaluatorObject Copy(EvaluatorObject evaluatorObject) {
        if (evaluatorObject.reference is not null && !evaluatorObject.isExplicitReference) {
            return Copy(evaluatorObject.reference);
        } else if (evaluatorObject.reference is not null) {
            return EvaluatorObject.GetInstance(
                evaluatorObject.referenceSymbol,
                evaluatorObject.reference,
                evaluatorObject.type,
                isExplicitReference: true
            );
        } else if (evaluatorObject.members is not null) {
            return EvaluatorObject.GetInstance(Copy(evaluatorObject.members), evaluatorObject.type);
        } else {
            return EvaluatorObject.GetInstance(evaluatorObject.value, evaluatorObject.type);
        }
    }

    private Dictionary<Symbol, EvaluatorObject> Copy(Dictionary<Symbol, EvaluatorObject> members) {
        var newMembers = new Dictionary<Symbol, EvaluatorObject>();

        foreach (var member in members) {
            var copy = Copy(member.Value);
            // copy.isPersistent = true;
            newMembers.Add(member.Key, copy);
        }

        return newMembers;
    }

    private bool IsTakeable(BoundKind kind) {
        // TODO Consider expanding this to cover more nodes
        return kind is BoundKind.ObjectCreationExpression or BoundKind.ArrayCreationExpression;
    }

    private void Assign(EvaluatorObject left, EvaluatorObject right, BoundKind rightKind = BoundKind.NopStatement) {
        right = Dereference(right, false);
        left = Dereference(left, false);

        var isTakeable = IsTakeable(rightKind);

        if (right.isExplicitReference && isTakeable) {
            left.reference = right;
            left.isReference = true;
            return;
        }

        if (right.reference is not null && right.isExplicitReference) {
            left.reference = right.reference;
            left.referenceSymbol = right.referenceSymbol;
            left.isReference = true;
            return;
        } else if (left.isExplicitReference) {
            left = Dereference(left);
        }

        if (right.members is null)
            left.members = null;

        if (right.value is null && right.members != null) {
            if (isTakeable)
                left.members = right.members;
            else
                left.members = Copy(right.members);
        } else {
            left.value = Value(right);
        }

        left.type = right.type;
        // if (!right.isReference) {
        //     left = Dereference(left);

        //     left.members = right.members;
        //     left.type = right.type;
        //     left.value = right.value;

        //     return;
        // }

        // right = Dereference(right, false);
        // left = Dereference(left, false);

        // if (right.isExplicitReference) {
        //     left.reference = right.reference;
        //     left.referenceSymbol = right.referenceSymbol;
        //     left.isReference = true;
        //     return;
        // } else if (left.isExplicitReference) {
        //     left = Dereference(left);
        // }

        // left.type = right.type;

        // if (right.members is null) {
        //     left.members = null;
        // } else {
        //     left.members = Copy(right.members);
        //     return;
        // }

        // left.value = right.value;
    }

    private EvaluatorObject Dereference(EvaluatorObject reference, bool dereferenceOnExplicit = true) {
        while (reference.isReference) {
            if (!dereferenceOnExplicit && reference.isExplicitReference)
                break;

            // Ref local/field, but it references itself
            // This happens when a ref local/field is created in order to pass-by-ref, but it is assigned to by a
            // creation expression
            if (reference.reference is null)
                return reference;

            reference = reference.reference;
        }

        return reference;
    }

    private EvaluatorObject CreateObject(NamedTypeSymbol type) {
        var members = new Dictionary<Symbol, EvaluatorObject>();
        var current = type;

        do {
            var typeMembers = current.GetMembers();

            foreach (var member in typeMembers) {
                if (member is FieldSymbol f) {
                    var value = EvaluatorObject.GetInstance(value: null, f.type);
                    // value.isPersistent = true;

                    if (f.refKind != RefKind.None) {
                        value.isReference = true;
                        value.isExplicitReference = true;
                    }

                    members.Add(f, value);
                }
            }

            current = current.baseType;
        } while (current is not null);

        return EvaluatorObject.GetInstance(members, type);
    }

    private void EnterClassScope(EvaluatorObject classObject) {
        _enclosingTypes.Push(classObject);
        _locals.Push(classObject.members);
    }

    private void ExitClassScope() {
        _enclosingTypes.Pop();
        _locals.Pop();
    }

    private bool ObjectIsNull(EvaluatorObject evaluatorObject, TypeSymbol type) {
        var leftValue = Value(evaluatorObject);
        var dereferenced = Dereference(evaluatorObject);

        if (dereferenced.members is null && leftValue is null &&
            (type.specialType != SpecialType.Type || evaluatorObject.type is null)) {
            return true;
        } else {
            return false;
        }
    }

    private EvaluatorObject NullAssertObject(BoundExpression expression, ValueWrapper<bool> abort) {
        var value = EvaluateExpression(expression, abort);
        var dereferenced = Dereference(value);

        if (dereferenced.members is null &&
            Value(dereferenced) is null &&
            expression.type.specialType != SpecialType.Type) {
            throw new BelteNullReferenceException(expression.syntax.location);
        }

        return value;
    }

    private bool ConditionalValue(TextLocation location, EvaluatorObject evaluatorObject) {
        var value = Value(evaluatorObject) ?? throw new BelteNullReferenceException(location);
        return (bool)value;
    }

    #endregion

    #region Statements

    private EvaluatorObject EvaluateStatement(
        MethodSymbol method,
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
                        var conditionValue = ConditionalValue(cgs.condition.syntax.location, condition);

                        if (conditionValue == cgs.jumpIfTrue)
                            index = labelToIndex[cgs.label];
                        else
                            index++;

                        break;
                    case BoundKind.ReturnStatement:
                        var returnStatement = (BoundReturnStatement)s;

                        if (returnStatement.expression is not null) {
                            _lastValue = EvaluateExpression(returnStatement.expression, abort);
                            _hasValue = true;

                            // while (_lastValue.isReference && !_lastValue.isPersistent) {
                            //     var previous = _lastValue;
                            //     _lastValue = _lastValue.reference;
                            //     previous.Free();
                            // }

                            if (method.refKind == RefKind.None)
                                // _lastValue = CopyIfRef(_lastValue);
                                _lastValue = Copy(Dereference(_lastValue));
                        } else if (_lastValue is not null && _context.options.isScript) {
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
                return EvaluatorObject.GetInstance();

            if (insideTry)
                throw;

            exceptions.Add(e);
            lastOutputWasPrint = false;
            _hasValue = false;

            if (!_context.options.isScript)
                abort.Value = true;

            return EvaluatorObject.GetInstance();
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
            BoundKind.AsOperator => EvaluateAsOperator((BoundAsOperator)expression, abort),
            BoundKind.IsOperator => EvaluateIsOperator((BoundIsOperator)expression, abort),
            BoundKind.ConditionalOperator => EvaluateConditionalOperator((BoundConditionalOperator)expression, abort),
            BoundKind.NullAssertOperator => EvaluateNullAssertOperator((BoundNullAssertOperator)expression, abort),
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
            return EvaluatorObject.GetInstance(constantValue.value, null);

        var type = CorLibrary.GetSpecialType(constantValue.specialType);
        return EvaluatorObject.GetInstance(constantValue.value, type);
    }

    private EvaluatorObject EvaluateTypeOfExpression(BoundTypeOfExpression expression, ValueWrapper<bool> _) {
        return EvaluatorObject.GetInstance(expression.sourceType, expression.type);
    }

    private EvaluatorObject EvaluateMethodGroup(BoundMethodGroup methodGroup, ValueWrapper<bool> _) {
        return EvaluatorObject.GetInstance(methodGroup, methodGroup.type);
    }

    private EvaluatorObject EvaluateInitializerList(BoundInitializerList node, ValueWrapper<bool> abort) {
        var builder = new EvaluatorObject[node.items.Length];

        for (var i = 0; i < node.items.Length; i++)
            // builder[i] = CopyIfRef(EvaluateExpression(node.items[i], abort));
            builder[i] = EvaluateExpression(node.items[i], abort);

        return EvaluatorObject.GetInstance(builder, node.type);
    }

    private EvaluatorObject EvaluateTypeExpression(BoundTypeExpression _, ValueWrapper<bool> _2) {
        // This should only ever be called when an invalid expression statement makes it through binding without err
        // because script compilation ignores normal expression statement restrictions.
        //
        // `Console;`
        //
        return EvaluatorObject.GetInstance();
    }

    private EvaluatorObject EvaluateArrayAccessExpression(BoundArrayAccessExpression node, ValueWrapper<bool> abort) {
        var receiver = EvaluateExpression(node.receiver, abort);
        var index = EvaluateExpression(node.index, abort);
        var array = (EvaluatorObject[])Value(receiver);
        var indexValue = Convert.ToInt32(Value(index));

        if (array is null)
            throw new BelteNullReferenceException(node.syntax.location);

        if (indexValue >= array.Length)
            throw new BelteIndexOutOfRangeException(node.syntax.location);

        // return EvaluatorObject.GetInstance(
        //     null,
        //     array[indexValue],
        //     node.type
        // );
        return array[indexValue];
    }

    private EvaluatorObject EvaluateArrayCreationExpression(
        BoundArrayCreationExpression node,
        ValueWrapper<bool> abort) {
        var array = EvaluatorObject.GetInstance();
        var arrayType = (ArrayTypeSymbol)node.type;

        // TODO There is probably a more efficient algorithm for this
        foreach (var size in node.sizes) {
            foreach (var element in IterateElements(array)) {
                var sizeValue = (long)Value(EvaluateExpression(size, abort));
                var members = new EvaluatorObject[sizeValue];

                for (var i = 0; i < sizeValue; i++) {
                    // TODO This will use a default when default expression is added for primitives instead of null
                    members[i] = EvaluatorObject.GetInstance();
                    // members[i].isPersistent = true;
                }

                element.value = members;
            }
        }

        // TODO Can this be moved to the top of the method ('var array = new EvaluatorObject(value: null, arrayType)')
        array.type = arrayType;
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
        if (node.constructor.originalDefinition == CorLibrary.GetWellKnownMember(WellKnownMembers.Nullable_ctor))
            return EvaluateExpression(node.arguments[0], abort);

        var newObject = CreateObject((NamedTypeSymbol)node.type);
        InvokeMethodWithResolvedReceiver(node.constructor, node.arguments, newObject, abort, node.argumentRefKinds);
        return newObject;
    }

    private EvaluatorObject EvaluateCallExpression(BoundCallExpression expression, ValueWrapper<bool> abort) {
        if (CheckStandardMap(
            expression.syntax.location,
            expression.method,
            expression.receiver,
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
                return EvaluatorObject.GetInstance(result, expression.method.returnType);
            else
                return null;
        }

        return InvokeMethod(
            expression.method,
            expression.arguments,
            expression.receiver,
            expression.argumentRefKinds,
            abort
        );
    }

    private void ResolveMethodArguments(MethodSymbol method, EvaluatorObject[] arguments) {
        var locals = new Dictionary<Symbol, EvaluatorObject>();

        // if (method.callingConvention == CallingConvention.Template) ;
        // AddTemplatesToLocals(method.templateParameters, method.templateArguments, locals, abort);

        for (var i = 0; i < arguments.Length; i++) {
            var parameter = method.parameters[i];
            var value = arguments[i];

            // TODO This seemingly rids the reference, no?
            // while (parameter.refKind != RefKind.None && value.isReference)
            //     value = Get(value.reference);

            // locals.Add(parameter, Copy(value));
            // Fix?:
            if (parameter.refKind == RefKind.None)
                // value = CopyIfRef(value);
                value = Copy(value);

            locals.Add(parameter, value);
        }

        _locals.Push(locals);
    }

    private EvaluatorObject InvokeMethodWithResolvedReceiver(
        MethodSymbol method,
        ImmutableArray<BoundExpression> arguments,
        EvaluatorObject receiver,
        ValueWrapper<bool> abort,
        ImmutableArray<RefKind> argRefKinds,
        bool isBaseCall = false) {
        var evaluatedArguments = arguments.Select(a => EvaluateExpression(a, abort)).ToArray();

        if (argRefKinds != default) {
            for (var i = 0; i < arguments.Length; i++) {
                if (argRefKinds[i] == RefKind.None)
                    continue;

                if (!evaluatedArguments[i].isReference) {
                    evaluatedArguments[i] = EvaluatorObject.GetInstance(
                        null,
                        evaluatedArguments[i],
                        evaluatedArguments[i].type,
                        true
                    );
                } else {
                    evaluatedArguments[i].isExplicitReference = true;
                }
            }
        }

        return InvokeResolvedMethod(method, evaluatedArguments, receiver, abort, isBaseCall);
    }

    private EvaluatorObject InvokeResolvedMethod(
        MethodSymbol method,
        EvaluatorObject[] arguments,
        EvaluatorObject receiver,
        ValueWrapper<bool> abort,
        bool isBaseCall = false) {
        if (method.isAbstract || method.isVirtual) {
            var typeToLookup = isBaseCall ? receiver.type.StrippedType().baseType : receiver.type.StrippedType();

            var newMethod = typeToLookup
                .GetMembersUnordered()
                .Where(s => s is MethodSymbol m && m.overriddenMethod == method)
                .FirstOrDefault() as MethodSymbol;

            if (newMethod is not null)
                method = newMethod;
        }

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

        ResolveMethodArguments(method, arguments);

        var result = EvaluateStatement(method, statement, abort, out _);

        // while (_templateConstantDepth > templateConstantDepth) {
        //     _templateConstantDepth--;
        //     _locals.Pop();
        // }

        PopAndFreeLocals();

        if (enteredScope)
            ExitClassScope();

        return result;
    }

    private void PopAndFreeLocals() {
        // var frame =
        _locals.Pop();

        // foreach (var local in frame.Values) {
        //     if (!local.isPersistent)
        //         local.Free();
        // }
    }

    private EvaluatorObject InvokeMethod(
        MethodSymbol method,
        ImmutableArray<BoundExpression> arguments,
        BoundExpression receiver,
        ImmutableArray<RefKind> argRefKinds,
        ValueWrapper<bool> abort) {
        var receiverObject = default(EvaluatorObject);

        // Have to check receiver too because constructors don't have receivers
        if (receiver is not null && method.callingConvention == CallingConvention.HasThis) {
            receiverObject = EvaluateExpression(receiver, abort);
            var dereferencedReceiver = Dereference(receiverObject);

            if (dereferencedReceiver.members is null)
                throw new BelteNullReferenceException(receiver.syntax.location);
        }

        return InvokeMethodWithResolvedReceiver(
            method,
            arguments,
            receiverObject,
            abort,
            argRefKinds,
            receiver?.kind == BoundKind.BaseExpression
        );
    }

    private EvaluatorObject EvaluateDataContainerExpression(BoundDataContainerExpression expression) {
        return EvaluatorObject.GetInstance(
            expression.dataContainer,
            Get(expression.dataContainer),
            expression.dataContainer.type
        );
    }

    private EvaluatorObject EvaluateConditionalOperator(BoundConditionalOperator expression, ValueWrapper<bool> abort) {
        var condition = EvaluateExpression(expression.condition, abort);
        var conditionValue = Value(condition);

        if ((bool)conditionValue)
            return EvaluateExpression(expression.trueExpression, abort);
        else
            return EvaluateExpression(expression.falseExpression, abort);
    }

    private EvaluatorObject EvaluateNullAssertOperator(BoundNullAssertOperator expression, ValueWrapper<bool> abort) {
        if (!expression.throwIfNull)
            return EvaluateExpression(expression.operand, abort);

        return NullAssertObject(expression.operand, abort);
    }

    private EvaluatorObject EvaluateAsOperator(BoundAsOperator expression, ValueWrapper<bool> abort) {
        var left = EvaluateExpression(expression.left, abort);
        var leftValue = Value(left);
        var dereferenced = Dereference(left);

        if (dereferenced.members is null)
            return EvaluatorObject.GetInstance(leftValue, expression.type);

        if (dereferenced.type.InheritsFromIgnoringConstruction((NamedTypeSymbol)expression.right.type))
            return left;

        return EvaluatorObject.GetInstance();
    }

    private EvaluatorObject EvaluateIsOperator(BoundIsOperator expression, ValueWrapper<bool> abort) {
        var left = EvaluateExpression(expression.left, abort);
        var right = expression.right;
        var leftValue = Value(left);
        var dereferenced = Dereference(left);
        var isNot = expression.isNot;

        if (leftValue is null && dereferenced.members is null)
            return EvaluatorObject.GetInstance(isNot, expression.type);

        if (dereferenced.members is null) {
            var isTrue = (right.type.StrippedType().specialType == SpecialType.Any) ||
                (SpecialTypeExtensions.SpecialTypeFromLiteralValue(leftValue) == right.type.StrippedType().specialType);

            return EvaluatorObject.GetInstance(isNot ^ isTrue, expression.type);
        }

        if (dereferenced.type.InheritsFromIgnoringConstruction((NamedTypeSymbol)expression.right.type))
            return EvaluatorObject.GetInstance(!isNot, expression.type);
        else
            return EvaluatorObject.GetInstance(isNot, expression.type);
    }

    private EvaluatorObject EvaluateUnaryOperator(BoundUnaryOperator expression, ValueWrapper<bool> abort) {
        var operand = EvaluateExpression(expression.operand, abort);
        var operandValue = Value(operand);
        var opKind = expression.operatorKind.Operator();

        if (operandValue is null)
            return EvaluatorObject.GetInstance();

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

        return EvaluatorObject.GetInstance(result, expression.type);
    }

    private EvaluatorObject EvaluateBinaryOperator(BoundBinaryOperator expression, ValueWrapper<bool> abort) {
        var left = EvaluateExpression(expression.left, abort);
        var opKind = expression.operatorKind.OperatorWithConditional();

        if (opKind is BinaryOperatorKind.Equal or BinaryOperatorKind.NotEqual) {
            if (expression.right.IsLiteralNull()) {
                var isNull = ObjectIsNull(left, expression.left.type);
                return EvaluatorObject.GetInstance(isNull == (opKind == BinaryOperatorKind.Equal), expression.type);
            }
        }

        var leftValue = Value(left);

        if (opKind == BinaryOperatorKind.ConditionalAnd) {
            if (leftValue is null || !(bool)leftValue)
                return EvaluatorObject.GetInstance(false, expression.type);

            var shortCircuitRight = EvaluateExpression(expression.right, abort);
            var shortCircuitRightValue = Value(shortCircuitRight);

            if (shortCircuitRightValue is null || !(bool)shortCircuitRightValue)
                return EvaluatorObject.GetInstance(false, expression.type);

            return EvaluatorObject.GetInstance(true, expression.type);
        }

        if (opKind == BinaryOperatorKind.ConditionalOr) {
            if (leftValue != null && (bool)leftValue)
                return EvaluatorObject.GetInstance(true, expression.type);

            var shortCircuitRight = EvaluateExpression(expression.right, abort);
            var shortCircuitRightValue = Value(shortCircuitRight);

            if (shortCircuitRightValue != null && (bool)shortCircuitRightValue)
                return EvaluatorObject.GetInstance(true, expression.type);

            return EvaluatorObject.GetInstance(false, expression.type);
        }

        var right = EvaluateExpression(expression.right, abort);
        var rightValue = Value(right);

        if (opKind is BinaryOperatorKind.Equal or BinaryOperatorKind.NotEqual) {
            if (expression.left.type.specialType == SpecialType.Type) {
                if ((leftValue as BoundTypeExpression).type.Equals((rightValue as BoundTypeExpression).type))
                    return EvaluatorObject.GetInstance(opKind == BinaryOperatorKind.Equal, expression.type);
                else
                    return EvaluatorObject.GetInstance(opKind == BinaryOperatorKind.NotEqual, expression.type);
            }

            if (expression.left.type.specialType == SpecialType.Object) {
                // Reference equality
                var refEquals = left.reference == right.reference;
                var positive = opKind == BinaryOperatorKind.Equal;
                return EvaluatorObject.GetInstance(positive == refEquals, expression.type);
            }
        }

        if (leftValue is null || rightValue is null)
            return EvaluatorObject.GetInstance();

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
                if (expressionType == SpecialType.Int) {
                    var rightLong = (long)rightValue;

                    if (rightLong == 0)
                        throw new BelteDivideByZeroException(expression.syntax.location);

                    result = (long)leftValue / rightLong;
                } else {
                    var rightDouble = Convert.ToDouble(rightValue);

                    if (rightDouble == 0)
                        throw new BelteDivideByZeroException(expression.syntax.location);

                    result = Convert.ToDouble(leftValue) / rightDouble;
                }

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

        return EvaluatorObject.GetInstance(result, expression.type);
    }

    private EvaluatorObject EvaluateAssignmentOperator(
        BoundAssignmentOperator expression,
        ValueWrapper<bool> abort) {
        var left = EvaluateExpression(expression.left, abort);
        var right = EvaluateExpression(expression.right, abort);

        if (expression.isRef)
            right.isExplicitReference = true;

        Assign(left, right, expression.right.kind);
        return right;
    }

    private EvaluatorObject EvaluateParameterExpression(BoundParameterExpression expression) {
        return EvaluatorObject.GetInstance(
            expression.parameter,
            Get(expression.parameter),
            expression.parameter.type
        );
    }

    private EvaluatorObject EvaluateFieldAccessExpression(
        BoundFieldAccessExpression expression,
        ValueWrapper<bool> abort) {
        var operand = Dereference(EvaluateExpression(expression.receiver, abort));

        if (operand.members is null)
            throw new BelteNullReferenceException(expression.syntax.location);

        // return EvaluatorObject.GetInstance(
        //     expression.field,
        //     operand.members[expression.field],
        //     expression.field.type
        // );
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
        return EvaluateCast(expression.syntax.location, value, expression.operand.type, expression.type);
    }

    private EvaluatorObject EvaluateCast(
        TextLocation location,
        EvaluatorObject value,
        TypeSymbol source,
        TypeSymbol target) {
        var dereferenced = Dereference(value);
        var strippedTarget = target.StrippedType();
        var strippedSource = source.StrippedType();

        if (dereferenced.members is null) {
            var valueValue = Value(value);

            if (valueValue is null) {
                if (target.specialType != SpecialType.Nullable)
                    throw new BelteNullReferenceException(location);

                return EvaluatorObject.GetInstance(valueValue, strippedTarget);
            }

            if (strippedSource.Equals(strippedTarget, TypeCompareKind.IgnoreNullability))
                return value;

            return EvaluatorObject.GetInstance(LiteralUtilities.Cast(valueValue, strippedTarget), target);
        }

        if (dereferenced.type.InheritsFromIgnoringConstruction((NamedTypeSymbol)strippedTarget))
            return value;

        throw new BelteInvalidCastException(location);
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
            result = NullAssertObject(receiver, abort);
            return true;
        }

        if (mapKey == "Nullable_get_HasValue") {
            var receiverValue = EvaluateExpression(receiver, abort);
            result = EvaluatorObject.GetInstance(!ObjectIsNull(receiverValue, receiver.type), method.returnType);
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
                    result = Dereference(EvaluateExpression(arguments[0], abort)).GetHashCode();
                    return true;
                case "LowLevel_GetTypeName_O":
                    result = EvaluateExpression(arguments[0], abort).type.name;
                    return true;
                case "Random_RandInt_I?":
                    _lazyRandom ??= new Random();
                    var max = Value(EvaluateExpression(arguments[0], abort));
                    result = Convert.ToInt64(_lazyRandom.Next(Convert.ToInt32(max)));
                    return true;
                case "LowLevel_ThrowNullConditionException":
                    throw new BelteNullConditionException(location);
                case "Random_Random":
                    _lazyRandom ??= new Random();
                    result = _lazyRandom.NextDouble();
                    return true;
                case "LowLevel_Sort_A?": {
                        var array = Dereference(EvaluateExpression(arguments[0], abort));

                        if (array.value is not EvaluatorObject[] ea)
                            return true;

                        Array.Sort(ea, (a, b) => Convert.ToDouble(a.value).CompareTo(Convert.ToDouble(b.value)));
                    }

                    return true;
                case "String_Split_SS": {
                        var args = arguments.Select(a => Value(EvaluateExpression(a, abort))).ToArray();
                        var text = (string)args[0];
                        var separator = (string)args[1];
                        var res = text.Split(separator);

                        result = res.Select(
                            r => EvaluatorObject.GetInstance(r, CorLibrary.GetSpecialType(SpecialType.String))
                        ).ToArray();
                    }

                    return true;
                case "Console_Print_S?":
                case "Console_Print_A?":
                case "Console_Print_O?":
                    printed = true;

                    if (arguments[0].type.StrippedType().isObjectType) {
                        var toStringResult = InvokeMethod(_toStringMethod, [], arguments[0], [], abort);
                        var func = StandardLibrary.EvaluatorMap[mapKey];
                        result = func(Value(toStringResult), null, null);
                        return true;
                    }

                    break;
            }

            var function = StandardLibrary.EvaluatorMap[mapKey];
            var valueArguments = arguments.Select(a => Value(EvaluateExpression(a, abort))).ToArray();

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

        if (mapKey == "Graphics_Initialize_SIIB") {
            var valueArguments = arguments.Select(a => Value(EvaluateExpression(a, abort))).ToArray();

            StartGraphics(
                (string)valueArguments[0],
                Convert.ToInt32(valueArguments[1]),
                Convert.ToInt32(valueArguments[2]),
                Convert.ToBoolean(valueArguments[3]),
                abort
            );

            return true;
        }

        if (_context.graphicsThread is null)
            throw new BelteEvaluatorException("All Graphics calls must come after Graphics.Initialize", location);

        while (_context.graphicsHandler?.GraphicsDevice is null)
            Thread.SpinWait(1);

        switch (mapKey) {
            case "Graphics_LoadTexture_S": {
                    var path = GetFilePath((string)Value(EvaluateExpression(arguments[0], abort)), location)
                        ?? throw new BelteEvaluatorException("Cannot load texture path does not exist", location);

                    result = LoadTexture(path);
                }

                break;
            case "Graphics_LoadTexture_SIII": {
                    var evaluatedArguments = arguments.Select(a => EvaluateExpression(a, abort)).ToArray();
                    var path = GetFilePath((string)Value(evaluatedArguments[0]), location)
                        ?? throw new BelteEvaluatorException("Cannot load texture path does not exist", location);

                    var r = evaluatedArguments[1].value;
                    var g = evaluatedArguments[2].value;
                    var b = evaluatedArguments[3].value;

                    result = LoadTexture(path, true, r, g, b);
                }

                break;
            case "Graphics_LoadSprite_SV?V?I?": {
                    var evaluatedArguments = arguments.Select(a => EvaluateExpression(a, abort)).ToArray();
                    var path = GetFilePath((string)Value(evaluatedArguments[0]), location)
                        ?? throw new BelteEvaluatorException("Cannot load sprite: path does not exist", location);

                    var spriteType = CorLibrary.GetSpecialType(SpecialType.Sprite);
                    var sprite = CreateObject(spriteType);

                    InvokeResolvedMethod(
                        spriteType.constructors[0],
                        [
                            LoadTexture(path),
                            evaluatedArguments[1],
                            evaluatedArguments[2],
                            evaluatedArguments[3]
                        ],
                        sprite,
                        abort
                    );

                    result = sprite;
                }

                break;
            case "Graphics_DrawSprite_S?": {
                    var argument = Dereference(EvaluateExpression(arguments[0], abort));

                    if (argument.members is null)
                        return true;

                    DrawSprite(argument, null, out result);
                }

                break;
            case "Graphics_DrawSprite_S?V?": {
                    var evaluatedArguments = arguments.Select(a => EvaluateExpression(a, abort)).ToArray();
                    var sprite = Dereference(evaluatedArguments[0]);

                    if (sprite.members is null)
                        return true;

                    DrawSprite(sprite, Dereference(evaluatedArguments[1]), out result);
                }

                break;
            case "Graphics_StopDraw_I?": {
                    var argument = Value(EvaluateExpression(arguments[0], abort));

                    if (argument is null)
                        return true;

                    _context.graphicsHandler.RemoveAction(Convert.ToInt32(argument));
                }

                break;
            case "Graphics_LoadText_S?SV?DD?I?I?I?": {
                    var evaluatedArguments = arguments.Select(a => EvaluateExpression(a, abort)).ToArray();
                    var path = GetFilePath((string)Value(evaluatedArguments[1]), location)
                        ?? throw new BelteEvaluatorException("Cannot load text: path does not exist", location);

                    var textType = CorLibrary.GetSpecialType(SpecialType.Text);
                    var text = CreateObject(textType);
                    var textFields = textType.GetMembers().Where(f => f is FieldSymbol).ToArray();

                    var fontSize = Convert.ToSingle(Value(evaluatedArguments[3]));

                    Assign(text.members[textFields[0]], evaluatedArguments[0]);
                    Assign(text.members[textFields[1]], evaluatedArguments[1]);
                    Assign(text.members[textFields[2]], evaluatedArguments[2]);
                    Assign(text.members[textFields[3]], evaluatedArguments[3]);
                    Assign(text.members[textFields[4]], evaluatedArguments[4]);
                    Assign(text.members[textFields[5]], evaluatedArguments[5]);
                    Assign(text.members[textFields[6]], evaluatedArguments[6]);
                    Assign(text.members[textFields[7]], evaluatedArguments[7]);

                    text.members[HiddenTextData] = EvaluatorObject.GetInstance(
                        _context.graphicsHandler.LoadText(path, fontSize),
                        null
                    );

                    result = text;
                }

                break;
            case "Graphics_DrawText_T?": {
                    var argument = Dereference(EvaluateExpression(arguments[0], abort));

                    if (argument.members is null)
                        return true;

                    var text = (string)Slot(argument, 0).value;
                    var posX = Slot(Slot(argument, 2), 0).value;
                    var posY = Slot(Slot(argument, 2), 1).value;
                    var r = Slot(argument, 5).value;
                    var g = Slot(argument, 6).value;
                    var b = Slot(argument, 7).value;

                    if (Slot(argument, 8).value is not DynamicSpriteFont spriteFont)
                        throw new BelteEvaluatorException("Cannot draw text: invalid text object", location);

                    if (_context.options.isScript) {
                        result = _context.graphicsHandler.AddAction(
                            () => { _context.graphicsHandler.DrawText(spriteFont, text, posX, posY, r, g, b); }
                        );
                    } else {
                        _context.graphicsHandler.DrawText(spriteFont, text, posX, posY, r, g, b);
                    }
                }

                break;
            case "Graphics_GetKey_S": {
                    var argument = (string)Value(EvaluateExpression(arguments[0], abort));
                    result = _context.graphicsHandler.GetKey(argument);
                }

                break;
            case "Graphics_GetMouseButton_S": {
                    var argument = (string)Value(EvaluateExpression(arguments[0], abort));
                    result = _context.graphicsHandler.GetMouseButton(argument);
                }

                break;
            case "Graphics_GetScroll": {
                    result = Convert.ToInt64(_context.graphicsHandler.GetScroll());
                }

                break;
            case "Graphics_GetMousePosition": {
                    var (x, y) = _context.graphicsHandler.GetMousePosition();
                    var vecType = CorLibrary.GetSpecialType(SpecialType.Vec2);
                    var vec = CreateObject(vecType);
                    var decimalType = CorLibrary.GetSpecialType(SpecialType.Decimal);

                    InvokeResolvedMethod(
                        vecType.constructors[0],
                        [
                            EvaluatorObject.GetInstance(Convert.ToDouble(x), decimalType),
                            EvaluatorObject.GetInstance(Convert.ToDouble(y), decimalType)
                        ],
                        vec,
                        abort
                    );

                    result = vec;
                }

                break;
            case "Graphics_DrawRect_R?I?I?I?":
                DrawRect(false, out result);
                break;
            case "Graphics_DrawRect_R?I?I?I?I?":
                DrawRect(true, out result);
                break;
            case "Graphics_Fill_III": {
                    var evaluatedArguments = arguments.Select(a => EvaluateExpression(a, abort)).ToArray();

                    var r = evaluatedArguments[0].value;
                    var g = evaluatedArguments[1].value;
                    var b = evaluatedArguments[2].value;

                    if (_context.options.isScript) {
                        result = _context.graphicsHandler.AddAction(
                            () => { _context.graphicsHandler.Fill(r, g, b); }
                        );
                    } else {
                        _context.graphicsHandler.Fill(r, g, b);
                    }
                }

                break;
            case "Graphics_Draw_T?R?R?I?B?D?": {
                    var evaluatedArguments = arguments.Select(a => EvaluateExpression(a, abort)).ToArray();
                    var texture = Dereference(evaluatedArguments[0]);

                    if (Slot(texture, 2).value is not Texture2D texture2D)
                        throw new BelteEvaluatorException("Cannot draw: null texture", location);

                    var srcRect = Dereference(evaluatedArguments[1]);
                    var dstRect = Dereference(evaluatedArguments[2]);
                    var rotation = evaluatedArguments[3].value;
                    var flip = evaluatedArguments[4].value;
                    var alpha = evaluatedArguments[5].value;

                    if (_context.options.isScript) {
                        result = _context.graphicsHandler.AddAction(
                            () => {
                                _context.graphicsHandler.Draw(texture2D, srcRect, dstRect, rotation, flip, alpha);
                            }
                        );
                    } else {
                        _context.graphicsHandler.Draw(texture2D, srcRect, dstRect, rotation, flip, alpha);
                        result = null;
                    }
                }

                break;
            case "Graphics_LoadSound_S": {
                    var path = GetFilePath((string)Value(EvaluateExpression(arguments[0], abort)), location)
                        ?? throw new BelteEvaluatorException("Cannot load sound: path does not exist", location);

                    var soundType = CorLibrary.GetSpecialType(SpecialType.Sound);
                    var sound = CreateObject(soundType);
                    sound.members[HiddenSoundData] = EvaluatorObject.GetInstance(
                        _context.graphicsHandler.LoadSound(path),
                        null
                    );

                    result = sound;
                }

                break;
            case "Graphics_PlaySound_S": {
                    var argument = Dereference(EvaluateExpression(arguments[0], abort));
                    var volume = Slot(argument, 0).value;
                    var loop = Slot(argument, 1).value;
                    var soundInstance = argument.members[HiddenSoundData];
                    _context.graphicsHandler.PlaySound((SoundEffect)soundInstance.value, volume, loop);
                }

                break;
            case "Graphics_SetCursorVisibility_B": {
                    var argument = (bool)Value(EvaluateExpression(arguments[0], abort));
                    _context.graphicsHandler.SetCursorVisibility(argument);
                }

                break;
            case "Graphics_LockFramerate_I": {
                    var argument = Convert.ToInt32(Value(EvaluateExpression(arguments[0], abort)));
                    _context.graphicsHandler.LockFramerate(argument);
                }

                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(mapKey);
        }

        return true;

        void DrawRect(bool includeAlpha, out object result) {
            result = null;
            var evaluatedArguments = arguments.Select(a => EvaluateExpression(a, abort)).ToArray();
            var rect = Dereference(evaluatedArguments[0]);

            if (rect.members is null)
                return;

            var (x, y, w, h) = ExtractRectangleComponents(rect);
            var r = evaluatedArguments[1].value;
            var g = evaluatedArguments[2].value;
            var b = evaluatedArguments[3].value;
            var a = includeAlpha ? evaluatedArguments[4].value : null;

            if (_context.options.isScript) {
                result = _context.graphicsHandler.AddAction(
                    () => { _context.graphicsHandler.DrawRect(x, y, w, h, r, g, b, a); }
                );
            } else {
                _context.graphicsHandler.DrawRect(x, y, w, h, r, g, b, a);
            }
        }

        void DrawSprite(EvaluatorObject sprite, EvaluatorObject offsetVec, out object result) {
            var (sx, sy, sw, sh) = ExtractRectangleComponents(Slot(sprite, 2));
            var (dx, dy, dw, dh) = ExtractRectangleComponents(Slot(sprite, 3));
            var rotation = Slot(sprite, 1).value;

            if (Slot(Slot(sprite, 4), 2).value is not Texture2D texture)
                throw new BelteEvaluatorException("Cannot draw sprite: it has a null texture", location);

            if (offsetVec is not null) {
                dx -= Convert.ToInt32(Slot(offsetVec, 0).value);
                dy -= Convert.ToInt32(Slot(offsetVec, 1).value);
            }

            if (_context.options.isScript) {
                result = _context.graphicsHandler.AddAction(
                    () => {
                        _context.graphicsHandler.DrawSprite(texture, sx, sy, sw, sh, dx, dy, dw, dh, rotation);
                    }
                );
            } else {
                _context.graphicsHandler.DrawSprite(texture, sx, sy, sw, sh, dx, dy, dw, dh, rotation);
                result = null;
            }
        }

        EvaluatorObject LoadTexture(
            string path,
            bool useColorKey = false,
            object r = null,
            object g = null,
            object b = null) {
            var textureType = CorLibrary.GetSpecialType(SpecialType.Texture);
            var texture = CreateObject(textureType);
            var textureFields = textureType.GetMembers().Where(f => f is FieldSymbol).ToArray();
            var texture2D = _context.graphicsHandler?.LoadTexture(path, useColorKey, r, g, b);

            Assign(texture.members[textureFields[0]], EvaluatorObject.GetInstance(
                Convert.ToInt64(texture2D.Width),
                CorLibrary.GetSpecialType(SpecialType.Int)
            ));

            Assign(texture.members[textureFields[1]], EvaluatorObject.GetInstance(
                Convert.ToInt64(texture2D.Height),
                CorLibrary.GetSpecialType(SpecialType.Int)
            ));

            texture.members[HiddenTextureData] = EvaluatorObject.GetInstance(texture2D, null);

            return texture;
        }
    }

    internal static (int x, int y, int w, int h) ExtractRectangleComponents(EvaluatorObject evaluatorObject) {
        return (
            Convert.ToInt32(Slot(evaluatorObject, 0).value),
            Convert.ToInt32(Slot(evaluatorObject, 1).value),
            Convert.ToInt32(Slot(evaluatorObject, 2).value),
            Convert.ToInt32(Slot(evaluatorObject, 3).value)
        );
    }

    private static EvaluatorObject Slot(EvaluatorObject evaluatorObject, int slot) {
        return evaluatorObject.members.Values.ElementAt(slot);
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
                    using var graphicsHandler = new GraphicsHandler(abort, usePointClamp);
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

        var argument = EvaluatorObject.GetInstance(deltaTime, CorLibrary.GetSpecialType(SpecialType.Decimal));
        InvokeResolvedMethod(_program.updatePoint, [argument], _programObject, abort);

        if (exceptions.Count > 0)
            abort.Value = true;
    }

    #endregion
}
