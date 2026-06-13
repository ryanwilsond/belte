using System;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal partial class Binder {
    internal BoundStatement BindStatement(StatementSyntax node, BelteDiagnosticQueue diagnostics) {
        return node.kind switch {
            SyntaxKind.BlockStatement => BindBlockStatement((BlockStatementSyntax)node, diagnostics),
            SyntaxKind.ReturnStatement => BindReturnStatement((ReturnStatementSyntax)node, diagnostics),
            SyntaxKind.ExpressionStatement => BindExpressionStatement((ExpressionStatementSyntax)node, diagnostics),
            SyntaxKind.DeferStatement => BindDeferStatement((DeferStatementSyntax)node, diagnostics),
            SyntaxKind.ScopedStatement => BindScopedStatement((ScopedStatementSyntax)node, diagnostics),
            SyntaxKind.LocalDeclarationStatement => BindLocalDeclarationStatement((LocalDeclarationStatementSyntax)node, diagnostics),
            SyntaxKind.EmptyStatement => BindEmptyStatement((EmptyStatementSyntax)node, diagnostics),
            SyntaxKind.LocalFunctionStatement => BindLocalFunctionStatement((LocalFunctionStatementSyntax)node, diagnostics),
            SyntaxKind.IfStatement => BindIfStatement((IfStatementSyntax)node, diagnostics),
            SyntaxKind.NullBindingStatement => BindNullBindingStatement((NullBindingStatementSyntax)node, diagnostics),
            SyntaxKind.WhileStatement => BindWhileStatement((WhileStatementSyntax)node, diagnostics),
            SyntaxKind.DoWhileStatement => BindDoWhileStatement((DoWhileStatementSyntax)node, diagnostics),
            SyntaxKind.ForStatement => BindForStatement((ForStatementSyntax)node, diagnostics),
            SyntaxKind.ForEachStatement => BindForEachStatement((ForEachStatementSyntax)node, diagnostics),
            SyntaxKind.BreakStatement => BindBreakStatement((BreakStatementSyntax)node, diagnostics),
            SyntaxKind.ContinueStatement => BindContinueStatement((ContinueStatementSyntax)node, diagnostics),
            SyntaxKind.CommitStatement => BindCommitStatement((CommitStatementSyntax)node, diagnostics),
            SyntaxKind.TryStatement => BindTryStatement((TryStatementSyntax)node, diagnostics),
            SyntaxKind.SwitchStatement => BindSwitchStatement((SwitchStatementSyntax)node, diagnostics),
            SyntaxKind.GotoStatement => BindGotoStatement((GotoStatementSyntax)node, diagnostics),
            SyntaxKind.InlineILStatement => BindInlineILStatement((InlineILStatementSyntax)node, diagnostics),
            SyntaxKind.WithStatement => BindWithStatement((WithStatementSyntax)node, diagnostics),
            SyntaxKind.UnreachableStatement => BindUnreachableStatement((UnreachableStatementSyntax)node),
            SyntaxKind.ReverseStatement => BindReverseStatement((ReverseStatementSyntax)node, diagnostics),
            SyntaxKind.ReverseDeferStatement => BindReverseDeferStatement((ReverseDeferStatementSyntax)node, diagnostics),
            _ => throw ExceptionUtilities.UnexpectedValue(node.kind),
        };
    }

    private BoundStatement BindReverseStatement(ReverseStatementSyntax node, BelteDiagnosticQueue diagnostics) {
        var name = node.identifier.valueText;
        var token = LookupTokenByName(name);

        if (token is null)
            diagnostics.Push(Error.UndefinedToken(node.identifier.location, name));

        return new BoundReverseStatement(node, token);
    }

    private BoundStatement BindReverseDeferStatement(
        ReverseDeferStatementSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var hasError = BindReverseExpression(
            node.expression,
            diagnostics,
            out _,
            out var conversion,
            out var call
        );

        if (hasError) {
            diagnostics.Push(Error.ReverseDeferExpressionNotReversible(node.expression.location));
            return new BoundReverseDeferStatement(node, null, conversion, true);
        }

        return new BoundReverseDeferStatement(node, call, conversion);
    }

    private BoundStatement BindSwitchStatement(SwitchStatementSyntax node, BelteDiagnosticQueue diagnostics) {
        var switchBinder = GetBinder(node);
        return switchBinder.BindSwitchStatementCore(node, switchBinder, diagnostics);
    }

    internal virtual BoundStatement BindSwitchStatementCore(
        SwitchStatementSyntax node,
        Binder originalBinder,
        BelteDiagnosticQueue diagnostics) {
        return next.BindSwitchStatementCore(node, originalBinder, diagnostics);
    }

    private BoundStatement BindGotoStatement(GotoStatementSyntax node, BelteDiagnosticQueue diagnostics) {
        switch (node.caseOrDefaultKeyword.kind) {
            case SyntaxKind.CaseKeyword:
            case SyntaxKind.DefaultKeyword:
                var binder = GetSwitchBinder(this);

                if (binder is null) {
                    diagnostics.Push(Error.InvalidGotoCase(node.location));
                    ImmutableArray<BoundNode> childNodes;

                    if (node.value is not null) {
                        var value = BindRValueWithoutTargetType(node.value, BelteDiagnosticQueue.Discarded);
                        childNodes = ImmutableArray.Create<BoundNode>(value);
                    } else {
                        childNodes = ImmutableArray<BoundNode>.Empty;
                    }

                    return new BoundErrorStatement(node, childNodes, true);
                }

                return binder.BindGotoCaseOrDefault(node, this, diagnostics);
            default:
                throw ExceptionUtilities.UnexpectedValue(node.kind);
        }
    }

    private static SwitchBinder GetSwitchBinder(Binder binder) {
        var switchBinder = binder as SwitchBinder;

        while (binder is not null && switchBinder is null) {
            binder = binder.next;
            switchBinder = binder as SwitchBinder;
        }

        return switchBinder;
    }

    internal BoundExpression ConvertPatternExpression(
        TypeSymbol inputType,
        BelteSyntaxNode node,
        BoundExpression expression,
        out ConstantValue constantValue,
        bool hasErrors,
        BelteDiagnosticQueue diagnostics,
        out Conversion patternExpressionConversion) {
        BoundExpression convertedExpression;
        // TODO We currently only use this for string conversions, but patterns will be added later

        if (inputType.ContainsTemplateParameter()) {
            // convertedExpression = expression;

            // if (!hasErrors && expression.constantValue is not null) {
            //     if (expression.constantValue == ConstantValue.Null) {
            //         if (!inputType.IsNullableType() && !inputType.IsPointerOrFunctionPointer()) {
            //             diagnostics.Push(Error.ValueCannotBeNull(expression.syntax.location, inputType));
            //             hasErrors = true;
            //         }
            //     } else {
            //         ConstantValue match = ExpressionOfTypeMatchesPatternType(Conversions, inputType, expression.Type, ref useSiteInfo, out _, operandConstantValue: null);
            //         if (match == ConstantValue.False || match == ConstantValue.Bad) {
            //             diagnostics.Add(ErrorCode.ERR_PatternWrongType, expression.Syntax.Location, inputType, expression.Display);
            //             hasErrors = true;
            //         }
            //     }

            //     if (!hasErrors) {
            //         var requiredVersion = MessageID.IDS_FeatureRecursivePatterns.RequiredVersion();
            //         patternExpressionConversion = this.Conversions.ClassifyConversionFromExpression(expression, inputType, isChecked: CheckOverflowAtRuntime, ref useSiteInfo);
            //         if (Compilation.LanguageVersion < requiredVersion && !patternExpressionConversion.IsImplicit) {
            //             diagnostics.Add(ErrorCode.ERR_ConstantPatternVsOpenType,
            //                 expression.Syntax.Location, inputType, expression.Display, new CSharpRequiredLanguageVersion(requiredVersion));
            //         }
            //     } else {
            //         patternExpressionConversion = Conversion.None;
            //     }
            // } else {
            //     patternExpressionConversion = Conversion.None;
            // }
            throw ExceptionUtilities.Unreachable();
        } else {
            convertedExpression = GenerateConversionForAssignment(
                inputType,
                expression,
                diagnostics,
                out patternExpressionConversion
            );

            if (convertedExpression.kind == BoundKind.CastExpression) {
                var conversion = (BoundCastExpression)convertedExpression;
                var operand = conversion.operand;

                if (inputType.IsNullableType() && (convertedExpression.constantValue is null ||
                    !ConstantValue.IsNull(convertedExpression.constantValue))) {
                    convertedExpression = CreateConversion(
                        operand,
                        inputType.GetNullableUnderlyingType(),
                        BelteDiagnosticQueue.Discarded
                    );
                } else if ((conversion.conversion.kind == ConversionKind.AnyBoxing ||
                    conversion.conversion.kind == ConversionKind.ImplicitReference)
                      && operand.constantValue is not null && convertedExpression.constantValue is null) {
                    convertedExpression = operand;
                } else if (conversion.conversion.kind == ConversionKind.ImplicitNullToPointer ||
                      (conversion.conversion.kind == ConversionKind.None &&
                      convertedExpression.type?.IsErrorType() == true)) {
                    convertedExpression = operand;
                }
            }
        }

        constantValue = convertedExpression.constantValue;
        return convertedExpression;
    }

    internal BoundStatement BindPossibleEmbeddedStatement(StatementSyntax node, BelteDiagnosticQueue diagnostics) {
        Binder binder;

        switch (node.kind) {
            case SyntaxKind.DeferStatement:
                diagnostics.Push(Error.BadEmbeddedStatementDefer(node.location));
                goto case SyntaxKind.ExpressionStatement;
            case SyntaxKind.LocalDeclarationStatement:
                diagnostics.Push(Error.BadEmbeddedStatement(node.location));
                goto case SyntaxKind.ExpressionStatement;
            case SyntaxKind.ExpressionStatement:
            case SyntaxKind.IfStatement:
            case SyntaxKind.NullBindingStatement:
            case SyntaxKind.WithStatement:
            case SyntaxKind.ReturnStatement:
                binder = GetBinder(node);
                return binder.WrapWithVariablesIfAny(node, binder.BindStatement(node, diagnostics));
            case SyntaxKind.LocalFunctionStatement:
                diagnostics.Push(Error.BadEmbeddedStatement(node.location));
                binder = GetBinder(node);
                return binder.WrapWithVariablesAndLocalFunctionsIfAny(node, binder.BindStatement(node, diagnostics));
            case SyntaxKind.EmptyStatement:
                var emptyStatement = (EmptyStatementSyntax)node;

                if (!emptyStatement.semicolon.isFabricated) {
                    switch (node.parent.kind) {
                        case SyntaxKind.ForStatement:
                        case SyntaxKind.WhileStatement:
                            if (emptyStatement.semicolon.GetNextToken()?.kind != SyntaxKind.OpenBraceToken)
                                break;

                            goto default;
                        default:
                            diagnostics.Push(Warning.PossibleMistakenEmptyStatement(node.location));
                            break;
                    }
                }

                goto default;
            default:
                return BindStatement(node, diagnostics);
        }
    }

    private BoundIfStatement BindIfStatement(IfStatementSyntax node, BelteDiagnosticQueue diagnostics) {
        var condition = BindBooleanExpression(node.expression, diagnostics);
        var consequence = BindPossibleEmbeddedStatement(node.then, diagnostics);
        var alternative = (node.elseClause is null)
            ? null
            : BindPossibleEmbeddedStatement(node.elseClause.body, diagnostics);

        return new BoundIfStatement(node, condition, consequence, alternative);
    }

    private BoundWithStatement BindWithStatement(WithStatementSyntax node, BelteDiagnosticQueue diagnostics) {
        var assignments = BindWithExpressionList(node.expressions, diagnostics, out var hasErrors);
        var wrapWithTry = node.tryKeyword is not null;
        var binder = GetBinder(node);
        var body = binder.BindPossibleEmbeddedStatement(node.body, diagnostics);
        return new BoundWithStatement(node, assignments, body, wrapWithTry, binder.commitLocal, hasErrors);
    }

    private BoundNullBindingStatement BindNullBindingStatement(
        NullBindingStatementSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var binder = GetBinder(node);
        return binder.BindNullBindingParts(diagnostics, binder);
    }

    internal virtual BoundNullBindingStatement BindNullBindingParts(
        BelteDiagnosticQueue diagnostics,
        Binder originalBinder) {
        return next.BindNullBindingParts(diagnostics, originalBinder);
    }

    private BoundWhileStatement BindWhileStatement(WhileStatementSyntax node, BelteDiagnosticQueue diagnostics) {
        var loopBinder = GetBinder(node);
        return loopBinder.BindWhileParts(diagnostics, loopBinder);
    }

    private BoundDoWhileStatement BindDoWhileStatement(DoWhileStatementSyntax node, BelteDiagnosticQueue diagnostics) {
        var loopBinder = GetBinder(node);
        return loopBinder.BindDoWhileParts(diagnostics, loopBinder);
    }

    private BoundForStatement BindForStatement(ForStatementSyntax node, BelteDiagnosticQueue diagnostics) {
        var loopBinder = GetBinder(node);
        return loopBinder.BindForParts(diagnostics, loopBinder);
    }

    private BoundStatement BindForEachStatement(ForEachStatementSyntax node, BelteDiagnosticQueue diagnostics) {
        var loopBinder = GetBinder(node);
        return GetBinder(node.expression)
            .WrapWithVariablesIfAny(node.expression, loopBinder.BindForEachParts(diagnostics, loopBinder));
    }

    internal virtual BoundForEachStatement BindForEachParts(BelteDiagnosticQueue diagnostics, Binder originalBinder) {
        return next.BindForEachParts(diagnostics, originalBinder);
    }

    internal virtual BoundStatement BindForEachDeconstruction(BelteDiagnosticQueue diagnostics, Binder originalBinder) {
        return next.BindForEachDeconstruction(diagnostics, originalBinder);
    }

    internal virtual BoundStatement BindNullBindingDeconstruction(
        BelteDiagnosticQueue diagnostics,
        Binder originalBinder) {
        return next.BindNullBindingDeconstruction(diagnostics, originalBinder);
    }

    private protected bool BindForEachCollection(
        SyntaxNode syntax,
        SyntaxNode collectionSyntax,
        ref BoundExpression collectionExpr,
        BelteDiagnosticQueue diagnostics,
        out TypeWithAnnotations inferredType) {
        var type = collectionExpr.StrippedType();
        var iterOps = type.GetMembers(WellKnownMemberNames.IterOperatorName);
        var lengthOps = type.GetMembers(WellKnownMemberNames.LengthOperatorName);
        var bestIndexOp = type.GetMembers(WellKnownMemberNames.IndexOperatorName)
            .WhereAsArray(m => m is MethodSymbol e && e.GetParameterType(1).specialType == SpecialType.Int)
            .SingleOrDefault() as MethodSymbol;
        var worseIndexOp = type.GetMembers(WellKnownMemberNames.IndexOperatorName)
            .WhereAsArray(m => m is MethodSymbol e && e.GetParameterType(1).StrippedType().specialType == SpecialType.Int)
            .SingleOrDefault() as MethodSymbol;

        if (type.IsArray()) {
            inferredType = ((ArrayTypeSymbol)type).elementTypeWithAnnotations;
            return false;
        } else if (type.specialType == SpecialType.String) {
            inferredType = new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Char));
            return false;
        } else if (type.originalDefinition.Equals(CorLibrary.GetWellKnownType(WellKnownType.Enumerator))) {
            inferredType = ((NamedTypeSymbol)type).templateArguments[0].type;
            return false;
        } else if (lengthOps.Any() && worseIndexOp is not null) {
            inferredType = (bestIndexOp ?? worseIndexOp).returnTypeWithAnnotations;
            return false;
        } else if (iterOps.Any()) {
            inferredType = ((NamedTypeSymbol)((MethodSymbol)iterOps.Single()).returnType).templateArguments[0].type;
            return false;
        } else {
            diagnostics.Push(Error.InvalidForEachExpression(collectionSyntax.location));
            inferredType = new TypeWithAnnotations(CreateErrorType());
            return true;
        }
    }

    private protected bool BindNullBindingSource(
        SyntaxNode syntax,
        SyntaxNode sourceSyntax,
        ref BoundExpression sourceExpr,
        BelteDiagnosticQueue diagnostics,
        out TypeWithAnnotations inferredType) {
        if (sourceExpr.IsLiteralNull() || sourceExpr.kind == BoundKind.UnconvertedNullptrExpression) {
            diagnostics.Push(Error.NullBindingOnNull(sourceSyntax.location));
            inferredType = new TypeWithAnnotations(CreateErrorType());
            return true;
        }

        if (!sourceExpr.Type().IsNullableType()) {
            diagnostics.Push(Error.NullBindingRequiresNullable(sourceSyntax.location));
            inferredType = new TypeWithAnnotations(sourceExpr.Type());
            return true;
        }

        inferredType = new TypeWithAnnotations(sourceExpr.StrippedType());
        return false;
    }

    private BoundStatement BindBreakStatement(BreakStatementSyntax node, BelteDiagnosticQueue diagnostics) {
        var target = breakLabel;

        if (target is null) {
            diagnostics.Push(Error.InvalidBreakOrContinue(node.location));
            return new BoundErrorStatement(node, [], hasErrors: true);
        }

        return new BoundBreakStatement(node, target);
    }

    private BoundStatement BindUnreachableStatement(UnreachableStatementSyntax node) {
        return new BoundUnreachableStatement(node);
    }

    private BoundStatement BindContinueStatement(ContinueStatementSyntax node, BelteDiagnosticQueue diagnostics) {
        var target = continueLabel;

        if (target is null) {
            diagnostics.Push(Error.InvalidBreakOrContinue(node.location));
            return new BoundErrorStatement(node, [], hasErrors: true);
        }

        return new BoundContinueStatement(node, target);
    }

    private BoundStatement BindCommitStatement(CommitStatementSyntax node, BelteDiagnosticQueue diagnostics) {
        var target = BuildWithCommit();

        if (target is null) {
            diagnostics.Push(Error.InvalidCommit(node.location));
            return new BoundErrorStatement(node, [], hasErrors: true);
        }

        return new BoundCommitStatement(node, target);
    }

    private BoundStatement BindInlineILStatement(InlineILStatementSyntax node, BelteDiagnosticQueue diagnostics) {
        var noVerify = node.noverifyKeyword is not null;
        var instructions = ArrayBuilder<(OpCode, ConstantValue, Symbol)>.GetInstance();
        var hasAnyErrors = false;
        var stackOffset = 0;

        foreach (var instructionSyntax in node.instructions) {
            var opCode = BindOpCodeFromIdentifier(
                instructionSyntax.location,
                instructionSyntax.opCode,
                instructionSyntax.opCodeSuffixOne,
                instructionSyntax.opCodeSuffixTwo,
                instructionSyntax.opCodeSuffixThree,
                diagnostics,
                out var hasErrors,
                out var displayString
            );

            hasAnyErrors |= hasErrors;

            var (constant, symbol) = hasErrors ? (null, null) : BindOpCodeOperand(
                instructionSyntax,
                opCode,
                instructionSyntax.literal,
                instructionSyntax.symbol,
                instructionSyntax.parameterList,
                diagnostics,
                displayString,
                out hasErrors
            );

            hasAnyErrors |= hasErrors;

            stackOffset += opCode.StackOffset(
                symbol is FunctionPointerTypeSymbol t ? t.signature : symbol as MethodSymbol
            );

            instructions.Add((opCode, constant, symbol));
        }

        if (compilation.options.buildMode == BuildMode.CSharpTranspile) {
            diagnostics.Push(Error.Unsupported.InlineIL(node.keyword.location));
            hasAnyErrors = true;
        }

        if (!hasAnyErrors && !noVerify && stackOffset != 0) {
            diagnostics.Push(Error.UnbalancedILStack(node.keyword.location));
            hasAnyErrors = true;
        }

        return new BoundInlineILStatement(node, instructions.ToImmutableAndFree(), hasErrors: hasAnyErrors);
    }

    private OpCode BindOpCodeFromIdentifier(
        TextLocation location,
        SyntaxToken identifier,
        SyntaxToken opCodeSuffixOne,
        SyntaxToken opCodeSuffixTwo,
        SyntaxToken opCodeSuffixThree,
        BelteDiagnosticQueue diagnostics,
        out bool hasErrors,
        out string displayString) {
        hasErrors = false;
        var opCodeText = identifier.text.ToLower();
        var suffixOneText = opCodeSuffixOne?.text?.ToLower() ?? "";
        var suffixTwoText = opCodeSuffixTwo?.text?.ToLower() ?? "";
        var suffixThreeText = opCodeSuffixThree?.text?.ToLower() ?? "";

        if (opCodeSuffixThree is not null)
            displayString = $"{opCodeText}.{suffixOneText}.{suffixTwoText}.{suffixThreeText}";
        else if (opCodeSuffixTwo is not null)
            displayString = $"{opCodeText}.{suffixOneText}.{suffixTwoText}";
        else if (opCodeSuffixOne is not null)
            displayString = $"{opCodeText}.{suffixOneText}";
        else
            displayString = opCodeText;

        switch (displayString) {
            case "add":
                return OpCode.Add;
            case "add.ovf":
                return OpCode.Add_Ovf;
            case "add.ovf.un":
                return OpCode.Add_Ovf_Un;
            case "and":
                return OpCode.And;
            case "arglist":
                return OpCode.Arglist;
            case "box":
                return OpCode.Box;
            case "call":
                return OpCode.Call;
            case "calli":
                return OpCode.Calli;
            case "callvirt":
                return OpCode.Callvirt;
            case "castclass":
                return OpCode.Castclass;
            case "ceq":
                return OpCode.Ceq;
            case "cgt":
                return OpCode.Cgt;
            case "cgt.un":
                return OpCode.Cgt_Un;
            case "clt":
                return OpCode.Clt;
            case "clt.un":
                return OpCode.Clt_Un;
            case "ckfinite":
                return OpCode.Ckfinite;
            case "constrained":
                return OpCode.Constrained;
            case "conv.i":
                return OpCode.Conv_I;
            case "conv.i1":
                return OpCode.Conv_I1;
            case "conv.i2":
                return OpCode.Conv_I2;
            case "conv.i4":
                return OpCode.Conv_I4;
            case "conv.i8":
                return OpCode.Conv_I8;
            case "conv.ovf.i":
                return OpCode.Conv_Ovf_I;
            case "conv.ovf.i.un":
                return OpCode.Conv_Ovf_I_Un;
            case "conv.ovf.i1":
                return OpCode.Conv_Ovf_I1;
            case "conv.ovf.i1.un":
                return OpCode.Conv_Ovf_I1_Un;
            case "conv.ovf.i2":
                return OpCode.Conv_Ovf_I2;
            case "conv.ovf.i2.un":
                return OpCode.Conv_Ovf_I2_Un;
            case "conv.ovf.i4":
                return OpCode.Conv_Ovf_I4;
            case "conv.ovf.i4.un":
                return OpCode.Conv_Ovf_I4_Un;
            case "conv.ovf.i8":
                return OpCode.Conv_Ovf_I8;
            case "conv.ovf.i8.un":
                return OpCode.Conv_Ovf_I8_Un;
            case "conv.ovf.u":
                return OpCode.Conv_Ovf_U;
            case "conv.ovf.u.un":
                return OpCode.Conv_Ovf_U_Un;
            case "conv.ovf.u1":
                return OpCode.Conv_Ovf_U1;
            case "conv.ovf.u1.un":
                return OpCode.Conv_Ovf_U1_Un;
            case "conv.ovf.u2":
                return OpCode.Conv_Ovf_U2;
            case "conv.ovf.u2.un":
                return OpCode.Conv_Ovf_U2_Un;
            case "conv.ovf.u4":
                return OpCode.Conv_Ovf_U4;
            case "conv.ovf.u4.un":
                return OpCode.Conv_Ovf_U4_Un;
            case "conv.ovf.u8":
                return OpCode.Conv_Ovf_U8;
            case "conv.ovf.u8.un":
                return OpCode.Conv_Ovf_U8_Un;
            case "conv.r.un":
                return OpCode.Conv_R_Un;
            case "conv.r4":
                return OpCode.Conv_R4;
            case "conv.r8":
                return OpCode.Conv_R8;
            case "conv.u":
                return OpCode.Conv_U;
            case "conv.u1":
                return OpCode.Conv_U1;
            case "conv.u2":
                return OpCode.Conv_U2;
            case "conv.u4":
                return OpCode.Conv_U4;
            case "conv.u8":
                return OpCode.Conv_U8;
            case "cpblk":
                return OpCode.Cpblk;
            case "cpobj":
                return OpCode.Cpobj;
            case "div":
                return OpCode.Div;
            case "div.un":
                return OpCode.Div_Un;
            case "dup":
                return OpCode.Dup;
            case "initblk":
                return OpCode.Initblk;
            case "initobj":
                return OpCode.Initobj;
            case "isinst":
                return OpCode.Isinst;
            case "ldarg":
                return OpCode.Ldarg;
            case "ldarg.0":
                return OpCode.Ldarg_0;
            case "ldarg.1":
                return OpCode.Ldarg_1;
            case "ldarg.2":
                return OpCode.Ldarg_2;
            case "ldarg.3":
                return OpCode.Ldarg_3;
            case "ldarg.s":
                return OpCode.Ldarg_S;
            case "ldarga":
                return OpCode.Ldarga;
            case "ldarga.s":
                return OpCode.Ldarga_S;
            case "ldc.i4":
                return OpCode.Ldc_I4;
            case "ldc.i4.0":
                return OpCode.Ldc_I4_0;
            case "ldc.i4.1":
                return OpCode.Ldc_I4_1;
            case "ldc.i4.2":
                return OpCode.Ldc_I4_2;
            case "ldc.i4.3":
                return OpCode.Ldc_I4_3;
            case "ldc.i4.4":
                return OpCode.Ldc_I4_4;
            case "ldc.i4.5":
                return OpCode.Ldc_I4_5;
            case "ldc.i4.6":
                return OpCode.Ldc_I4_6;
            case "ldc.i4.7":
                return OpCode.Ldc_I4_7;
            case "ldc.i4.8":
                return OpCode.Ldc_I4_8;
            case "ldc.i4.m1":
                return OpCode.Ldc_I4_M1;
            case "ldc.i4.s":
                return OpCode.Ldc_I4_S;
            case "ldc.i8":
                return OpCode.Ldc_I8;
            case "ldc.r4":
                return OpCode.Ldc_R4;
            case "ldc.r8":
                return OpCode.Ldc_R8;
            case "ldelem":
                return OpCode.Ldelem;
            case "ldelem.i":
                return OpCode.Ldelem_I;
            case "ldelem.i1":
                return OpCode.Ldelem_I1;
            case "ldelem.i2":
                return OpCode.Ldelem_I2;
            case "ldelem.i4":
                return OpCode.Ldelem_I4;
            case "ldelem.i8":
                return OpCode.Ldelem_I8;
            case "ldelem.r4":
                return OpCode.Ldelem_R4;
            case "ldelem.r8":
                return OpCode.Ldelem_R8;
            case "ldelem.ref":
                return OpCode.Ldelem_Ref;
            case "ldelem.u1":
                return OpCode.Ldelem_U1;
            case "ldelem.u2":
                return OpCode.Ldelem_U2;
            case "ldelem.u4":
                return OpCode.Ldelem_U4;
            case "ldelema":
                return OpCode.Ldelema;
            case "ldfld":
                return OpCode.Ldfld;
            case "ldflda":
                return OpCode.Ldflda;
            case "ldftn":
                return OpCode.Ldftn;
            case "ldind.i":
                return OpCode.Ldind_I;
            case "ldind.i1":
                return OpCode.Ldind_I1;
            case "ldind.i2":
                return OpCode.Ldind_I2;
            case "ldind.i4":
                return OpCode.Ldind_I4;
            case "ldind.i8":
                return OpCode.Ldind_I8;
            case "ldind.r4":
                return OpCode.Ldind_R4;
            case "ldind.r8":
                return OpCode.Ldind_R8;
            case "ldind.ref":
                return OpCode.Ldind_Ref;
            case "ldind.u1":
                return OpCode.Ldind_U1;
            case "ldind.u2":
                return OpCode.Ldind_U2;
            case "ldind.u4":
                return OpCode.Ldind_U4;
            case "ldlen":
                return OpCode.Ldlen;
            case "ldloc":
                return OpCode.Ldloc;
            case "ldloc.0":
                return OpCode.Ldloc_0;
            case "ldloc.1":
                return OpCode.Ldloc_1;
            case "ldloc.2":
                return OpCode.Ldloc_2;
            case "ldloc.3":
                return OpCode.Ldloc_3;
            case "ldloc.s":
                return OpCode.Ldloc_S;
            case "ldloca":
                return OpCode.Ldloca;
            case "ldloca.s":
                return OpCode.Ldloca_S;
            case "ldnull":
                return OpCode.Ldnull;
            case "ldobj":
                return OpCode.Ldobj;
            case "ldsfld":
                return OpCode.Ldsfld;
            case "ldsflda":
                return OpCode.Ldsflda;
            case "ldstr":
                return OpCode.Ldstr;
            case "ldtoken":
                return OpCode.Ldtoken;
            case "ldvirtftn":
                return OpCode.Ldvirtftn;
            case "localloc":
                return OpCode.Localloc;
            case "mkrefany":
                return OpCode.Mkrefany;
            case "mul":
                return OpCode.Mul;
            case "mul.ovf":
                return OpCode.Mul_Ovf;
            case "mul.ovf.un":
                return OpCode.Mul_Ovf_Un;
            case "neg":
                return OpCode.Neg;
            case "newarr":
                return OpCode.Newarr;
            case "newobj":
                return OpCode.Newobj;
            case "nop":
                return OpCode.Nop;
            case "not":
                return OpCode.Not;
            case "or":
                return OpCode.Or;
            case "pop":
                return OpCode.Pop;
            case "readonly":
                return OpCode.Readonly;
            case "refanytype":
                return OpCode.Refanytype;
            case "refanyval":
                return OpCode.Refanyval;
            case "rem":
                return OpCode.Rem;
            case "rem.un":
                return OpCode.Rem_Un;
            case "starg":
                return OpCode.Starg;
            case "starg.s":
                return OpCode.Starg_S;
            case "shl":
                return OpCode.Shl;
            case "shr":
                return OpCode.Shr;
            case "shr.un":
                return OpCode.Shr_Un;
            case "sizeof":
                return OpCode.Sizeof;
            case "stelem":
                return OpCode.Stelem;
            case "stelem.i":
                return OpCode.Stelem_I;
            case "stelem.i1":
                return OpCode.Stelem_I1;
            case "stelem.i2":
                return OpCode.Stelem_I2;
            case "stelem.i4":
                return OpCode.Stelem_I4;
            case "stelem.i8":
                return OpCode.Stelem_I8;
            case "stelem.r4":
                return OpCode.Stelem_R4;
            case "stelem.r8":
                return OpCode.Stelem_R8;
            case "stelem.ref":
                return OpCode.Stelem_Ref;
            case "stfld":
                return OpCode.Stfld;
            case "stind.i":
                return OpCode.Stind_I;
            case "stind.i1":
                return OpCode.Stind_I1;
            case "stind.i2":
                return OpCode.Stind_I2;
            case "stind.i4":
                return OpCode.Stind_I4;
            case "stind.i8":
                return OpCode.Stind_I8;
            case "stind.r4":
                return OpCode.Stind_R4;
            case "stind.r8":
                return OpCode.Stind_R8;
            case "stind.ref":
                return OpCode.Stind_Ref;
            case "stloc":
                return OpCode.Stloc;
            case "stloc.0":
                return OpCode.Stloc_0;
            case "stloc.1":
                return OpCode.Stloc_1;
            case "stloc.2":
                return OpCode.Stloc_2;
            case "stloc.3":
                return OpCode.Stloc_3;
            case "stloc.s":
                return OpCode.Stloc_S;
            case "stobj":
                return OpCode.Stobj;
            case "stsfld":
                return OpCode.Stsfld;
            case "sub":
                return OpCode.Sub;
            case "sub.ovf":
                return OpCode.Sub_Ovf;
            case "sub.ovf.un":
                return OpCode.Sub_Ovf_Un;
            case "tail":
                return OpCode.Tail;
            case "unaligned":
                return OpCode.Unaligned;
            case "unbox":
                return OpCode.Unbox;
            case "unbox.any":
                return OpCode.Unbox_Any;
            case "volatile":
                return OpCode.Volatile;
            case "xor":
                return OpCode.Xor;
            case "beq":
            case "beq.s":
            case "bge":
            case "bge.s":
            case "bge.un":
            case "bge.un.s":
            case "bgt":
            case "bgt.s":
            case "bgt.un":
            case "bgt.un.s":
            case "ble":
            case "ble.s":
            case "ble.un":
            case "ble.un.s":
            case "blt":
            case "blt.s":
            case "blt.un":
            case "blt.un.s":
            case "bne.un":
            case "bne.un.s":
            case "br":
            case "br.s":
            case "break":
            case "brfalse":
            case "brfalse.s":
            case "brinst":
            case "brinst.s":
            case "brnull":
            case "brnull.s":
            case "brtrue":
            case "brtrue.s":
            case "brzero":
            case "brzero.s":
            case "endfault":
            case "endfilter":
            case "endfinally":
            case "jmp":
            case "leave":
            case "leave.s":
            case "no":
            case "ret":
            case "rethrow":
            case "switch":
            case "throw":
                diagnostics.Push(Error.Unsupported.ILOpCode(location, displayString));
                hasErrors = true;
                return OpCode.Nop;
            default:
                diagnostics.Push(Error.UnknownILOpCode(location, displayString));
                hasErrors = true;
                return OpCode.Nop;
        }
    }

    private (ConstantValue, Symbol) BindOpCodeOperand(
        SyntaxNode node,
        OpCode opCode,
        SyntaxToken literal,
        TypeSyntax symbol,
        FunctionPointerParameterListSyntax parameterList,
        BelteDiagnosticQueue diagnostics,
        string displayString,
        out bool hasErrors) {
        hasErrors = false;
        var operandKind = opCode.ToOperandKind();

        if (operandKind == OperandKind.None) {
            if (literal is not null || symbol is not null) {
                diagnostics.Push(Error.InvalidILOperand(node.location, displayString));
                hasErrors = true;
            }

            return (null, null);
        }

        if (literal is not null) {
            if (!operandKind.IsLiteral()) {
                diagnostics.Push(Error.InvalidILOperandKind(node.location, displayString, operandKind.ToString()));
                hasErrors = true;
                return (null, null);
            }

            var targetSpecialType = operandKind.ToSpecialType();
            var targetType = CorLibrary.GetSpecialType(targetSpecialType);
            var value = literal.value;
            var specialType = SpecialTypeExtensions.SpecialTypeFromLiteralValue(value);
            var constantValue = new ConstantValue(value, specialType);
            var type = CorLibrary.GetSpecialType(specialType);
            BoundExpression boundOperand = new BoundLiteralExpression(node, constantValue, type);
            boundOperand = ReduceNumericIfApplicable(targetType, boundOperand);
            boundOperand = GenerateConversionForAssignment(targetType, boundOperand, diagnostics);
            hasErrors |= boundOperand.hasAnyErrors;

            if (boundOperand.constantValue is null && !boundOperand.hasAnyErrors) {
                diagnostics.Push(Error.ConstantExpected(literal.location));
                hasErrors = true;
                return (null, null);
            }

            return (boundOperand.constantValue, null);
        }

        if (operandKind.IsLiteral()) {
            diagnostics.Push(Error.InvalidILOperandKind(node.location, displayString, operandKind.ToString()));
            hasErrors = true;
            return (null, null);
        }

        switch (operandKind) {
            case OperandKind.Token:
            case OperandKind.TypeToken: {
                    var boundSymbol = BindType(symbol, diagnostics);
                    CreateParameterListError();
                    return (null, boundSymbol.type);
                }
            case OperandKind.Class: {
                    var boundSymbol = BindType(symbol, diagnostics);
                    CreateParameterListError();

                    if (boundSymbol.type.typeKind != TypeKind.Class) {
                        diagnostics.Push(Error.InvalidILOperandKind(node.location, displayString, operandKind.ToString()));
                        hasErrors = true;
                        return (null, null);
                    }

                    return (null, boundSymbol.type);
                }
            case OperandKind.Constructor: {
                    var boundSymbol = BindType(symbol, diagnostics);

                    if (boundSymbol.type.typeKind != TypeKind.Class) {
                        diagnostics.Push(Error.InvalidILOperandKind(node.location, displayString, operandKind.ToString()));
                        hasErrors = true;
                        return (null, null);
                    }

                    var constructors = ((NamedTypeSymbol)boundSymbol.type).instanceConstructors;
                    constructors = LookForSignature(constructors, true);

                    if (constructors.Length > 1) {
                        diagnostics.Push(Error.AmbiguousMethodOverload(symbol.location, constructors.ToArray()));
                        hasErrors = true;
                        return (null, null);
                    }

                    return (null, constructors.FirstOrDefault());
                }
            case OperandKind.Field: {
                    var boundSymbol = BindMethodGroup(symbol, false, false, diagnostics);
                    CreateParameterListError();

                    if (boundSymbol is not BoundFieldAccessExpression f) {
                        diagnostics.Push(Error.InvalidILOperandKind(node.location, displayString, operandKind.ToString()));
                        hasErrors = true;
                        return (null, null);
                    }

                    return (null, f.field);
                }
            case OperandKind.Method: {
                    var boundSymbol = BindMethodGroup(symbol, true, false, diagnostics);

                    if (boundSymbol is not BoundMethodGroup m) {
                        diagnostics.Push(Error.InvalidILOperandKind(node.location, displayString, operandKind.ToString()));
                        hasErrors = true;
                        return (null, null);
                    }

                    var methods = LookForSignature(m.methods, !boundSymbol.hasAnyErrors);

                    if (methods.Length > 1) {
                        diagnostics.Push(Error.AmbiguousMethodOverload(symbol.location, methods.ToArray()));
                        hasErrors = true;
                        return (null, null);
                    }

                    return (null, methods.FirstOrDefault());
                }
            case OperandKind.FunctionPointer: {
                    var boundSymbol = BindType(symbol, diagnostics);
                    CreateParameterListError();

                    if (boundSymbol.type.typeKind != TypeKind.FunctionPointer) {
                        diagnostics.Push(Error.InvalidILOperandKind(node.location, displayString, operandKind.ToString()));
                        hasErrors = true;
                        return (null, null);
                    }

                    return (null, boundSymbol.type);
                }
            case OperandKind.ValueType: {
                    var boundSymbol = BindType(symbol, diagnostics);
                    CreateParameterListError();

                    // TODO Should this just be normal isValueType instead?
                    if (!boundSymbol.type.IsVerifierValue()) {
                        diagnostics.Push(Error.InvalidILOperandKind(node.location, displayString, operandKind.ToString()));
                        hasErrors = true;
                        return (null, null);
                    }

                    return (null, boundSymbol.type);
                }
            default:
                throw ExceptionUtilities.UnexpectedValue(operandKind);
        }

        void CreateParameterListError() {
            if (parameterList is not null)
                diagnostics.Push(Error.UnexpectedParameterList(parameterList.location));
        }

        ImmutableArray<MethodSymbol> LookForSignature(ImmutableArray<MethodSymbol> candidates, bool diagnose) {
            if (parameterList is null)
                return candidates;

            var builder = ArrayBuilder<MethodSymbol>.GetInstance();

            var parameterTypes = ParameterHelpers.MakeFunctionPointerParameters(
                this,
                null,
                parameterList.parameters,
                diagnostics
            ).SelectAsArray(p => p.type);
            var parameterCount = parameterTypes.Length;

            builder.AddRange(candidates.Where(c => c.parameterCount == parameterCount));

            if (builder.Count == 0 && diagnose) {
                diagnostics.Push(Error.WrongArgumentCount(symbol.location, candidates[0].name, parameterCount));
                return builder.ToImmutableAndFree();
            }

            for (var i = 0; i < builder.Count; i++) {
                var method = builder[i];

                if (!parameterTypes.SequenceEqual(
                    method.GetParameterTypes().SelectAsArray(p => p.type),
                    TypeCompareKind.ConsiderEverything,
                    (param1, param2, compareKind) => param1.Equals(param2, compareKind))) {
                    builder.RemoveAt(i);
                    i--;
                }
            }

            if (builder.Count == 0 && diagnose)
                diagnostics.Push(Error.InvalidParameterList(symbol.location, candidates[0].name));

            return builder.ToImmutableAndFree();
        }
    }

    private BoundStatement BindTryStatement(TryStatementSyntax node, BelteDiagnosticQueue diagnostics) {
        var tryBlock = BindBlockStatement(node.body, diagnostics);

        var catchBlock = (node.catchClause is not null)
            ? BindBlockStatement(node.catchClause.body, diagnostics)
            : null;

        var finallyBlock = (node.finallyClause is not null)
            ? BindBlockStatement(node.finallyClause.body, diagnostics)
            : null;

        return new BoundTryStatement(node, tryBlock, catchBlock, finallyBlock);
    }

    private BoundStatement BindLocalFunctionStatement(
        LocalFunctionStatementSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var localSymbol = LookupLocalFunction(node.identifier);
        var hasErrors = localSymbol.scopeBinder.ValidateDeclarationNameConflictsInScope(localSymbol, diagnostics);

        BoundBlockStatement body = null;

        if (node.body is not null) {
            body = RunAnalysis(BindBlockStatement(node.body, diagnostics), diagnostics);
        } else if (!hasErrors && (!localSymbol.isExtern || !localSymbol.isStatic)) {
            hasErrors = true;
            throw ExceptionUtilities.Unreachable();
            // diagnostics.Push(Error.LocalFunctionMissingBody(localSymbol.location, localSymbol));
        }

        localSymbol.GetDeclarationDiagnostics(diagnostics);

        return new BoundLocalFunctionStatement(node, localSymbol, body, hasErrors);

        BoundBlockStatement RunAnalysis(BoundBlockStatement block, BelteDiagnosticQueue blockDiagnostics) {
            if (block is not null) {
                // TODO do we need to do any control flow analysis here
            }

            return block;
        }
    }

    private BoundStatement BindScopedStatement(ScopedStatementSyntax node, BelteDiagnosticQueue diagnostics) {
        var usingBinder = GetBinder(node);
        return usingBinder.BindScopedStatementParts(diagnostics, usingBinder);
    }

    internal virtual BoundStatement BindScopedStatementParts(BelteDiagnosticQueue diagnostics, Binder originalBinder) {
        return next.BindScopedStatementParts(diagnostics, originalBinder);
    }

    private BoundLocalDeclarationStatement BindLocalDeclarationStatement(
        LocalDeclarationStatementSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var typeSyntax = node.declaration.type.SkipRef(out _);
        var isConst = node.isConst;
        var isConstExpr = node.isConstExpr;

        var declarationType = BindVariableTypeWithAnnotations(
            node.declaration,
            diagnostics,
            typeSyntax,
            ref isConst,
            ref isConstExpr,
            out var isImplicitlyTyped,
            out var isNonNullable,
            out var isNullable,
            out var alias
        );

        var kind = isConstExpr
            ? DataContainerDeclarationKind.ConstantExpression
            : (isConst ? DataContainerDeclarationKind.Constant : DataContainerDeclarationKind.Variable);

        return BindVariableDeclaration(
            kind,
            isImplicitlyTyped,
            isNonNullable,
            isNullable,
            node.declaration,
            typeSyntax,
            declarationType,
            alias,
            diagnostics,
            true,
            node.modifiers,
            node.scopedKeyword is not null,
            node
        );
    }

    internal BoundLocalDeclarationStatement BindVariableDeclaration(
        DataContainerDeclarationKind kind,
        bool isImplicitlyTyped,
        bool isNonNullable,
        bool isNullable,
        VariableDeclarationSyntax declaration,
        TypeSyntax typeSyntax,
        TypeWithAnnotations declarationType,
        AliasSymbol alias,
        BelteDiagnosticQueue diagnostics,
        bool includeBoundType,
        SyntaxTokenList modifiers,
        bool isScoped,
        BelteSyntaxNode associatedSyntaxNode = null) {
        var dataContainer = LocateDeclaredVariableSymbol(declaration, typeSyntax, modifiers);

        dataContainer.GetDeclarationDiagnostics(diagnostics);

        return BindVariableDeclaration(
            dataContainer,
            kind,
            isImplicitlyTyped,
            isNonNullable,
            isNullable,
            declaration,
            typeSyntax,
            declarationType,
            alias,
            diagnostics,
            includeBoundType,
            isScoped,
            associatedSyntaxNode
        );
    }

    private SourceDataContainerSymbol LocateDeclaredVariableSymbol(
        VariableDeclarationSyntax declaration,
        TypeSyntax typeSyntax,
        SyntaxTokenList modifiers) {
        return LocateDeclaredVariableSymbol(
            declaration.identifier,
            typeSyntax,
            declaration.initializer,
            modifiers
        );
    }

    private SourceDataContainerSymbol LocateDeclaredVariableSymbol(
        SyntaxToken identifier,
        TypeSyntax typeSyntax,
        EqualsValueClauseSyntax equalsValue,
        SyntaxTokenList modifiers) {
        var localSymbol = LookupLocal(identifier) ?? SourceDataContainerSymbol.MakeLocal(
            containingMember,
            this,
            false,
            typeSyntax,
            identifier,
            equalsValue,
            modifiers
        );

        return localSymbol;
    }

    private protected BoundLocalDeclarationStatement BindVariableDeclaration(
        SourceDataContainerSymbol localSymbol,
        DataContainerDeclarationKind kind,
        bool isImplicitlyTyped,
        bool isNonNullable,
        bool isNullable,
        VariableDeclarationSyntax declaration,
        TypeSyntax typeSyntax,
        TypeWithAnnotations declarationType,
        AliasSymbol alias,
        BelteDiagnosticQueue diagnostics,
        bool includeBoundType,
        bool isScoped,
        BelteSyntaxNode associatedSyntaxNode = null) {
        var localDiagnostics = BelteDiagnosticQueue.GetInstance();
        associatedSyntaxNode ??= declaration;

        var nameConflict = localSymbol.scopeBinder.ValidateDeclarationNameConflictsInScope(localSymbol, diagnostics);
        var hasErrors = false;
        var equalsClauseSyntax = declaration.initializer;

        if (!IsInitializerRefKindValid(
            equalsClauseSyntax,
            declaration,
            localSymbol.refKind,
            diagnostics,
            out var valueKind,
            out var value)) {
            hasErrors = true;
        }

        BoundExpression initializer = null;

        var conversionFlags = localSymbol.refKind != RefKind.None
            ? ConversionForAssignmentFlags.RefAssignment
            : ConversionForAssignmentFlags.None;

        if (isImplicitlyTyped) {
            alias = null;

            if (localSymbol.declarationKind != DataContainerDeclarationKind.Variable &&
                typeSyntax.kind != SyntaxKind.EmptyName) {
                diagnostics.Push(Error.ConstantAndVariable(localSymbol.location));
            }

            // TODO Should we ever lift the elements?
            initializer = BindInferredVariableInitializer(
                diagnostics,
                value,
                valueKind,
                declaration,
                false /*!isNonNullable || isNullable*/
            );

            if (initializer is not null && initializer.IsLiteralNull()) {
                diagnostics.Push(Error.NullAssignOnImplicit(declaration.location));
                hasErrors = true;
            }

            if (initializer is not null && initializer.kind == BoundKind.UnconvertedNullptrExpression) {
                diagnostics.Push(Error.NullptrNoTargetType(initializer.syntax.location));
                hasErrors = true;
            }

            if (initializer is not null &&
                initializer.kind == BoundKind.ArrayCreationExpression &&
                initializer.type is ArrayTypeSymbol arrayType &&
                // This node means we have something like `new Buffer...` which is obviously intentionally not a fat array
                // TODO Just need to double check there aren't any other nodes to not "fatify" on
                initializer.syntax.kind != SyntaxKind.ObjectCreationExpression) {
                var fatArray = CreateArrayOrFatArray(arrayType.elementTypeWithAnnotations, arrayType.rank, diagnostics);
                initializer = GenerateConversionForAssignment(fatArray, initializer, diagnostics);
            }

            var initializerType = initializer?.Type();

            if (initializerType is not null) {
                declarationType = new TypeWithAnnotations(initializerType);

                if (declarationType.IsVoidType()) {
                    diagnostics.Push(
                        Error.ImplicitlyTypedLocalAssignedBadValue(declaration.location, declarationType.type)
                    );

                    declarationType = new TypeWithAnnotations(CreateErrorType("var"));
                    hasErrors = true;
                } else if (declarationType.type.IsPointerOrFunctionPointer() && isNullable) {
                    diagnostics.Push(Error.CannotAnnotatePointer(typeSyntax.location));
                } else {
                    if (isNonNullable && declarationType.IsNullableType()) {
                        declarationType = new TypeWithAnnotations(declarationType.nullableUnderlyingTypeOrSelf);
                        initializer = GenerateConversionForAssignment(
                            declarationType.type,
                            initializer,
                            diagnostics,
                            conversionFlags
                        );
                    } else if (isNullable && !declarationType.IsNullableType()) {
                        declarationType = declarationType.SetIsAnnotated();
                        initializer = GenerateConversionForAssignment(
                            declarationType.type,
                            initializer,
                            diagnostics,
                            conversionFlags
                        );
                    }
                }

                if (!declarationType.type.IsErrorType()) {
                    if (declarationType.isStatic) {
                        diagnostics.Push(Error.CannotInitializeVarWithStaticClass(
                            typeSyntax.location,
                            initializerType
                        ));

                        hasErrors = true;
                    }
                }
            } else {
                declarationType = new TypeWithAnnotations(CreateErrorType("var"));
                hasErrors = true;
            }
        } else {
            if (declaration.argumentList is not null) {
                // Handled later
            } else if (equalsClauseSyntax is null) {
                if (declarationType.IsNullableType() || declarationType.IsVoidType() ||
                    declarationType.type.IsPointerOrFunctionPointer()) {
                    initializer = new BoundLiteralExpression(
                        declaration,
                        ConstantValue.Null,
                        declarationType.type
                    );

                    if (declarationType.type.IsPointerOrFunctionPointer()) {
                        initializer = new BoundCastExpression(
                            declaration,
                            initializer,
                            Conversion.ImplicitNullToPointer,
                            null,
                            declarationType.type
                        );
                    }
                } else {
                    if (localSymbol.isGlobal || localSymbol.isConst || localSymbol.isFinal) {
                        initializer = ErrorExpression(declaration);

                        if (!declarationType.isStatic)
                            diagnostics.Push(Error.NoInitOnNonNullable(declaration.location));

                        hasErrors = true;
                    }
                }
            } else {
                initializer = BindPossibleArrayInitializer(value, declarationType.type, valueKind, diagnostics);
                initializer = ReduceNumericIfApplicable(declarationType.type, initializer);
                initializer = GenerateConversionForAssignment(
                    declarationType.type,
                    initializer,
                    localDiagnostics,
                    conversionFlags
                );
            }
        }

        if (declaration.argumentList is not null) {
            if (isImplicitlyTyped)
                diagnostics.Push(Error.ImplicitlyTypedStackAllocLocal(declaration.location));
            else if (equalsClauseSyntax is not null)
                diagnostics.Push(Error.StackAllocLocalWithInitializer(declaration.location));

            if (flags.Includes(BinderFlags.InCatchBlock) || flags.Includes(BinderFlags.InFinallyBlock))
                diagnostics.Push(Error.StackAllocInCatchFinally(declaration.location));

            var arguments = declaration.argumentList.arguments;

            var elementType = declarationType;
            var type = GetStackAllocType(declaration, elementType, BelteDiagnosticQueue.Discarded, out hasErrors);

            var intType = CorLibrary.GetSpecialType(SpecialType.Int32);

            if (arguments.Count != 1)
                diagnostics.Push(Error.BadStackAllocExpression(declaration.argumentList.location));

            if (arguments.Count == 0) {
                initializer = new BoundStackAllocExpression(
                    declaration,
                    elementType.type,
                    BoundFactory.Literal(declaration, 1, intType),
                    type,
                    hasErrors
                );

                declarationType = new TypeWithAnnotations(type);
            } else {
                var sizeExpression = ((ArgumentSyntax)arguments[0]).expression;

                var boundSize = BindValue(sizeExpression, diagnostics, BindValueKind.RValue);
                boundSize = ReduceNumericIfApplicable(intType, boundSize);
                boundSize = GenerateConversionForAssignment(intType, boundSize, diagnostics);

                if (boundSize.constantValue is not null && (int)boundSize.constantValue.value < 0) {
                    diagnostics.Push(Error.NegativeStackAllocSize(sizeExpression.location));
                    hasErrors = true;
                }

                initializer = new BoundStackAllocExpression(
                    declaration,
                    elementType.type,
                    boundSize,
                    type,
                    hasErrors
                );

                declarationType = new TypeWithAnnotations(type);
            }
        }

        localSymbol.SetTypeWithAnnotations(declarationType);

        if (kind == DataContainerDeclarationKind.ConstantExpression && initializer is not null) {
            var constantValueDiagnostics = localSymbol.GetConstantValueDiagnostics(initializer);
            diagnostics.PushRange(constantValueDiagnostics);
            hasErrors = constantValueDiagnostics.AnyErrors();
        }

        diagnostics.PushRangeAndFree(localDiagnostics);
        BoundTypeExpression boundDeclType = null;

        if (includeBoundType) {
            var invalidDimensions = ArrayBuilder<BoundExpression>.GetInstance();

            typeSyntax.VisitRankSpecifiers((rankSpecifier, args) => {
                var _ = false;
                var size = args.binder.BindArrayDimension(rankSpecifier.size, args.diagnostics, ref _);
                if (size is not null)
                    args.invalidDimensions.Add(size);
            }, (binder: this, invalidDimensions, diagnostics));

            boundDeclType = new BoundTypeExpression(typeSyntax, declarationType, alias, declarationType.type);
        }

        MethodSymbol disposeMethod = null;

        if (isScoped) {
            var stripped = declarationType.type.StrippedType();
            var lookupResult = LookupResult.GetInstance();
            LookupMembersInternal(
                lookupResult,
                stripped,
                WellKnownMemberNames.Dispose,
                0,
                null,
                LookupOptions.MustBeInstance | LookupOptions.MustBeInvocableIfMember,
                this,
                null,
                false
            );

            if (lookupResult.isMultiViable) {
                disposeMethod = lookupResult.symbols.SingleOrDefault(s => s is MethodSymbol m && m.parameterCount == 0)
                    as MethodSymbol;
            }

            if (!lookupResult.isMultiViable || disposeMethod is null) {
                diagnostics.Push(Error.ScopedWithoutDispose(
                    associatedSyntaxNode?.location ?? declaration.location,
                    stripped
                ));
            }
        }

        return new BoundLocalDeclarationStatement(
            associatedSyntaxNode,
            new BoundDataContainerDeclaration(
                declaration,
                localSymbol,
                hasErrors ? BindToTypeForErrorRecovery(initializer) : initializer
            ),
            isScoped,
            disposeMethod,
            hasErrors | nameConflict
        );
    }

    internal static BoundExpression ReduceNumericIfApplicable(TypeSymbol declarationType, BoundExpression expression) {
        var declarationSpecialType = declarationType.StrippedType().specialType;
        var shouldTryToReduce = ShouldTryToReduce(expression, declarationSpecialType);

        if (shouldTryToReduce) {
            var literalValue = LiteralUtilities.ReduceNumeric(
                expression.constantValue.value,
                declarationSpecialType.IsUnsigned()
            );

            var specialType = SpecialTypeExtensions.SpecialTypeFromLiteralValue(literalValue);
            var constantValue = new ConstantValue(literalValue, specialType);
            var type = CorLibrary.GetSpecialType(specialType);
            expression = new BoundLiteralExpression(expression.syntax, constantValue, type);
        }

        return expression;
    }

    private static bool ShouldTryToReduce(BoundExpression expression, SpecialType declarationSpecialType) {
        return (expression.kind == BoundKind.LiteralExpression || expression.constantValue is not null) &&
            expression.type is not null &&
            expression.type.specialType.IsNumeric() &&
            declarationSpecialType.IsNumeric();
    }

    internal BoundExpression BindInferredVariableInitializer(
        BelteDiagnosticQueue diagnostics,
        RefKind refKind,
        EqualsValueClauseSyntax initializer,
        BelteSyntaxNode errorSyntax,
        bool shouldLiftIfPossible) {
        IsInitializerRefKindValid(initializer, initializer, refKind, diagnostics, out var valueKind, out var value);
        return BindInferredVariableInitializer(diagnostics, value, valueKind, errorSyntax, shouldLiftIfPossible);
    }

    private protected BoundExpression BindInferredVariableInitializer(
        BelteDiagnosticQueue diagnostics,
        ExpressionSyntax initializer,
        BindValueKind valueKind,
        BelteSyntaxNode errorSyntax,
        bool shouldLiftIfPossible = false) {
        if (initializer is null) {
            diagnostics.Push(Error.NoInitOnImplicit(errorSyntax.location));
            return null;
        }

        if (initializer.kind == SyntaxKind.InitializerListExpression) {
            var result = BindUnexpectedArrayInitializer(
                (InitializerListExpressionSyntax)initializer,
                diagnostics,
                true,
                shouldLiftIfPossible
            );

            return CheckValue(result, valueKind, diagnostics);
        }

        var value = BindValue(initializer, diagnostics, valueKind);
        var expression = value.kind == BoundKind.MethodGroup
            ? BindToInferredDelegateType(value, diagnostics)
            : BindToNaturalType(value, diagnostics);

        if (!expression.hasAnyErrors && !expression.HasExpressionType() && !compilation.options.isScript)
            diagnostics.Push(Error.ImplicitlyTypedLocalAssignedBadValue(errorSyntax.location, expression.Type()));

        return expression;
    }

    private BoundExpression BindToInferredDelegateType(BoundExpression expression, BelteDiagnosticQueue diagnostics) {
        if (compilation.options.isScript)
            return BindToNaturalType(expression, diagnostics);

        diagnostics.Push(
            Error.MethodGroupCannotBeUsedAsValue(expression.syntax.location, (BoundMethodGroup)expression)
        );

        return GenerateConversionForAssignment(CreateErrorType(), expression, diagnostics);
    }

    private BoundExpression BindArrayDimension(
        ExpressionSyntax dimension,
        BelteDiagnosticQueue diagnostics,
        ref bool hasErrors) {
        if (dimension is null)
            return null;

        return BindValue(dimension, diagnostics, BindValueKind.RValue);
    }

    internal ArrayTypeSymbol CreateArrayTypeSymbol(TypeSymbol elementType, int rank = 1) {
        ArgumentNullException.ThrowIfNull(elementType);

        if (rank < 1)
            throw new ArgumentException(null, nameof(rank));

        return ArrayTypeSymbol.CreateArray(new TypeWithAnnotations(elementType, true), rank);
    }

    internal bool ValidateDeclarationNameConflictsInScope(Symbol symbol, BelteDiagnosticQueue diagnostics) {
        var location = GetLocation(symbol);
        return ValidateNameConflictsInScope(symbol, location, symbol.name, diagnostics);
    }

    private TextLocation GetLocation(Symbol symbol) {
        return symbol.location ?? symbol.containingSymbol.location;
    }

    private bool ValidateNameConflictsInScope(
        Symbol symbol,
        TextLocation location,
        string name,
        BelteDiagnosticQueue diagnostics) {
        if (string.IsNullOrEmpty(name))
            return false;

        var onlyLookingForWarnings = false;

        for (var binder = this; binder is not null; binder = binder.next) {
            if (binder is InContainerBinder inContainerBinder) {
                var container = inContainerBinder.container;

                if (name == container.name && symbol.kind == SymbolKind.Local) {
                    diagnostics.Push(Warning.LocalUsingTypeName(location, name));
                    return false;
                }

                // TODO This check is really slow because it populates members
                // TODO We need to consider how valuable this warning really is
                // foreach (var member in container.GetMembers()) {
                //     if (member.name == name && member.kind == SymbolKind.NamedType) {
                //         diagnostics.Push(Warning.LocalUsingTypeName(location, name));
                //         return false;
                //     }
                // }
            }

            if (!onlyLookingForWarnings) {
                var scope = binder as LocalScopeBinder;

                if (scope?.EnsureSingleDefinition(symbol, name, location, diagnostics) == true)
                    return true;
            }

            if (binder.isNestedFunctionBinder)
                onlyLookingForWarnings = true;

            if (binder.IsLastBinderWithinMember())
                onlyLookingForWarnings = true;
        }

        return false;
    }

    private bool IsLastBinderWithinMember() {
        var containingMember = this.containingMember;
        return (containingMember?.kind) switch {
            null or SymbolKind.NamedType or SymbolKind.Namespace => true,
            _ => containingMember.containingSymbol?.kind == SymbolKind.NamedType &&
                                next?.containingMember != containingMember,
        };
    }

    internal TypeWithAnnotations BindVariableTypeWithAnnotations(
        BelteSyntaxNode declarationNode,
        BelteDiagnosticQueue diagnostics,
        TypeSyntax typeSyntax,
        ref bool isConst,
        ref bool isConstExpr,
        out bool isImplicitlyTyped,
        out bool isNonNullable,
        out bool isNullable,
        out AliasSymbol alias) {
        var declType = BindTypeOrImplicitType(
            typeSyntax.SkipRef(out _),
            diagnostics,
            out isImplicitlyTyped,
            out isNonNullable,
            out isNullable,
            out alias
        );

        if (!isImplicitlyTyped) {
            if (declType.nullableUnderlyingTypeOrSelf.isStatic)
                diagnostics.Push(Error.StaticDataContainer(declarationNode.location));

            if (declType.IsVoidType())
                diagnostics.Push(Error.VoidUsedAsType(typeSyntax.location));
        }

        return declType;
    }

    private BoundBlockStatement BindBlockStatement(BlockStatementSyntax node, BelteDiagnosticQueue diagnostics) {
        var binder = GetBinder(node);
        return binder.BindBlockParts(node, diagnostics);
    }

    private BoundBlockStatement BindBlockParts(BlockStatementSyntax node, BelteDiagnosticQueue diagnostics) {
        var syntaxStatements = node.statements;
        var nStatements = syntaxStatements.Count;

        var boundStatements = ArrayBuilder<BoundStatement>.GetInstance(nStatements);

        for (var i = 0; i < nStatements; i++) {
            var boundStatement = BindStatement(syntaxStatements[i], diagnostics);
            boundStatements.Add(boundStatement);
        }

        return FinishBindBlockParts(node, boundStatements.ToImmutableAndFree());
    }

    private BoundBlockStatement FinishBindBlockParts(
        BelteSyntaxNode node,
        ImmutableArray<BoundStatement> boundStatements) {
        var locals = GetDeclaredLocalsForScope(node);
        var localFunctions = GetDeclaredLocalFunctionsForScope(node);
        return new BoundBlockStatement(node, boundStatements, locals, localFunctions);
    }

    private BoundReturnStatement BindReturnStatement(ReturnStatementSyntax node, BelteDiagnosticQueue diagnostics) {
        var expressionSyntax = node.expression.UnwrapRefExpression(out var refKind);
        BoundExpression argument = null;

        if (expressionSyntax is not null) {
            var requiredValueKind = GetRequiredReturnValueKind(refKind);
            argument = BindValue(expressionSyntax, diagnostics, requiredValueKind);
        }

        if (flags.Includes(BinderFlags.InWithTryBody))
            diagnostics.Push(Warning.ExitingControlFlowInWith(node.location));

        if (flags.Includes(BinderFlags.InFinallyBlock))
            diagnostics.Push(Error.CannotReturnFromFinally(node.location));

        if (flags.Includes(BinderFlags.InDeferBody))
            diagnostics.Push(Error.CannotReturnFromDefer(node.location));

        var returnType = GetCurrentReturnType(out var signatureRefKind);
        var hasErrors = false;

        if (returnType is not null &&
            refKind != RefKind.None != (signatureRefKind != RefKind.None) &&
            !argument.IsLiteralNull()) {
            if (refKind == RefKind.None)
                diagnostics.Push(Error.MustHaveRefReturn(node.keyword.location));
            else
                diagnostics.Push(Error.MustNotHaveRefReturn(node.keyword.location));

            hasErrors = true;
        }

        if (argument is not null) {
            // TODO Why was this enforced?
            // if (compilation.options.isScript && refKind != RefKind.None &&
            //     argument is BoundDataContainerExpression d && d.dataContainer.isGlobal) {
            //     diagnostics.Push(Error.RefReturnGlobal(expressionSyntax.location));
            //     hasErrors = true;
            // }

            hasErrors |= argument.type is not null && argument.type.IsErrorType();
        }

        if (hasErrors)
            return new BoundReturnStatement(node, refKind, argument, true);

        if (returnType is not null) {
            if (returnType.IsVoidType()) {
                if (argument is not null && containingMember is not SynthesizedEntryPoint) {
                    hasErrors = true;
                    diagnostics.Push(Error.UnexpectedReturnValue(node.keyword.location));
                    // TODO confirm this error has enough info, maybe include containingMember?
                }
            } else {
                if (argument is null) {
                    if (containingMember is not SynthesizedEntryPoint) {
                        hasErrors = true;
                        diagnostics.Push(Error.MissingReturnValue(node.keyword.location));
                    }
                } else {
                    argument = CreateReturnConversion(node, diagnostics, argument, signatureRefKind, returnType);
                }
            }
        } else {
            if (argument?.type is not null &&
                argument.type.IsVoidType() &&
                containingMember is not SynthesizedEntryPoint) {
                diagnostics.Push(Error.UnexpectedReturnValue(node.expression.location));
                hasErrors = true;
            }
        }

        return new BoundReturnStatement(
            node,
            refKind,
            hasErrors ? BindToTypeForErrorRecovery(argument) : argument,
            hasErrors
        );
    }

    private BoundExpressionStatement BindExpressionStatement(
        ExpressionStatementSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var expression = BindRValueWithoutTargetType(node.expression, diagnostics);

        if (!compilation.options.isScript) {
            if (IsInvalidExpressionStatement(expression))
                diagnostics.Push(Error.InvalidExpressionStatement(node.location));
        }

        return new BoundExpressionStatement(node, expression);

    }

    private static bool IsInvalidExpressionStatement(BoundExpression expression) {
        if (expression is BoundCompileTimeExpression cte)
            return IsInvalidExpressionStatement(cte.expression);

        switch (expression.kind) {
            case BoundKind.AssignmentOperator:
            case BoundKind.DeconstructionAssignmentOperator:
            case BoundKind.ErrorExpression:
            case BoundKind.CompoundAssignmentOperator:
            case BoundKind.ThrowExpression:
            case BoundKind.IncrementOperator:
            case BoundKind.NullCoalescingAssignmentOperator:
            case BoundKind.FunctionPointerCallExpression:
            case BoundKind.CompileTimeExpression:
            case BoundKind.CallExpression:
            case BoundKind.CascadeListExpression:
            case BoundKind.ReversibleExpression:
                return false;
            case BoundKind.ConditionalAccessExpression:
                var conditionalAccess = (BoundConditionalAccessExpression)expression;

                if (conditionalAccess.accessExpression.kind == BoundKind.CallExpression)
                    return false;

                return true;
            case BoundKind.ClampOperator:
                var clampOperator = (BoundClampOperator)expression;
                return !clampOperator.isAssignment;
            default:
                return true;
        }
    }

    private BoundDeferStatement BindDeferStatement(DeferStatementSyntax node, BelteDiagnosticQueue diagnostics) {
        var statement = BindPossibleEmbeddedStatement(node.statement, diagnostics);
        return new BoundDeferStatement(node, statement);
    }

    private BindValueKind GetRequiredReturnValueKind(RefKind refKind) {
        var requiredValueKind = BindValueKind.RValue;

        if (refKind != RefKind.None) {
            GetCurrentReturnType(out var signatureRefKind);
            requiredValueKind = signatureRefKind == RefKind.Ref ? BindValueKind.RefReturn : BindValueKind.RefConst;
        }

        return requiredValueKind;
    }

    private protected virtual TypeSymbol GetCurrentReturnType(out RefKind refKind) {
        if (containingMember is MethodSymbol symbol) {
            refKind = symbol.refKind;

            if (symbol is SourceStateMethodSymbol)
                return symbol.returnType.tupleElementTypes[1].type.type;

            return symbol.returnType;
        }

        refKind = RefKind.None;
        return null;
    }

    internal virtual BoundNode BindMethodBody(BelteSyntaxNode syntax, BelteDiagnosticQueue diagnostics) {
        switch (syntax) {
            case BaseMethodDeclarationSyntax method:
                if (method.kind == SyntaxKind.ConstructorDeclaration)
                    return BindConstructorBody((ConstructorDeclarationSyntax)method, diagnostics);

                return BindMethodBody(method, method.body, diagnostics);
            case ReverseClauseSyntax reverseMethod:
                return BindMethodBody(reverseMethod, reverseMethod.body, diagnostics);
            case StateClauseSyntax stateMethod:
                return BindMethodBody(stateMethod, stateMethod.body, diagnostics);
            case CompilationUnitSyntax compilationUnit:
                return BindSimpleProgram(compilationUnit, diagnostics);
            default:
                throw ExceptionUtilities.UnexpectedValue(syntax.kind);
        }
    }

    private BoundNode BindSimpleProgram(CompilationUnitSyntax compilationUnit, BelteDiagnosticQueue diagnostics) {
        return GetBinder(compilationUnit).BindSimpleProgramCompilationUnit(compilationUnit, diagnostics);
    }

    private BoundNode BindSimpleProgramCompilationUnit(
        CompilationUnitSyntax compilationUnit,
        BelteDiagnosticQueue diagnostics) {
        var boundStatements = ArrayBuilder<BoundStatement>.GetInstance();
        var first = true;

        foreach (var element in compilationUnit.elements) {
            if (element is GlobalStatementSyntax topLevelStatement) {
                if (first)
                    first = false;

                if (topLevelStatement.attributeLists?.Count > 0)
                    diagnostics.Push(Error.InvalidAttributes(topLevelStatement.attributeLists[0].location));

                if (topLevelStatement.modifiers?.Count > 0) {
                    foreach (var modifier in topLevelStatement.modifiers)
                        diagnostics.Push(Error.InvalidModifier(modifier.location, SyntaxFacts.GetText(modifier.kind)));
                }

                var boundStatement = BindStatement(topLevelStatement.statement, diagnostics);
                boundStatements.Add(boundStatement);
            }
        }

        return new BoundNonConstructorMethodBody(
            compilationUnit,
            FinishBindBlockParts(compilationUnit, boundStatements.ToImmutableAndFree())
        );
    }

    private BoundNode BindConstructorBody(ConstructorDeclarationSyntax constructor, BelteDiagnosticQueue diagnostics) {
        var initializer = constructor.constructorInitializer;
        var bodyBinder = GetBinder(constructor);

        var initializerCall = initializer is null
            ? bodyBinder.BindImplicitConstructorInitializer(constructor, diagnostics)
            : bodyBinder.BindConstructorInitializer(initializer, diagnostics);

        var body = (BoundBlockStatement)bodyBinder.BindStatement(constructor.body, diagnostics);
        var locals = bodyBinder.GetDeclaredLocalsForScope(constructor);

        return new BoundConstructorMethodBody(constructor, locals, initializerCall, body);
    }

    private BoundNode BindMethodBody(
        BelteSyntaxNode declaration,
        BlockStatementSyntax body,
        BelteDiagnosticQueue diagnostics) {
        if (body is null)
            return null;

        return new BoundNonConstructorMethodBody(declaration, (BoundBlockStatement)BindStatement(body, diagnostics));
    }
}
