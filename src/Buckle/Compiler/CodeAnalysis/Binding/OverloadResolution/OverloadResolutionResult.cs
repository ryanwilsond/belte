using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// The results of the <see cref="OverloadResolution" />. Describes if it succeeded, and which method was picked if it
/// succeeded.
/// </summary>
internal sealed class OverloadResolutionResult<TMember> where TMember : Symbol {
    private static readonly ObjectPool<OverloadResolutionResult<TMember>> Pool = CreatePool();

    private ThreeState _bestResultState;
    private MemberResolutionResult<TMember> _bestResult;

    internal readonly ArrayBuilder<MemberResolutionResult<TMember>> resultsBuilder;

    internal OverloadResolutionResult() {
        resultsBuilder = [];
    }

    internal bool succeeded {
        get {
            EnsureBestResultLoaded();
            return _bestResultState == ThreeState.True && _bestResult.result.isValid;
        }
    }

    internal MemberResolutionResult<TMember> bestResult {
        get {
            EnsureBestResultLoaded();
            return _bestResult;
        }
    }

    internal ImmutableArray<MemberResolutionResult<TMember>> results => resultsBuilder.ToImmutable();

    internal bool hasAnyApplicableMember {
        get {
            foreach (var result in resultsBuilder) {
                if (result.result.isApplicable)
                    return true;
            }

            return false;
        }
    }

    internal void Clear() {
        _bestResult = default;
        _bestResultState = ThreeState.Unknown;
        resultsBuilder.Clear();
    }

    internal void ReportDiagnostics<T>(
        Binder binder,
        TextLocation location,
        SyntaxNode node,
        BelteDiagnosticQueue diagnostics,
        string name,
        BoundExpression receiver,
        SyntaxNode invokedExpression,
        AnalyzedArguments arguments,
        ImmutableArray<T> memberGroup,
        NamedTypeSymbol typeContainingConstructor,
        bool isMethodGroupConversion = false,
        RefKind? returnRefKind = null)
        where T : Symbol {
        var symbols = StaticCast<Symbol>.From(memberGroup);

        if (HadAmbiguousBestMethods(diagnostics, symbols, location))
            return;

        if (HadAmbiguousWorseMethods(diagnostics, symbols, location))
            return;

        if (HadStaticInstanceMismatch(
            diagnostics,
            symbols,
            invokedExpression?.location ?? location,
            binder,
            receiver,
            node)) {
            return;
        }

        // if (isMethodGroupConversion && returnRefKind != null &&
        //     HadReturnMismatch(location, diagnostics, delegateOrFunctionPointerType)) {
        //     return;
        // }

        // if (HadConstraintFailure(location, diagnostics)) {
        //     return;
        // }

        if (HadBadArguments(
            diagnostics,
            binder,
            name,
            receiver,
            arguments,
            symbols,
            location,
            binder.flags,
            isMethodGroupConversion)) {
            return;
        }

        // if (HadConstructedParameterFailedConstraintCheck(binder.Conversions, binder.Compilation, diagnostics, location)) {
        //     return;
        // }

        // if (InaccessibleTypeArgument(diagnostics, symbols, location)) {
        //     return;
        // }

        // if (TypeInferenceFailed(binder, diagnostics, symbols, receiver, arguments, location, queryClause)) {
        //     return;
        // }

        var supportedRequiredParameterMissingConflicts = false;
        MemberResolutionResult<TMember> firstSupported = default;
        // MemberResolutionResult<TMember> firstUnsupported = default;

        var supportedInPriorityOrder = new MemberResolutionResult<TMember>[7];
        const int DuplicateNamedArgumentPriority = 0;
        const int RequiredParameterMissingPriority = 1;
        const int NameUsedForPositionalPriority = 2;
        const int NoCorrespondingNamedParameterPriority = 3;
        const int NoCorrespondingParameterPriority = 4;
        const int BadNonTrailingNamedArgumentPriority = 5;
        // const int WrongCallingConventionPriority = 6;

        foreach (var result in resultsBuilder) {
            switch (result.result.kind) {
                case MemberResolutionKind.NoCorrespondingNamedParameter:
                    if (supportedInPriorityOrder[NoCorrespondingNamedParameterPriority].isNull ||
                        result.result.firstBadArgument > supportedInPriorityOrder[NoCorrespondingNamedParameterPriority].result.firstBadArgument) {
                        supportedInPriorityOrder[NoCorrespondingNamedParameterPriority] = result;
                    }
                    break;
                case MemberResolutionKind.NoCorrespondingParameter:
                    if (supportedInPriorityOrder[NoCorrespondingParameterPriority].isNull) {
                        supportedInPriorityOrder[NoCorrespondingParameterPriority] = result;
                    }
                    break;
                case MemberResolutionKind.RequiredParameterMissing:
                    if (supportedInPriorityOrder[RequiredParameterMissingPriority].isNull) {
                        supportedInPriorityOrder[RequiredParameterMissingPriority] = result;
                    } else {
                        supportedRequiredParameterMissingConflicts = true;
                    }
                    break;
                case MemberResolutionKind.NameUsedForPositional:
                    if (supportedInPriorityOrder[NameUsedForPositionalPriority].isNull ||
                        result.result.firstBadArgument > supportedInPriorityOrder[NameUsedForPositionalPriority].result.firstBadArgument) {
                        supportedInPriorityOrder[NameUsedForPositionalPriority] = result;
                    }
                    break;
                case MemberResolutionKind.BadNonTrailingNamedArgument:
                    if (supportedInPriorityOrder[BadNonTrailingNamedArgumentPriority].isNull ||
                        result.result.firstBadArgument > supportedInPriorityOrder[BadNonTrailingNamedArgumentPriority].result.firstBadArgument) {
                        supportedInPriorityOrder[BadNonTrailingNamedArgumentPriority] = result;
                    }
                    break;
                case MemberResolutionKind.DuplicateNamedArgument:
                    if (supportedInPriorityOrder[DuplicateNamedArgumentPriority].isNull ||
                        result.result.firstBadArgument > supportedInPriorityOrder[DuplicateNamedArgumentPriority].result.firstBadArgument) {
                        supportedInPriorityOrder[DuplicateNamedArgumentPriority] = result;
                    }
                    break;
                // case MemberResolutionKind.WrongCallingConvention: {
                //         if (supportedInPriorityOrder[wrongCallingConventionPriority].IsNull) {
                //             supportedInPriorityOrder[wrongCallingConventionPriority] = result;
                //         }
                //     }
                //     break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(result.result.kind);
            }
        }

        foreach (var supported in supportedInPriorityOrder) {
            if (supported.isNotNull) {
                firstSupported = supported;
                break;
            }
        }

        if (firstSupported.isNotNull) {
            if (!(firstSupported.result.kind == MemberResolutionKind.RequiredParameterMissing &&
                supportedRequiredParameterMissingConflicts)
                && !isMethodGroupConversion) {
                switch (firstSupported.result.kind) {
                    case MemberResolutionKind.NameUsedForPositional:
                        ReportNameUsedForPositional(firstSupported, diagnostics, arguments, symbols);
                        return;
                    case MemberResolutionKind.NoCorrespondingNamedParameter:
                        ReportNoCorrespondingNamedParameter(firstSupported, name, diagnostics, arguments, symbols);
                        return;
                    case MemberResolutionKind.RequiredParameterMissing:
                        ReportMissingRequiredParameter(firstSupported, diagnostics, symbols, location);
                        return;
                    case MemberResolutionKind.NoCorrespondingParameter:
                        break;
                    case MemberResolutionKind.BadNonTrailingNamedArgument:
                        ReportBadNonTrailingNamedArgument(firstSupported, diagnostics, arguments, symbols);
                        return;
                    case MemberResolutionKind.DuplicateNamedArgument:
                        ReportDuplicateNamedArgument(firstSupported, diagnostics, arguments);
                        return;
                }
            }
            // else if (firstSupported.result.kind == MemberResolutionKind.WrongCallingConvention) {
            //     ReportWrongCallingConvention(location, diagnostics, symbols, firstSupported, ((FunctionPointerTypeSymbol)delegateOrFunctionPointerType).Signature);
            //     return;
            // }
        }

        if (!isMethodGroupConversion)
            ReportBadParameterCount(diagnostics, name, arguments, symbols, location, typeContainingConstructor);
    }

    private static void ReportBadParameterCount(
        BelteDiagnosticQueue diagnostics,
        string name,
        AnalyzedArguments arguments,
        ImmutableArray<Symbol> symbols,
        TextLocation location,
        NamedTypeSymbol typeContainingConstructor) {
        var argCount = arguments.arguments.Count;

        if (typeContainingConstructor is not null)
            diagnostics.Push(Error.WrongConstructorArgumentCount(location, name, argCount));
        else
            diagnostics.Push(Error.WrongArgumentCount(location, name, argCount));
    }

    private static void ReportDuplicateNamedArgument(
        MemberResolutionResult<TMember> bad,
        BelteDiagnosticQueue diagnostics,
        AnalyzedArguments arguments) {
        var badArg = bad.result.firstBadArgument;
        (var name, var location) = arguments.names[badArg].GetValueOrDefault();
        diagnostics.Push(Error.NamedArgumentTwice(location, name));
    }

    private static void ReportBadNonTrailingNamedArgument(
        MemberResolutionResult<TMember> bad,
        BelteDiagnosticQueue diagnostics,
        AnalyzedArguments arguments,
        ImmutableArray<Symbol> symbols) {
        var badArg = bad.result.firstBadArgument;
        (var badName, var location) = arguments.names[badArg].GetValueOrDefault();
        diagnostics.Push(Error.BadNonTrailingNamedArgument(location, badName));
    }

    private static void ReportMissingRequiredParameter(
        MemberResolutionResult<TMember> bad,
        BelteDiagnosticQueue diagnostics,
        ImmutableArray<Symbol> symbols,
        TextLocation location) {
        var badMember = bad.member;
        var parameters = badMember.GetParameters();
        var badParamIndex = bad.result.badParameter;
        var badParamName = parameters[badParamIndex].name;
        diagnostics.Push(Error.NoCorrespondingArgument(location, badParamName, badMember));
    }

    private static void ReportNameUsedForPositional(
        MemberResolutionResult<TMember> bad,
        BelteDiagnosticQueue diagnostics,
        AnalyzedArguments arguments,
        ImmutableArray<Symbol> symbols) {
        var badArg = bad.result.firstBadArgument;
        (var badName, var location) = arguments.names[badArg].GetValueOrDefault();
        diagnostics.Push(Error.NamedArgumentUsedInPositional(location, badName));
    }

    private static void ReportNoCorrespondingNamedParameter(
        MemberResolutionResult<TMember> bad,
        string methodName,
        BelteDiagnosticQueue diagnostics,
        AnalyzedArguments arguments,
        ImmutableArray<Symbol> symbols) {
        var badArg = bad.result.firstBadArgument;
        (var badName, var location) = arguments.names[badArg].GetValueOrDefault();
        diagnostics.Push(Error.BadArgumentName(location, methodName, badName));
    }

    private bool HadBadArguments(
        BelteDiagnosticQueue diagnostics,
        Binder binder,
        string name,
        BoundExpression receiver,
        AnalyzedArguments arguments,
        ImmutableArray<Symbol> symbols,
        TextLocation location,
        BinderFlags flags,
        bool isMethodGroupConversion) {
        var badArg = GetFirstMemberKind(MemberResolutionKind.BadArgumentConversion);

        if (badArg.isNull)
            return false;

        if (isMethodGroupConversion)
            return true;

        var method = badArg.member;

        foreach (var arg in badArg.result.badArguments.TrueBits())
            ReportBadArgumentError(diagnostics, binder, name, arguments, symbols, badArg, method, arg);

        return true;
    }

    private static void ReportBadArgumentError(
        BelteDiagnosticQueue diagnostics,
        Binder binder,
        string name,
        AnalyzedArguments arguments,
        ImmutableArray<Symbol> symbols,
        MemberResolutionResult<TMember> badArg,
        TMember method,
        int arg) {
        var argument = arguments.Argument(arg);

        if (argument.hasErrors)
            return;

        var parm = badArg.result.ParameterFromArgument(arg);
        var sourceLocation = argument.syntax.location;

        var parameter = method.GetParameters()[parm];
        var isLastParameter = method.GetParameterCount() == parm + 1;
        var refArg = arguments.RefKind(arg);
        var refParameter = parameter.refKind;

        if (!argument.HasExpressionType()) {
            // TODO Do we need to inject "<null>" here in place of argument.type?
            diagnostics.Push(Error.CannotConvertArgument(sourceLocation, argument.type, parameter.type, arg + 1));
        } else if (refArg != refParameter &&
              !(refParameter == RefKind.RefConst && refArg is RefKind.None or RefKind.Ref)) {
            if (refParameter is RefKind.None or RefKind.RefConst)
                diagnostics.Push(Error.ArgumentExtraRef(sourceLocation, "ref", arg + 1));
            else
                diagnostics.Push(Error.ArgumentWrongRef(sourceLocation, "ref", arg + 1));
        } else {
            if (argument.type is { } argType) {
                diagnostics.Push(Error.CannotConvertArgument(sourceLocation, argType, parameter.type, arg + 1));
            } else {
                // TODO Reachable error?
                // diagnostics.Add(
                //     ErrorCode.ERR_BadArgType,
                //     sourceLocation,
                //     symbols,
                //     arg + 1,
                //     argument.Display,
                //     new FormattedSymbol(unwrapIfParamsCollection(badArg, parameter, isLastParameter), SymbolDisplayFormat.CSharpErrorMessageNoParameterNamesFormat));
            }
        }
    }

    private bool HadStaticInstanceMismatch(
        BelteDiagnosticQueue diagnostics,
        ImmutableArray<Symbol> symbols,
        TextLocation location,
        Binder binder,
        BoundExpression receiver,
        SyntaxNode node) {
        var staticInstanceMismatch = GetFirstMemberKind(MemberResolutionKind.StaticInstanceMismatch);

        if (staticInstanceMismatch.isNull)
            return false;

        if (receiver?.hasErrors != true) {
            Symbol symbol = staticInstanceMismatch.member;

            if (symbol.RequiresInstanceReceiver()) {
                if (Binder.WasImplicitReceiver(receiver) && binder.inFieldInitializer)
                    diagnostics.Push(Error.InstanceRequiredInFieldInitializer(location, symbol));
                else
                    diagnostics.Push(Error.InstanceRequired(location, symbol));
            } else {
                diagnostics.Push(Error.NoInstanceRequired(location, symbol));
            }
        }

        return true;
    }

    private MemberResolutionResult<TMember> GetFirstMemberKind(MemberResolutionKind kind) {
        foreach (var result in resultsBuilder) {
            if (result.result.kind == kind)
                return result;
        }

        return default;
    }

    private bool HadAmbiguousBestMethods(
        BelteDiagnosticQueue diagnostics,
        ImmutableArray<Symbol> symbols,
        TextLocation location) {
        var nValid = TryGetFirstTwoValidResults(out var validResult1, out var validResult2);

        if (nValid <= 1)
            return false;

        diagnostics.Push(Error.AmbiguousMethodOverload(
            location,
            [(MethodSymbol)validResult1.leastOverriddenMember.originalDefinition,
            (MethodSymbol)validResult2.leastOverriddenMember.originalDefinition]
        ));

        return true;
    }

    private bool HadAmbiguousWorseMethods(
        BelteDiagnosticQueue diagnostics,
        ImmutableArray<Symbol> symbols,
        TextLocation location) {
        var nWorse = TryGetFirstTwoWorseResults(out var worseResult1, out var worseResult2);

        if (nWorse <= 1)
            return false;

        diagnostics.Push(Error.AmbiguousMethodOverload(
            location,
            [(MethodSymbol)worseResult1.leastOverriddenMember.originalDefinition,
            (MethodSymbol)worseResult2.leastOverriddenMember.originalDefinition]
        ));

        return true;
    }

    private int TryGetFirstTwoWorseResults(
        out MemberResolutionResult<TMember> first,
        out MemberResolutionResult<TMember> second) {
        var count = 0;
        var foundFirst = false;
        var foundSecond = false;
        first = default;
        second = default;

        foreach (var res in resultsBuilder) {
            if (res.result.kind == MemberResolutionKind.Worse) {
                count++;

                if (!foundFirst) {
                    first = res;
                    foundFirst = true;
                } else if (!foundSecond) {
                    second = res;
                    foundSecond = true;
                }
            }
        }

        return count;
    }

    private int TryGetFirstTwoValidResults(
        out MemberResolutionResult<TMember> first,
        out MemberResolutionResult<TMember> second) {
        var count = 0;
        var foundFirst = false;
        var foundSecond = false;
        first = default;
        second = default;

        foreach (var res in resultsBuilder) {
            if (res.result.isValid) {
                count++;

                if (!foundFirst) {
                    first = res;
                    foundFirst = true;
                } else if (!foundSecond) {
                    second = res;
                    foundSecond = true;
                }
            }
        }

        return count;
    }

    internal static OverloadResolutionResult<TMember> GetInstance() {
        return Pool.Allocate();
    }

    internal void Free() {
        Clear();
        Pool.Free(this);
    }

    private static ObjectPool<OverloadResolutionResult<TMember>> CreatePool() {
        ObjectPool<OverloadResolutionResult<TMember>> pool = null;
        pool = new ObjectPool<OverloadResolutionResult<TMember>>(() => new OverloadResolutionResult<TMember>(), 10);
        return pool;
    }

    private void EnsureBestResultLoaded() {
        if (!_bestResultState.HasValue())
            _bestResultState = TryGetBestResult(resultsBuilder, out _bestResult);
    }

    private static ThreeState TryGetBestResult(
        ArrayBuilder<MemberResolutionResult<TMember>> allResults,
        out MemberResolutionResult<TMember> best) {
        best = default;
        var haveBest = ThreeState.False;

        foreach (var pair in allResults) {
            if (pair.result.isValid) {
                if (haveBest == ThreeState.True) {
                    best = default;
                    return ThreeState.False;
                }

                haveBest = ThreeState.True;
                best = pair;
            }
        }

        return haveBest;
    }
}
