using System.Collections.Generic;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class TypeofBinder : Binder {
    private readonly Dictionary<TemplateNameSyntax, bool> _allowedMap;

    internal TypeofBinder(ExpressionSyntax typeExpression, Binder next) : base(next, next.flags) {
        OpenTypeVisitor.Visit(typeExpression, out _allowedMap);
    }

    private protected override bool IsUnboundTypeAllowed(TemplateNameSyntax syntax) {
        return _allowedMap is not null && _allowedMap.TryGetValue(syntax, out var allowed) && allowed;
    }
}
