using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Resolves overloads to find the best overload.
/// </summary>
internal sealed class OverloadResolution {
    private readonly Binder _binder;

    /// <summary>
    /// Creates an <see cref="OverloadResolution" />, uses a Binders diagnostics.
    /// </summary>
    /// <param name="binder"><see cref="Binder" /> to use diagnostics from.</param>
    internal OverloadResolution(Binder binder) {
        _binder = binder;
    }

    /// <summary>
    /// Uses partially bound arguments to find the best method overload and resolves it. Does not log diagnostics.
    /// </summary>
    /// <param name="types">Available overloads.</param>
    /// <param name="arguments">Bound arguments.</param>
    /// <param name="name">The name of the method.</param>
    /// <param name="operand">Original expression operand, used for diagnostic locations.</param>
    /// <param name="argumentList">The original arguments, used for calculations.</param>
    internal OverloadResolutionResult<MethodSymbol> SuppressedMethodOverloadResolution(
        ImmutableArray<MethodSymbol> methods,
        ImmutableArray<(string name, BoundExpression expression)> arguments,
        string name,
        SyntaxNodeOrToken operand,
        ArgumentListSyntax argumentList,
        BoundType receiverType) {
        var tempDiagnostics = new BelteDiagnosticQueue();
        tempDiagnostics.Move(_binder.diagnostics);

        var result = MethodOverloadResolution(methods, arguments, name, operand, argumentList, [], receiverType);

        _binder.diagnostics.Clear();
        _binder.diagnostics.Move(tempDiagnostics);

        return result;
    }

    /// <summary>
    /// Uses partially bound arguments to find the best method overload and resolves it.
    /// </summary>
    /// <param name="types">Available overloads.</param>
    /// <param name="arguments">Bound arguments.</param>
    /// <param name="name">The name of the method.</param>
    /// <param name="operand">Original expression operand, used for diagnostic locations.</param>
    /// <param name="argumentList">The original arguments, used for calculations.</param>
    internal OverloadResolutionResult<MethodSymbol> MethodOverloadResolution(
        ImmutableArray<MethodSymbol> methods,
        ImmutableArray<(string name, BoundExpression expression)> arguments,
        string name,
        SyntaxNodeOrToken operand,
        ArgumentListSyntax argumentList,
        ImmutableArray<TypeOrConstant>? templateArguments,
        BoundType receiverType) {
        var minScore = int.MaxValue;
        var possibleOverloads = new List<MethodSymbol>();

        var boundArguments = ArrayBuilder<BoundExpression>.GetInstance();

        var tempDiagnostics = new BelteDiagnosticQueue();
        tempDiagnostics.Move(_binder.diagnostics);

        var isConstructor = false;

        foreach (var method in methods) {
            var preBoundArgumentsBuilder = ArrayBuilder<(string name, BoundExpression expression)>.GetInstance();
            preBoundArgumentsBuilder.AddRange(arguments);

            var beforeCount = _binder.diagnostics.Count;
            var score = 0;
            var isInner = method.name.Contains(">g__");

            isConstructor = method.name == WellKnownMemberNames.InstanceConstructorName;

            var defaultParameterCount = method.parameters.Where(p => p.defaultValue is not null).Count();
            var expressionArgumentsCount = argumentList?.arguments?.Count ?? arguments.Length;
            var expressionArguments = argumentList?.arguments;

            if (expressionArgumentsCount < method.parameters.Length - defaultParameterCount ||
                expressionArgumentsCount > method.parameters.Length) {
                var count = 0;

                if (isInner) {
                    foreach (var parameter in method.parameters) {
                        if (parameter.name.StartsWith("$"))
                            count++;
                    }
                }

                if (!isInner || expressionArgumentsCount + count != method.parameters.Length) {
                    ResolveIncorrectArgumentCount(
                        operand,
                        argumentList?.closeParenthesis?.span,
                        method.name,
                        method.parameters,
                        defaultParameterCount,
                        expressionArgumentsCount,
                        expressionArguments,
                        false
                    );

                    continue;
                }
            }

            var canContinue = CalculateArgumentRearrangements(
                methods.Length,
                name,
                preBoundArgumentsBuilder,
                method.parameters,
                expressionArgumentsCount,
                expressionArguments,
                out var rearrangedArguments,
                out var seenParameterNames
            );

            for (var i = 0; i < method.parameters.Length; i++) {
                var parameter = method.parameters[i];

                if (!parameter.name.StartsWith('$') &&
                    seenParameterNames.Add(parameter.name) &&
                    parameter.defaultValue is not null) {
                    rearrangedArguments[i] = preBoundArgumentsBuilder.Count;
                    preBoundArgumentsBuilder.Add((parameter.name, parameter.defaultValue));
                }
            }

            var currentBoundArguments = ArrayBuilder<BoundExpression>.GetInstance();

            if (canContinue) {
                score = RearrangeArguments(
                    method.parameters,
                    score,
                    expressionArgumentsCount,
                    expressionArguments,
                    rearrangedArguments,
                    preBoundArgumentsBuilder.ToImmutableAndFree(),
                    currentBoundArguments,
                    false,
                    templateArguments,
                    receiverType
                );

                if (isInner) {
                    for (var i = 0; i < method.parameters.Length; i++) {
                        var parameter = method.parameters[i];

                        if (!parameter.name.StartsWith('$'))
                            continue;

                        var argument = SyntaxFactory.Reference(parameter.name.Substring(1));
                        var boundArgument = _binder.BindCast(
                            argument,
                            parameter.type,
                            argument: i,
                            receiverType: receiverType,
                            templateArguments: templateArguments
                        );

                        currentBoundArguments.Add(boundArgument);
                    }
                }
            }

            if (methods.Length == 1 && _binder.diagnostics.AnyErrors()) {
                CleanUpDiagnostics(methods, tempDiagnostics);
                return OverloadResolutionResult<MethodSymbol>.Failed();
            }

            minScore = UpdateScore(
                minScore,
                possibleOverloads,
                boundArguments,
                method,
                beforeCount,
                score,
                currentBoundArguments
            );

            currentBoundArguments.Free();
        }

        CleanUpDiagnostics(methods, tempDiagnostics);

        if (methods.Length > 1 && possibleOverloads.Count == 0) {
            if (isConstructor)
                _binder.diagnostics.Push(Error.NoConstructorOverload(operand.location, methods[0].containingType.name));
            else
                _binder.diagnostics.Push(Error.NoMethodOverload(operand.location, name));

            return OverloadResolutionResult<MethodSymbol>.Failed();
        } else if (methods.Length > 1 && possibleOverloads.Count > 1) {
            // Special case where there are default overloads
            if (possibleOverloads[0].name == "HasValue") {
                possibleOverloads.Clear();
                possibleOverloads.Add(BuiltinMethods.HasValueAny);
            } else if (possibleOverloads[0].name == "Value") {
                possibleOverloads.Clear();
                possibleOverloads.Add(BuiltinMethods.ValueAny);
            } else {
                // Attempts to still find a preferred overload even if the parameter list is ambiguous
                // Prefers:
                //      - Methods that are accessible
                //      - Methods with fewer parameters
                //      - Methods from more direct parent types
                var tempPossibleOverloads = new List<MethodSymbol>();

                if (receiverType is not null) {
                    foreach (var overload in possibleOverloads) {
                        if (overload.declaredAccessibility == Accessibility.Public) {
                            tempPossibleOverloads.Add(overload);
                        } else if (overload.declaredAccessibility == Accessibility.Protected &&
                            TypeUtilities.TypeInheritsFrom(receiverType.typeSymbol, overload.containingType)) {
                            tempPossibleOverloads.Add(overload);
                        } else if (overload.declaredAccessibility == Accessibility.Private && receiverType.typeSymbol == overload.containingType) {
                            tempPossibleOverloads.Add(overload);
                        }
                    }

                    if (tempPossibleOverloads.Count > 0)
                        possibleOverloads = tempPossibleOverloads;
                }

                if (possibleOverloads.Count > 1) {
                    tempPossibleOverloads = new List<MethodSymbol>();
                    var minArguments = int.MaxValue;

                    foreach (var overload in possibleOverloads) {
                        if (overload.parameters.Length < minArguments) {
                            tempPossibleOverloads.Clear();
                            minArguments = overload.parameters.Length;
                        }

                        if (overload.parameters.Length == minArguments)
                            tempPossibleOverloads.Add(overload);
                    }

                    possibleOverloads = tempPossibleOverloads;

                    if (possibleOverloads.Count > 1) {
                        if (receiverType is not null) {
                            var minBaseDepth = int.MaxValue;
                            tempPossibleOverloads = new List<MethodSymbol>();

                            foreach (var overload in possibleOverloads) {
                                var depth = TypeUtilities.GetInheritanceDepth(receiverType.typeSymbol, overload.containingType);

                                if (depth < minBaseDepth) {
                                    tempPossibleOverloads.Clear();
                                    minBaseDepth = depth;
                                }

                                if (depth == minBaseDepth)
                                    tempPossibleOverloads.Add(overload);
                            }

                            possibleOverloads = tempPossibleOverloads;
                        }

                        if (possibleOverloads.Count > 1) {
                            _binder.diagnostics.Push(
                                Error.AmbiguousMethodOverload(operand.location, possibleOverloads.ToArray())
                            );

                            return OverloadResolutionResult<MethodSymbol>.Ambiguous();
                        }
                    }
                }
            }
        } else if (methods.Length == 1 && possibleOverloads.Count == 0) {
            possibleOverloads.Add(methods[0]);
        }

        return OverloadResolutionResult<MethodSymbol>.Succeeded(
            possibleOverloads.SingleOrDefault(),
            boundArguments.ToImmutableAndFree()
        );
    }

    /// <summary>
    /// Uses partially bound arguments to find the best template overload and resolves it.
    /// </summary>
    /// <param name="symbols">Available overloads.</param>
    /// <param name="arguments">Bound arguments.</param>
    /// <param name="name">The name of the template.</param>
    /// <param name="operand">Original expression operand, used for diagnostic locations.</param>
    /// <param name="argumentList">The original arguments, used for calculations.</param>
    internal OverloadResolutionResult<ISymbolWithTemplates> TemplateOverloadResolution(
        ImmutableArray<ISymbolWithTemplates> symbols,
        ImmutableArray<(string name, TypeOrConstant constant)> arguments,
        string name,
        SyntaxNodeOrToken operand,
        TemplateArgumentListSyntax argumentList) {
        var minScore = int.MaxValue;
        var possibleOverloads = new List<ISymbolWithTemplates>();

        var boundArguments = ArrayBuilder<BoundExpression>.GetInstance();
        var preBoundArgumentsBuilder = ArrayBuilder<(string name, BoundExpression expression)>.GetInstance();

        foreach (var argument in arguments) {
            if (argument.constant.isConstant) {
                BoundExpression expression;

                if (argument.constant.constant is null)
                    expression = argument.constant.expression;
                else if (argument.constant.constant.value is ImmutableArray<ConstantValue>)
                    expression = new BoundInitializerListExpression(argument.constant.constant, argument.constant.type);
                else
                    expression = new BoundLiteralExpression(argument.constant.constant.value);

                preBoundArgumentsBuilder.Add((argument.name, expression));
            } else {
                preBoundArgumentsBuilder.Add((argument.name, argument.constant.type));
            }
        }

        var tempDiagnostics = new BelteDiagnosticQueue();
        tempDiagnostics.Move(_binder.diagnostics);

        foreach (var symbol in symbols) {
            var beforeCount = _binder.diagnostics.Count;
            var score = 0;

            var defaultParameterCount = symbol.templateParameters.Where(p => p.defaultValue is not null).Count();
            var expressionArguments = argumentList?.arguments;
            var argumentCount = expressionArguments?.Count ?? 0;

            if (argumentCount < symbol.templateParameters.Length - defaultParameterCount ||
                argumentCount > symbol.templateParameters.Length) {
                if (argumentCount != symbol.templateParameters.Length) {
                    ResolveIncorrectArgumentCount(
                        operand,
                        argumentList?.closeAngleBracket?.span,
                        symbol.name,
                        symbol.templateParameters,
                        defaultParameterCount,
                        expressionArguments?.Count ?? 0,
                        expressionArguments,
                        true
                    );

                    continue;
                }
            }

            var canContinue = CalculateArgumentRearrangements(
                symbols.Length,
                name,
                preBoundArgumentsBuilder,
                symbol.templateParameters,
                expressionArguments?.Count ?? 0,
                expressionArguments,
                out var rearrangedArguments,
                out var seenParameterNames
            );

            for (var i = 0; i < symbol.templateParameters.Length; i++) {
                var parameter = symbol.templateParameters[i];

                if (!parameter.name.StartsWith('$') &&
                    seenParameterNames.Add(parameter.name) &&
                    parameter.defaultValue is not null) {
                    rearrangedArguments[i] = preBoundArgumentsBuilder.Count;
                    preBoundArgumentsBuilder.Add((parameter.name, parameter.defaultValue));
                }
            }

            var currentBoundArguments = ArrayBuilder<BoundExpression>.GetInstance();

            if (canContinue) {
                score = RearrangeArguments(
                    symbol.templateParameters,
                    score,
                    expressionArguments?.Count ?? 0,
                    expressionArguments,
                    rearrangedArguments,
                    preBoundArgumentsBuilder.ToImmutableAndFree(),
                    currentBoundArguments,
                    true,
                    null,
                    null
                );
            }

            if (symbols.Length == 1 && _binder.diagnostics.AnyErrors()) {
                CleanUpDiagnostics(symbols, tempDiagnostics);
                return OverloadResolutionResult<ISymbolWithTemplates>.Failed();
            }

            minScore = UpdateScore(
                minScore,
                possibleOverloads,
                boundArguments,
                symbol,
                beforeCount,
                score,
                currentBoundArguments
            );

            currentBoundArguments.Free();
        }

        CleanUpDiagnostics(symbols, tempDiagnostics);

        if (symbols.Length > 1 && possibleOverloads.Count == 0) {
            _binder.diagnostics.Push(Error.NoTemplateOverload(operand.location, name));
            return OverloadResolutionResult<ISymbolWithTemplates>.Failed();
        } else if (symbols.Length > 1 && possibleOverloads.Count > 1) {
            var reference = possibleOverloads[0];
            var failed = false;

            foreach (var possibleOverload in possibleOverloads) {
                if (possibleOverload.templateParameters.Length == reference.templateParameters.Length &&
                    possibleOverload.templateConstraints.Length == reference.templateConstraints.Length) {
                    for (var i = 0; i < possibleOverload.templateParameters.Length; i++) {
                        if (possibleOverload.templateParameters[i].type != reference.templateParameters[i].type) {
                            failed = true;
                            break;
                        }
                    }

                    if (!failed) {
                        for (var i = 0; i < possibleOverload.templateConstraints.Length; i++) {
                            if (possibleOverload.templateConstraints[i] != reference.templateConstraints[i]) {
                                failed = true;
                                break;
                            }
                        }
                    }
                }

                if (failed) {
                    _binder.diagnostics.Push(
                        Error.AmbiguousTemplateOverload(operand.location, possibleOverloads.ToArray())
                    );

                    return OverloadResolutionResult<ISymbolWithTemplates>.Ambiguous();
                }
            }

            return OverloadResolutionResult<ISymbolWithTemplates>.Succeeded(
                possibleOverloads.ToArray(),
                boundArguments.ToImmutableAndFree()
            );
        } else if (symbols.Length == 1 && possibleOverloads.Count == 0) {
            possibleOverloads.Add(symbols[0]);
        }

        return OverloadResolutionResult<ISymbolWithTemplates>.Succeeded(
            possibleOverloads.SingleOrDefault(),
            boundArguments.ToImmutableAndFree()
        );
    }

    internal void ResolveIncorrectArgumentCount(
        SyntaxNodeOrToken operand,
        TextSpan closingSpan,
        string name,
        ImmutableArray<ParameterSymbol> parameters,
        int defaultParameterCount,
        int argumentCount,
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        bool isTemplate) {
        TextSpan span;
        if (argumentCount > parameters.Length) {
            SyntaxNodeOrToken firstExceedingNode;

            if (argumentCount > 1 && parameters.Length > 0) {
                firstExceedingNode = arguments.GetSeparator(parameters.Length - 1);
            } else {
                firstExceedingNode = arguments[0].kind == SyntaxKind.EmptyExpression
                    ? arguments.GetSeparator(0)
                    : arguments[0];
            }

            SyntaxNodeOrToken lastExceedingNode = arguments.Last().kind == SyntaxKind.EmptyExpression
                ? arguments.GetSeparator(argumentCount - 2)
                : arguments.Last();

            span = TextSpan.FromBounds(firstExceedingNode.span.start, lastExceedingNode.span.end);
        } else {
            span = closingSpan ?? operand.span;
        }

        var location = new TextLocation(operand.syntaxTree.text, span);

        _binder.diagnostics.Push(Error.IncorrectArgumentCount(
            location,
            name,
            parameters.Length,
            defaultParameterCount,
            argumentCount,
            isTemplate
        ));
    }

    private bool CalculateArgumentRearrangements(
        int overloadCount,
        string name,
        ArrayBuilder<(string name, BoundExpression expression)> preBoundArgumentsBuilder,
        ImmutableArray<ParameterSymbol> parameters,
        int argumentCount,
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        out Dictionary<int, int> rearrangedArguments,
        out HashSet<string> seenParameterNames) {
        rearrangedArguments = new Dictionary<int, int>();
        seenParameterNames = new HashSet<string>();
        var canContinue = true;

        for (var i = 0; i < argumentCount; i++) {
            var argumentName = preBoundArgumentsBuilder[i].name;

            if (argumentName is null) {
                seenParameterNames.Add(parameters[i].name);
                rearrangedArguments[i] = i;
                continue;
            }

            int? destinationIndex = null;

            for (var j = 0; j < parameters.Length; j++) {
                if (parameters[j].name == argumentName) {
                    if (!seenParameterNames.Add(argumentName)) {
                        _binder.diagnostics.Push(Error.ParameterAlreadySpecified(
                            arguments[i].identifier.location,
                            argumentName
                        ));

                        canContinue = false;
                    } else {
                        destinationIndex = j;
                    }

                    break;
                }
            }

            if (!canContinue)
                break;

            if (!destinationIndex.HasValue) {
                _binder.diagnostics.Push(Error.NoSuchParameter(
                    arguments[i].identifier.location, name,
                    arguments[i].identifier.text, overloadCount > 1
                ));

                canContinue = false;
            } else {
                rearrangedArguments[destinationIndex.Value] = i;
            }
        }

        return canContinue;
    }

    private int RearrangeArguments(
        ImmutableArray<ParameterSymbol> parameters,
        int score,
        int argumentCount,
        SeparatedSyntaxList<ArgumentSyntax> expressionArguments,
        Dictionary<int, int> rearrangedArguments,
        ImmutableArray<(string name, BoundExpression expression)> preBoundArguments,
        ArrayBuilder<BoundExpression> currentBoundArguments,
        bool isTemplate,
        ImmutableArray<TypeOrConstant>? templateArguments,
        BoundType receiverType) {
        for (var i = 0; i < preBoundArguments.Length; i++) {
            var argument = preBoundArguments[rearrangedArguments[i]];
            var parameter = parameters[i];
            // If this evaluates to null, it means that there was a default value automatically passed in
            var location = i >= argumentCount ? null : expressionArguments?[i]?.location;

            var argumentExpression = argument.expression;
            var isImplicitNull = false;

            if (argumentExpression.type.typeSymbol is null &&
                argumentExpression is BoundLiteralExpression le &&
                ConstantValue.IsNull(argumentExpression.constantValue) &&
                le.isArtificial) {
                argumentExpression = new BoundLiteralExpression(
                    null, BoundType.CopyWith(argumentExpression.type, typeSymbol: parameter.type.typeSymbol)
                );

                isImplicitNull = true;
            }

            var boundArgument = _binder.BindCast(
                location,
                argumentExpression,
                parameter.type,
                out var castType,
                argument: i + 1,
                isImplicitNull: isImplicitNull,
                isTemplate: isTemplate,
                receiverType: receiverType,
                templateArguments: templateArguments
            );

            if (castType.isAnyAdding)
                score += 3;
            if (castType.isImplicit && !castType.isIdentity)
                score += 2;
            if (castType.isNullAdding)
                score += 1;

            currentBoundArguments.Add(boundArgument);
        }

        return score;
    }

    private int UpdateScore<T>(
        int minScore,
        List<T> possibleOverloads,
        ArrayBuilder<BoundExpression> boundArguments,
        T overload,
        int beforeCount,
        int score,
        ArrayBuilder<BoundExpression> currentBoundArguments) where T : ISymbol {
        if (_binder.diagnostics.Count == beforeCount) {
            if (score < minScore) {
                boundArguments.Clear();
                boundArguments.AddRange(currentBoundArguments);
                minScore = score;
                possibleOverloads.Clear();
            } else if (score == minScore && currentBoundArguments.Count < boundArguments.Count) {
                boundArguments.Clear();
                boundArguments.AddRange(currentBoundArguments);
            }

            if (score == minScore)
                possibleOverloads.Add(overload);
        }

        return minScore;
    }

    private void CleanUpDiagnostics<T>(ImmutableArray<T> overloads, BelteDiagnosticQueue tempDiagnostics)
        where T : ISymbol {
        if (overloads.Length > 1) {
            _binder.diagnostics.Clear();
            _binder.diagnostics.Move(tempDiagnostics);
        } else if (overloads.Length == 1) {
            tempDiagnostics.Move(_binder.diagnostics);
            _binder.diagnostics.Move(tempDiagnostics);
        }
    }
}
