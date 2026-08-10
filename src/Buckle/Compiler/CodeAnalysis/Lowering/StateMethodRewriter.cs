using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Libraries;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class StateMethodRewriter : BoundTreeExpander {
    private readonly TypeSymbol _returnType;
    private readonly NamedTypeSymbol _tupleType;
    private readonly DataContainerSymbol _returnTemp;
    private readonly LabelSymbol _successLabel;

    private readonly Compilation _compilation;

    private bool _isTarget;

    private StateMethodRewriter(
        Compilation compilation,
        MethodSymbol stateMethod,
        BoundBlockStatement targetBody,
        BoundBlockStatement stateBody) {
        _compilation = compilation;
        _returnType = stateMethod.returnType.tupleElementTypes[0].type.type;
        _tupleType = (NamedTypeSymbol)stateMethod.returnType;
        _container = stateMethod;

        _localNames.AddRange(targetBody.locals.Select(l => l.name));
        _localNames.AddRange(stateBody.locals.Select(l => l.name));

        if (_returnType.IsVoidType())
            _returnType = compilation.GetSpecialType(SpecialType.Int32);

        _returnTemp = GenerateTempLocal(_returnType);
        _successLabel = GenerateLabel("Success");
    }

    private protected override MethodSymbol _container { get; set; }

    internal static BoundBlockStatement Merge(
        Compilation compilation,
        MethodSymbol stateMethod,
        BoundBlockStatement targetBody,
        BoundBlockStatement stateBody) {
        var stateMethodRewriter = new StateMethodRewriter(compilation, stateMethod, targetBody, stateBody);

        var tempDeclaration = LocalDeclaration(stateBody.syntax, stateMethodRewriter._returnTemp, null);
        var successLabel = Label(stateBody.syntax, stateMethodRewriter._successLabel);

        var rewrittenTarget = stateMethodRewriter.Expand(targetBody, isTarget: true);
        var rewrittenState = stateMethodRewriter.Expand(stateBody, isTarget: false);

        return new BoundBlockStatement(
            stateBody.syntax,
            [
                tempDeclaration,
                .. rewrittenTarget.statements,
                successLabel,
                .. rewrittenState.statements,
            ],
            rewrittenTarget.locals.AddRange(rewrittenState.locals),
            rewrittenTarget.localFunctions.AddRange(rewrittenState.localFunctions)
        );
    }

    private BoundBlockStatement Expand(BoundBlockStatement block, bool isTarget) {
        _isTarget = isTarget;
        return (BoundBlockStatement)ExpandBlockStatement(block)[0];
    }

    private protected override List<BoundStatement> ExpandReturnStatement(BoundReturnStatement statement) {
        /*

        return <value>

        ----> is target method body

        returnTemp = <value>
        goto Success

        ----> is state method body

        return (returnTemp, <value>)

        */
        var syntax = statement.syntax;

        if (_isTarget) {
            return [
                Statement(syntax, Assignment(syntax,
                    Local(syntax, _returnTemp),
                    statement.expression is null
                        ? Literal(_compilation, syntax, 0, _returnType)
                        : statement.expression,
                    false,
                    _returnType
                )),
                Goto(syntax, _successLabel)
            ];
        } else {
            var tupleCtor = _compilation.corLibrary.GetWellKnownMethod(WellKnownMember.ValueTuple_T2_ctor)
                .AsMember(_tupleType);

            return [
                new BoundReturnStatement(syntax,
                    RefKind.None,
                    new BoundObjectCreationExpression(syntax,
                        tupleCtor,
                        [Local(syntax, _returnTemp), statement.expression],
                        [],
                        [],
                        default,
                        false,
                        _tupleType
                    )
                )
            ];
        }
    }
}
