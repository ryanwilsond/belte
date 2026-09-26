using System;
using System.Diagnostics;
using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Bound node, gets created from a <see cref="SyntaxNode" />.
/// </summary>
[DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
internal abstract class BoundNode {
    private protected BoundNode(BoundKind kind, SyntaxNode syntax, bool hasErrors) {
        this.kind = kind;
        this.hasErrors = hasErrors;
        this.syntax = syntax;
    }

    private protected BoundNode(BoundKind kind, SyntaxNode syntax) {
        this.kind = kind;
        this.syntax = syntax;
    }

    internal BoundKind kind { get; }

    internal bool hasErrors { get; }

    internal SyntaxNode syntax { get; }

    internal bool hasAnyErrors {
        get {
            if (hasErrors || (syntax is not null && syntax.containsDiagnostics))
                return true;

            var expression = this as BoundExpression;
            return expression?.type?.StrippedType()?.IsErrorType() == true;
        }
    }

    internal virtual BoundNode Accept(BoundTreeVisitor visitor) {
        throw new NotImplementedException();
    }

    internal static Conversion GetConversion(BoundExpression conversion, BoundValuePlaceholder placeholder) {
        switch (conversion) {
            case null:
                return Conversion.None;
            case BoundCastExpression boundConversion:
                if ((object)boundConversion.operand == placeholder)
                    return boundConversion.conversion;

                if (!boundConversion.conversion.isUserDefined)
                    boundConversion = (BoundCastExpression)boundConversion.operand;

                if (boundConversion.conversion.isUserDefined) {
                    BoundCastExpression next;

                    if ((object)boundConversion.operand == placeholder ||
                        (object)(next = (BoundCastExpression)boundConversion.operand).operand == placeholder ||
                        (object)((BoundCastExpression)next.operand).operand == placeholder) {
                        return boundConversion.conversion;
                    }
                }

                goto default;
            case BoundValuePlaceholder valuePlaceholder when (object)valuePlaceholder == placeholder:
                return Conversion.Identity;
            default:
                throw ExceptionUtilities.UnexpectedValue(conversion);
        }
    }

    public override string ToString() {
        return DisplayText.DisplayNode(this).ToString();
    }

    private string GetDebuggerDisplay() {
        return GetType().Name + " " + ToString();
    }
}
