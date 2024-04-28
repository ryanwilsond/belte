using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Resolves overloads to find the best overload.
/// </summary>
internal sealed class OverloadResolution {
    private readonly Binder _binder;

    private bool _suppressDiagnostics = false;

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
        _suppressDiagnostics = true;
        var result = MethodOverloadResolution(methods, arguments, name, operand, argumentList, receiverType);
        _suppressDiagnostics = false;

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
        BoundType receiverType) {
        var minScore = int.MaxValue;
        var possibleOverloads = new List<MethodSymbol>();

        var boundArguments = ImmutableArray.CreateBuilder<BoundExpression>();

        var tempDiagnostics = new BelteDiagnosticQueue();
        tempDiagnostics.Move(_binder.diagnostics);

        var isConstructor = false;

        foreach (var method in methods) {
            var preBoundArgumentsBuilder = ImmutableArray.CreateBuilder<(string name, BoundExpression expression)>();
            preBoundArgumentsBuilder.AddRange(arguments);

            var beforeCount = _binder.diagnostics.Count;
            var score = 0;
            var isInner = method.name.Contains(">g__");

            isConstructor = method.name == WellKnownMemberNames.InstanceConstructorName;

            var defaultParameterCount = method.parameters.Where(p => p.defaultValue != null).Count();
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
                    parameter.defaultValue != null) {
                    rearrangedArguments[i] = preBoundArgumentsBuilder.Count;
                    preBoundArgumentsBuilder.Add((parameter.name, parameter.defaultValue));
                }
            }

            var currentBoundArguments = ImmutableArray.CreateBuilder<BoundExpression>();

            if (canContinue) {
                score = RearrangeArguments(
                    method.parameters,
                    score,
                    expressionArgumentsCount,
                    expressionArguments,
                    rearrangedArguments,
                    preBoundArgumentsBuilder.ToImmutable(),
                    currentBoundArguments,
                    false,
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
                            receiverType: receiverType
                        );

                        currentBoundArguments.Add(boundArgument);
                    }
                }
            }

            if (methods.Length == 1 && _binder.diagnostics.Errors().Any()) {
                tempDiagnostics.Move(_binder.diagnostics);
                _binder.diagnostics.Move(tempDiagnostics);
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
        }

        CleanUpDiagnostics(methods, tempDiagnostics);

        if (methods.Length > 1 && possibleOverloads.Count == 0) {
            if (isConstructor) {
                if (!_suppressDiagnostics)
                    _binder.diagnostics.Push(Error.NoConstructorOverload(operand.location, methods[0].containingType.name));
            } else {
                if (!_suppressDiagnostics)
                    _binder.diagnostics.Push(Error.NoMethodOverload(operand.location, name));
            }

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
                var minArguments = int.MaxValue;
                var tempPossibleOverloads = new List<MethodSymbol>();

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
                    _binder.diagnostics.Push(Error.AmbiguousMethodOverload(operand.location, possibleOverloads.ToArray()));
                    return OverloadResolutionResult<MethodSymbol>.Ambiguous();
                }
            }
        } else if (methods.Length == 1 && possibleOverloads.Count == 0) {
            possibleOverloads.Add(methods[0]);
        }

        return OverloadResolutionResult<MethodSymbol>.Succeeded(
            possibleOverloads.SingleOrDefault(),
            boundArguments.ToImmutable()
        );
    }

    /// <summary>
    /// Uses partially bound arguments to find the best template overload and resolves it.
    /// </summary>
    /// <param name="types">Available overloads.</param>
    /// <param name="arguments">Bound arguments.</param>
    /// <param name="name">The name of the template.</param>
    /// <param name="operand">Original expression operand, used for diagnostic locations.</param>
    /// <param name="argumentList">The original arguments, used for calculations.</param>
    internal OverloadResolutionResult<NamedTypeSymbol> TemplateOverloadResolution(
        ImmutableArray<NamedTypeSymbol> types,
        ImmutableArray<(string name, BoundTypeOrConstant constant)> arguments,
        string name,
        SyntaxNodeOrToken operand,
        TemplateArgumentListSyntax argumentList) {
        var minScore = int.MaxValue;
        var possibleOverloads = new List<NamedTypeSymbol>();

        var boundArguments = ImmutableArray.CreateBuilder<BoundExpression>();
        var preBoundArgumentsBuilder = ImmutableArray.CreateBuilder<(string name, BoundExpression expression)>();

        foreach (var argument in arguments) {
            if (argument.constant.isConstant) {
                var expression = argument.constant.constant is null
                    ? argument.constant.expression
                    : new BoundLiteralExpression(argument.constant.constant?.value);

                preBoundArgumentsBuilder.Add((argument.name, expression));
            } else {
                preBoundArgumentsBuilder.Add((argument.name, argument.constant.type));
            }
        }

        var tempDiagnostics = new BelteDiagnosticQueue();
        tempDiagnostics.Move(_binder.diagnostics);

        foreach (var type in types) {
            var beforeCount = _binder.diagnostics.Count;
            var score = 0;

            var defaultParameterCount = type.templateParameters.Where(p => p.defaultValue != null).Count();
            var expressionArguments = argumentList?.arguments;
            var argumentCount = expressionArguments?.Count ?? 0;

            if (argumentCount < type.templateParameters.Length - defaultParameterCount ||
                argumentCount > type.templateParameters.Length) {
                if (argumentCount != type.templateParameters.Length) {
                    ResolveIncorrectArgumentCount(
                        operand,
                        argumentList?.closeAngleBracket?.span,
                        type.name,
                        type.templateParameters,
                        defaultParameterCount,
                        expressionArguments?.Count ?? 0,
                        expressionArguments,
                        true
                    );

                    continue;
                }
            }

            var canContinue = CalculateArgumentRearrangements(
                types.Length,
                name,
                preBoundArgumentsBuilder,
                type.templateParameters,
                expressionArguments?.Count ?? 0,
                expressionArguments,
                out var rearrangedArguments,
                out var seenParameterNames
            );

            for (var i = 0; i < type.templateParameters.Length; i++) {
                var parameter = type.templateParameters[i];

                if (!parameter.name.StartsWith('$') &&
                    seenParameterNames.Add(parameter.name) &&
                    parameter.defaultValue != null) {
                    rearrangedArguments[i] = preBoundArgumentsBuilder.Count;
                    preBoundArgumentsBuilder.Add((parameter.name, parameter.defaultValue));
                }
            }

            var currentBoundArguments = ImmutableArray.CreateBuilder<BoundExpression>();

            if (canContinue) {
                score = RearrangeArguments(
                    type.templateParameters,
                    score,
                    expressionArguments?.Count ?? 0,
                    expressionArguments,
                    rearrangedArguments,
                    preBoundArgumentsBuilder.ToImmutable(),
                    currentBoundArguments,
                    true,
                    null
                );
            }

            if (types.Length == 1 && _binder.diagnostics.Errors().Any()) {
                tempDiagnostics.Move(_binder.diagnostics);
                _binder.diagnostics.Move(tempDiagnostics);
                return OverloadResolutionResult<NamedTypeSymbol>.Failed();
            }

            minScore = UpdateScore(
                minScore,
                possibleOverloads,
                boundArguments,
                type,
                beforeCount,
                score,
                currentBoundArguments
            );
        }

        CleanUpDiagnostics(types, tempDiagnostics);

        if (types.Length > 1 && possibleOverloads.Count == 0) {
            if (!_suppressDiagnostics)
                _binder.diagnostics.Push(Error.NoTemplateOverload(operand.location, name));

            return OverloadResolutionResult<NamedTypeSymbol>.Failed();
        } else if (types.Length > 1 && possibleOverloads.Count > 1) {
            _binder.diagnostics.Push(Error.AmbiguousTemplateOverload(operand.location, possibleOverloads.ToArray()));
            return OverloadResolutionResult<NamedTypeSymbol>.Ambiguous();
        } else if (types.Length == 1 && possibleOverloads.Count == 0) {
            possibleOverloads.Add(types[0]);
        }

        return OverloadResolutionResult<NamedTypeSymbol>.Succeeded(
            possibleOverloads.SingleOrDefault(),
            boundArguments.ToImmutable()
        );
    }

    private bool CalculateArgumentRearrangements(
        int overloadCount,
        string name,
        ImmutableArray<(string name, BoundExpression expression)>.Builder preBoundArgumentsBuilder,
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
                        if (!_suppressDiagnostics) {
                            _binder.diagnostics.Push(
                                Error.ParameterAlreadySpecified(
                                    arguments[i].identifier.location,
                                    argumentName
                                )
                            );
                        }

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
                if (!_suppressDiagnostics) {
                    _binder.diagnostics.Push(Error.NoSuchParameter(
                        arguments[i].identifier.location, name,
                        arguments[i].identifier.text, overloadCount > 1
                    ));
                }

                canContinue = false;
            } else {
                rearrangedArguments[destinationIndex.Value] = i;
            }
        }

        return canContinue;
    }

    private void ResolveIncorrectArgumentCount(
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

        if (!_suppressDiagnostics) {
            _binder.diagnostics.Push(Error.IncorrectArgumentCount(
                location,
                name,
                parameters.Length,
                defaultParameterCount,
                argumentCount,
                isTemplate
            ));
        }
    }

    private int RearrangeArguments(
        ImmutableArray<ParameterSymbol> parameters,
        int score,
        int argumentCount,
        SeparatedSyntaxList<ArgumentSyntax> expressionArguments,
        Dictionary<int, int> rearrangedArguments,
        ImmutableArray<(string name, BoundExpression expression)> preBoundArguments,
        ImmutableArray<BoundExpression>.Builder currentBoundArguments,
        bool isTemplate,
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
                BoundConstant.IsNull(argumentExpression.constantValue) &&
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
                receiverType: receiverType
            );

            if (castType.isImplicit && !castType.isIdentity)
                score++;

            currentBoundArguments.Add(boundArgument);
        }

        return score;
    }

    private int UpdateScore<T>(
        int minScore,
        List<T> possibleOverloads,
        ImmutableArray<BoundExpression>.Builder boundArguments,
        T overload,
        int beforeCount,
        int score,
        ImmutableArray<BoundExpression>.Builder currentBoundArguments) where T : Symbol {
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
        where T : Symbol {
        if (overloads.Length > 1) {
            _binder.diagnostics.Clear();
            _binder.diagnostics.Move(tempDiagnostics);
        } else if (overloads.Length == 1) {
            tempDiagnostics.Move(_binder.diagnostics);
            _binder.diagnostics.Move(tempDiagnostics);
        }
    }
}
