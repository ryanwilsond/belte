using System.Diagnostics;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis;

internal static class MethodBodySynthesizer {
    internal static BoundBlockStatement ConstructAutoPropertyAccessorBody(SourceMemberMethodSymbol accessor) {
        Debug.Assert(accessor.methodKind is MethodKind.PropertyGet or MethodKind.PropertySet);

        var property = (SourcePropertySymbolBase)accessor.associatedSymbol;
        var syntax = property.belteSyntaxNode;
        BoundExpression thisReference = null;

        if (!accessor.isStatic) {
            var thisSymbol = accessor.thisParameter;
            thisReference = new BoundThisExpression(syntax, thisSymbol.type);
        }

        var field = property.backingField;
        var fieldAccess = new BoundFieldAccessExpression(
            syntax,
            thisReference,
            field,
            null,
            field.type
        );

        BoundStatement statement;

        if (accessor.methodKind == MethodKind.PropertyGet) {
            statement = new BoundReturnStatement(accessor.syntaxNode, RefKind.None, fieldAccess);
        } else {
            Debug.Assert(accessor.methodKind == MethodKind.PropertySet);
            var parameter = accessor.parameters[0];

            statement = new BoundExpressionStatement(
                accessor.syntaxNode,
                new BoundAssignmentOperator(
                    syntax,
                    fieldAccess,
                    new BoundParameterExpression(syntax, parameter, null, parameter.type),
                    isRef: false,
                    property.type
                )
            );
        }

        return new BoundBlockStatement(syntax, [statement], [], []);
    }
}
