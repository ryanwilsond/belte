using System;
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

    /// <summary>
    /// Creates an <see cref="OverloadResolution" />, uses a Binders diagnostics.
    /// </summary>
    /// <param name="binder"><see cref="Binder" /> to use diagnostics from.</param>
    internal OverloadResolution(Binder binder) {
        _binder = binder;
    }

    /// <summary>
    /// Resolves a method invocation to find the best overload.
    /// </summary>
    /// <param name="methods">Available overloads.</param>
    /// <param name="arguments">Bound arguments.</param>
    /// <param name="expression">Original call expression, used purely for diagnostic locations.</param>
    /// <returns></returns>
    internal OverloadResolutionResult MethodInvocationOverloadResolution(
        ImmutableArray<Symbol> methods, ImmutableArray<(string name, BoundExpression expression)> arguments,
        CallExpressionSyntax expression) {
        var minScore = int.MaxValue;
        var possibleOverloads = new List<MethodSymbol>();
        var name = expression.identifier.identifier.text;

        var boundArguments = ImmutableArray.CreateBuilder<BoundExpression>();
        var preBoundArgumentsBuilder = ImmutableArray.CreateBuilder<(string name, BoundExpression expression)>();
        preBoundArgumentsBuilder.AddRange(arguments);

        var tempDiagnostics = new BelteDiagnosticQueue();
        tempDiagnostics.Move(_binder.diagnostics);

        foreach (var symbol in methods) {
            var beforeCount = _binder.diagnostics.count;
            var score = 0;
            var isInner = symbol.name.Contains(">g__");

            if (symbol is not MethodSymbol method) {
                _binder.diagnostics.Push(Error.CannotCallNonMethod(expression.identifier.location, name));
                return OverloadResolutionResult.Failed();
                ;
            }

            var defaultParameterCount = method.parameters.Where(p => p.defaultValue != null).ToArray().Length;

            if (expression.arguments.Count < method.parameters.Length - defaultParameterCount ||
                expression.arguments.Count > method.parameters.Length) {
                var count = 0;

                if (isInner) {
                    foreach (var parameter in method.parameters) {
                        if (parameter.name.StartsWith("$"))
                            count++;
                    }
                }

                if (!isInner || expression.arguments.Count + count != method.parameters.Length) {
                    TextSpan span;

                    if (expression.arguments.Count > method.parameters.Length) {
                        SyntaxNodeOrToken firstExceedingNode;

                        if (expression.arguments.Count > 1) {
                            firstExceedingNode = expression.arguments.GetSeparator(method.parameters.Length - 1);
                        } else {
                            firstExceedingNode = expression.arguments[0].kind == SyntaxKind.EmptyExpression
                                ? expression.arguments.GetSeparator(0)
                                : expression.arguments[0];
                        }

                        SyntaxNodeOrToken lastExceedingNode = expression.arguments.Last().kind == SyntaxKind.EmptyExpression
                            ? expression.arguments.GetSeparator(expression.arguments.Count - 2)
                            : expression.arguments.Last();

                        span = TextSpan.FromBounds(firstExceedingNode.span.start, lastExceedingNode.span.end);
                    } else {
                        span = expression.closeParenthesis.span;
                    }

                    var location = new TextLocation(expression.syntaxTree.text, span);
                    _binder.diagnostics.Push(Error.IncorrectArgumentCount(
                        location, method.name, method.parameters.Length,
                        defaultParameterCount, expression.arguments.Count
                    ));

                    continue;
                }
            }

            var rearrangedArguments = new Dictionary<int, int>();
            var seenParameterNames = new HashSet<string>();
            var canContinue = true;

            for (var i = 0; i < expression.arguments.Count; i++) {
                var argumentName = preBoundArgumentsBuilder[i].name;

                if (argumentName == null) {
                    seenParameterNames.Add(method.parameters[i].name);
                    rearrangedArguments[i] = i;
                    continue;
                }

                int? destinationIndex = null;

                for (var j = 0; j < method.parameters.Length; j++) {
                    if (method.parameters[j].name == argumentName) {
                        if (!seenParameterNames.Add(argumentName)) {
                            _binder.diagnostics.Push(
                                Error.ParameterAlreadySpecified(expression.arguments[i].name.location, argumentName)
                            );
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
                        expression.arguments[i].name.location, name,
                        expression.arguments[i].name.text, methods.Length > 1
                    ));

                    canContinue = false;
                } else {
                    rearrangedArguments[destinationIndex.Value] = i;
                }
            }

            for (var i = 0; i < method.parameters.Length; i++) {
                var parameter = method.parameters[i];

                if (!parameter.name.StartsWith('$') &&
                    seenParameterNames.Add(parameter.name) &&
                    parameter.defaultValue != null) {
                    rearrangedArguments[i] = preBoundArgumentsBuilder.Count;
                    preBoundArgumentsBuilder.Add((parameter.name, parameter.defaultValue));
                }
            }

            var preBoundArguments = preBoundArgumentsBuilder.ToImmutable();
            var currentBoundArguments = ImmutableArray.CreateBuilder<BoundExpression>();

            if (canContinue) {
                for (var i = 0; i < preBoundArguments.Length; i++) {
                    var argument = preBoundArguments[rearrangedArguments[i]];
                    var parameter = method.parameters[i];
                    // If this evaluates to null, it means that there was a default value automatically passed in
                    var location = i >= expression.arguments.Count ? null : expression.arguments[i].location;

                    var argumentExpression = argument.expression;
                    var isImplicitNull = false;

                    if (argument.expression.type.typeSymbol == null &&
                        argument.expression is BoundLiteralExpression le &&
                        BoundConstant.IsNull(argument.expression.constantValue) &&
                        le.isArtificial) {
                        argumentExpression = new BoundLiteralExpression(
                            null, BoundType.CopyWith(argument.expression.type, typeSymbol: parameter.type.typeSymbol)
                        );
                        isImplicitNull = true;
                    }

                    var boundArgument = _binder.BindCast(
                        location, argumentExpression, parameter.type, out var castType,
                        argument: i + 1, isImplicitNull: isImplicitNull
                    );

                    if (castType.isImplicit && !castType.isIdentity)
                        score++;

                    currentBoundArguments.Add(boundArgument);
                }

                if (isInner) {
                    for (var i = 0; i < method.parameters.Length; i++) {
                        var parameter = method.parameters[i];

                        if (!parameter.name.StartsWith('$'))
                            continue;

                        var argument = SyntaxFactory.Reference(parameter.name.Substring(1));
                        var boundArgument = _binder.BindCast(argument, parameter.type, argument: i);
                        currentBoundArguments.Add(boundArgument);
                    }
                }
            }

            if (methods.Length == 1 && _binder.diagnostics.Errors().Any()) {
                tempDiagnostics.Move(_binder.diagnostics);
                _binder.diagnostics.Move(tempDiagnostics);

                return OverloadResolutionResult.Failed();
                ;
            }

            if (_binder.diagnostics.count == beforeCount) {
                if (score < minScore) {
                    boundArguments.Clear();
                    boundArguments.AddRange(currentBoundArguments);
                    minScore = score;
                    possibleOverloads.Clear();
                }

                if (score == minScore) {
                    possibleOverloads.Add(method);
                }
            }
        }

        if (methods.Length > 1) {
            _binder.diagnostics.Clear();
            _binder.diagnostics.Move(tempDiagnostics);
        } else if (methods.Length == 1) {
            tempDiagnostics.Move(_binder.diagnostics);
            _binder.diagnostics.Move(tempDiagnostics);
        }

        if (methods.Length > 1 && possibleOverloads.Count == 0) {
            _binder.diagnostics.Push(Error.NoOverload(expression.identifier.location, name));

            return OverloadResolutionResult.Failed();
            ;
        } else if (methods.Length > 1 && possibleOverloads.Count > 1) {
            // Special case where there are default overloads
            if (possibleOverloads[0].name == "HasValue") {
                possibleOverloads.Clear();
                possibleOverloads.Add(BuiltinMethods.HasValueAny);
            } else if (possibleOverloads[0].name == "Value") {
                possibleOverloads.Clear();
                possibleOverloads.Add(BuiltinMethods.ValueAny);
            } else {
                _binder.diagnostics.Push(
                    Error.AmbiguousOverload(expression.identifier.location, possibleOverloads.ToArray())
                );

                return OverloadResolutionResult.Failed();
            }
        }

        return new OverloadResolutionResult(possibleOverloads.SingleOrDefault(), boundArguments.ToImmutable(), true);
    }
}
