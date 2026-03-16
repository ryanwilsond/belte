using System;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class BinderFactory {
    internal sealed class BinderFactoryVisitor : SyntaxVisitor<Binder> {
        private int _position;
        private BelteSyntaxNode _memberDeclaration;
        private Symbol _member;
        private BinderFactory _factory;

        internal void Initialize(
            BinderFactory factory,
            int position,
            BelteSyntaxNode memberDeclaration,
            Symbol member) {
            _factory = factory;
            _position = position;
            _memberDeclaration = memberDeclaration;
            _member = member;
        }

        internal void Clear() {
            _factory = null;
            _position = 0;
            _memberDeclaration = null;
            _member = null;
        }

        private Compilation _compilation => _factory._compilation;

        private SyntaxTree _syntaxTree => _factory.syntaxTree;

        private ConcurrentCache<BinderCacheKey, Binder> _binderCache => _factory._binderCache;

        private EndBinder _endBinder => _factory._endBinder;

        private bool _inScript => _factory.inScript;

        internal static BinderCacheKey CreateBinderCacheKey(BelteSyntaxNode node, NodeUsage usage) {
            return new BinderCacheKey(node, usage);
        }

        internal override Binder DefaultVisit(SyntaxNode parent) {
            return ((BelteSyntaxNode)parent).parent.Accept(this);
        }

        internal override Binder Visit(SyntaxNode node) {
            return ((BelteSyntaxNode)node).Accept(this);
        }

        private Binder VisitCore(SyntaxNode node) {
            return ((BelteSyntaxNode)node).Accept(this);
        }

        internal override Binder VisitCompilationUnit(CompilationUnitSyntax node) {
            return VisitCompilationUnit(node, IsInUsing(node));
        }

        internal Binder VisitCompilationUnit(CompilationUnitSyntax node, bool inUsing) {
            if (node != _syntaxTree.GetRoot())
                throw new ArgumentOutOfRangeException(nameof(node), "node is not apart of the tree");

            // TODO We should probably set this up in case the compilation bounces from scripts to normal
            // var key = new BinderCacheKey(node, _inScript ? NodeUsage.CompilationUnitScript : NodeUsage.Normal);
            var key = new BinderCacheKey(node, NodeUsage.Normal);

            if (!_binderCache.TryGetValue(key, out var result)) {
                // TODO The SubmissionBinder is what fetches types from previous compilations (so the CorLibrary)
                // So is this fine as is, or should there still be some _inScript check
                result = new SubmissionBinder(_compilation.globalNamespaceInternal, node, _endBinder);
                result = AddInImportsBinders(
                    (SourceNamespaceSymbol)_compilation.sourceModule.globalNamespace,
                    node,
                    result,
                    inUsing
                );
                result = new InContainerBinder(_compilation.globalNamespaceInternal, result);

                if (_inScript)
                    result = result.WithAdditionalFlags(BinderFlags.IgnoreAccessibility);

                if (SynthesizedEntryPoint.GetSimpleProgramEntryPoint(_compilation, node, fallbackToMainEntryPoint: true)
                    is SynthesizedEntryPoint simpleProgram) {
                    var bodyBinder = simpleProgram.GetBodyBinder(_factory._ignoreAccessibility);
                    result = new SimpleProgramUnitBinder(
                        result,
                        (SimpleProgramBinder)bodyBinder.GetBinder(simpleProgram.syntaxNode)
                    );
                }

                _binderCache.TryAdd(key, result);
            }

            return result;
        }

        private static Binder AddInImportsBinders(
            SourceNamespaceSymbol declaringSymbol,
            BelteSyntaxNode declarationSyntax,
            Binder next,
            bool inUsing) {
            if (inUsing) {
                return next;
            } else {
                return WithUsingAliasesBinder.Create(
                    declaringSymbol,
                    declarationSyntax,
                    WithUsingNamespacesAndTypesBinder.Create(declaringSymbol, declarationSyntax, next)
                );
            }
        }

        internal override Binder VisitNamespaceDeclaration(NamespaceDeclarationSyntax parent) {
            if (!SyntaxFacts.IsInNamespaceDeclaration(_position, parent))
                return VisitCore(parent.parent);

            var inBody = SyntaxFacts.IsBetweenTokens(_position, parent.openBrace, parent.closeBrace);
            var inUsing = IsInUsing(parent);

            return VisitNamespaceDeclaration(parent, _position, inBody, inUsing);
        }

        internal Binder VisitNamespaceDeclaration(
            BaseNamespaceDeclarationSyntax parent,
            int position,
            bool inBody,
            bool inUsing) {

            var extraInfo = inUsing ? NodeUsage.NamespaceUsings : (inBody ? NodeUsage.NamespaceBody : NodeUsage.Normal);
            var key = CreateBinderCacheKey(parent, extraInfo);

            if (!_binderCache.TryGetValue(key, out var result)) {
                Binder outer;
                outer = _factory.GetBinder(parent.parent, position);

                if (!inBody)
                    result = outer;
                else
                    result = MakeNamespaceBinder(parent, parent.name, outer, inUsing);

                _binderCache.TryAdd(key, result);
            }

            return result;
        }

        private static Binder MakeNamespaceBinder(BelteSyntaxNode node, NameSyntax name, Binder outer, bool inUsing) {
            if (name is QualifiedNameSyntax dotted) {
                outer = MakeNamespaceBinder(dotted.left, dotted.left, outer, inUsing: false);
                name = dotted.right;
            }

            NamespaceOrTypeSymbol container;

            if (outer is InContainerBinder inContainerBinder)
                container = inContainerBinder.container;
            else
                container = outer.compilation.globalNamespaceInternal;

            var ns = ((NamespaceSymbol)container).GetNestedNamespace(name);

            if (ns is null)
                return outer;

            if (node is BaseNamespaceDeclarationSyntax namespaceDecl) {
                outer = AddInImportsBinders(
                    (SourceNamespaceSymbol)outer.compilation.sourceModule.GetModuleNamespace(ns),
                    namespaceDecl,
                    outer,
                    inUsing
                );
            }

            return new InContainerBinder(ns, outer);
        }

        private bool IsInUsing(BelteSyntaxNode containingNode) {
            var containingSpan = containingNode.span;

            SyntaxToken token;

            if (containingNode.kind != SyntaxKind.CompilationUnit && _position == containingSpan.end)
                token = containingNode.GetLastToken();
            else if (_position < containingSpan.start || _position > containingSpan.end)
                return false;
            else
                token = containingNode.FindToken(_position);

            var node = token.parent;

            while (node is not null && node != containingNode) {
                if (node.kind == SyntaxKind.UsingDirective && node.parent == containingNode)
                    return true;

                node = node.parent;
            }

            return false;
        }

        internal override Binder VisitGlobalStatement(GlobalStatementSyntax node) {
            if (SyntaxFacts.IsSimpleProgramTopLevelStatement(node)) {
                var compilationUnit = (CompilationUnitSyntax)node.parent;

                if (compilationUnit != _syntaxTree.GetRoot())
                    throw new ArgumentOutOfRangeException(nameof(node), "node not part of tree");

                var key = CreateBinderCacheKey(compilationUnit, NodeUsage.MethodBody);

                if (!_binderCache.TryGetValue(key, out var result)) {
                    var simpleProgram = SynthesizedEntryPoint.GetSimpleProgramEntryPoint(
                        _compilation,
                        (CompilationUnitSyntax)node.parent,
                        false
                    );

                    var bodyBinder = simpleProgram.GetBodyBinder(_factory._ignoreAccessibility || _inScript);
                    result = bodyBinder.GetBinder(compilationUnit);
                    _binderCache.TryAdd(key, result);
                }

                return result;
            }

            return base.VisitGlobalStatement(node);
        }

        private Binder VisitTypeDeclarationCore(TypeDeclarationSyntax node) {
            if (!LookupPosition.IsInTypeDeclaration(_position, node))
                return VisitCore(node.parent);

            var nodeUsage = NodeUsage.Normal;

            if (node.openBrace != default &&
                node.closeBrace != default &&
                LookupPosition.IsBetweenTokens(_position, node.openBrace, node.closeBrace)) {
                nodeUsage = NodeUsage.NamedTypeBodyOrTemplateParameters;
            } else if (LookupPosition.IsInTemplateParameterList(_position, node)) {
                nodeUsage = NodeUsage.NamedTypeBodyOrTemplateParameters;
            } else if (LookupPosition.IsBetweenTokens(_position, node.keyword, node.openBrace)) {
                nodeUsage = NodeUsage.NamedTypeBase;
            }

            return VisitTypeDeclarationCore(node, nodeUsage);
        }

        internal Binder VisitTypeDeclarationCore(TypeDeclarationSyntax node, NodeUsage nodeUsage) {
            var key = CreateBinderCacheKey(node, nodeUsage);

            if (!_binderCache.TryGetValue(key, out var resultBinder)) {
                resultBinder = VisitCore(node.parent);

                if (nodeUsage != NodeUsage.Normal) {
                    var typeSymbol = ((NamespaceOrTypeSymbol)resultBinder.containingMember).GetSourceTypeMember(node);

                    if (nodeUsage == NodeUsage.NamedTypeBase) {
                        resultBinder = new WithClassTemplateParametersBinder(typeSymbol, resultBinder);
                    } else {
                        resultBinder = new InContainerBinder(typeSymbol, resultBinder);

                        if (node.templateParameterList is not null)
                            resultBinder = new WithClassTemplateParametersBinder(typeSymbol, resultBinder);
                    }
                }

                _binderCache.TryAdd(key, resultBinder);
            }

            return resultBinder;
        }

        internal override Binder VisitMethodDeclaration(MethodDeclarationSyntax node) {
            if (!LookupPosition.IsInMethodDeclaration(_position, node))
                return VisitCore(node.parent);

            NodeUsage usage;

            if (LookupPosition.IsInBody(_position, node))
                usage = NodeUsage.MethodBody;
            else if (LookupPosition.IsInMethodTemplateParameterScope(_position, node))
                usage = NodeUsage.MethodTemplateParameters;
            else
                usage = NodeUsage.Normal;

            var key = CreateBinderCacheKey(node, usage);

            if (!_binderCache.TryGetValue(key, out var resultBinder)) {
                var parentType = node.parent as TypeDeclarationSyntax;

                if (parentType is not null)
                    resultBinder = VisitTypeDeclarationCore(parentType, NodeUsage.NamedTypeBodyOrTemplateParameters);
                else
                    resultBinder = VisitCore(node.parent);

                SourceMemberMethodSymbol method = null;

                if (usage != NodeUsage.Normal && node.templateParameterList is not null) {
                    method = GetMethodSymbol(node, resultBinder);
                    resultBinder = new WithMethodTemplateParametersBinder(method, resultBinder);
                }

                if (usage == NodeUsage.MethodBody) {
                    method ??= GetMethodSymbol(node, resultBinder);
                    resultBinder = new InMethodBinder(method, resultBinder);

                    if (method.isEffectivelyConst)
                        resultBinder = resultBinder.WithAdditionalFlags(BinderFlags.ConstContext);
                }

                _binderCache.TryAdd(key, resultBinder);
            }

            return resultBinder;
        }

        private SourceMemberMethodSymbol GetMethodSymbol(BaseMethodDeclarationSyntax baseMethodDeclarationSyntax, Binder outerBinder) {
            if (baseMethodDeclarationSyntax == _memberDeclaration)
                return (SourceMemberMethodSymbol)_member;

            var container = GetContainerType(outerBinder);

            if (container is null)
                return null;

            var methodName = GetMethodName(baseMethodDeclarationSyntax);
            return (SourceMemberMethodSymbol)GetMemberSymbol(methodName, baseMethodDeclarationSyntax.fullSpan, container, SymbolKind.Method);
        }

        private NamedTypeSymbol GetContainerType(Binder binder) {
            var containingSymbol = binder.containingMember;

            if (containingSymbol is not NamedTypeSymbol container)
                container = ((NamespaceSymbol)containingSymbol).implicitType;

            return container;
        }

        private Symbol GetMemberSymbol(
            string memberName,
            TextSpan memberSpan,
            NamedTypeSymbol container,
            SymbolKind kind) {
            foreach (var sym in container.GetMembers(memberName)) {
                if (CheckSymbol(sym, memberSpan, kind, out var result))
                    return result;
            }

            return null;

            bool CheckSymbol(Symbol sym, TextSpan memberSpan, SymbolKind kind, out Symbol result) {
                result = sym;

                if (sym.kind != kind)
                    return false;

                var syntaxReference = sym.syntaxReference;

                if (kind is SymbolKind.Method) {

                    if (InSpan(syntaxReference.location, syntaxReference.syntaxTree, _syntaxTree, memberSpan))
                        return true;
                } else if (InSpan(syntaxReference.location, syntaxReference.syntaxTree, _syntaxTree, memberSpan)) {
                    return true;
                }

                return false;
            }
        }

        private static bool InSpan(
            TextLocation location,
            SyntaxTree firstSyntaxTree,
            SyntaxTree secondSyntaxTree,
            TextSpan span) {
            return (firstSyntaxTree == secondSyntaxTree) && span.Contains(location.span);
        }

        private static string GetMethodName(BaseMethodDeclarationSyntax syntax) {
            switch (syntax.kind) {
                case SyntaxKind.ConstructorDeclaration:
                    return WellKnownMemberNames.InstanceConstructorName;
                case SyntaxKind.OperatorDeclaration:
                    var operatorDeclaration = (OperatorDeclarationSyntax)syntax;
                    return SyntaxFacts.GetOperatorMemberName(operatorDeclaration);
                case SyntaxKind.MethodDeclaration:
                    var methodDeclSyntax = (MethodDeclarationSyntax)syntax;
                    return methodDeclSyntax.identifier.text;
                default:
                    throw ExceptionUtilities.UnexpectedValue(syntax.kind);
            }
        }

        internal override Binder VisitConstructorDeclaration(ConstructorDeclarationSyntax node) {
            if (!LookupPosition.IsInMethodDeclaration(_position, node))
                return VisitCore(node.parent);

            var inBodyOrInitializer = LookupPosition.IsInConstructorParameterScope(_position, node);
            var nodeUsage = inBodyOrInitializer ? NodeUsage.ConstructorBodyOrInitializer : NodeUsage.Normal;
            var key = CreateBinderCacheKey(node, nodeUsage);

            if (!_binderCache.TryGetValue(key, out var resultBinder)) {
                resultBinder = VisitCore(node.parent);

                if (inBodyOrInitializer) {
                    var method = GetMethodSymbol(node, resultBinder);

                    if (method is not null)
                        resultBinder = new InMethodBinder(method, resultBinder);
                }

                _binderCache.TryAdd(key, resultBinder);
            }

            return resultBinder;
        }

        internal override Binder VisitOperatorDeclaration(OperatorDeclarationSyntax node) {
            if (!LookupPosition.IsInMethodDeclaration(_position, node))
                return VisitCore(node.parent);

            var inBody = LookupPosition.IsInBody(_position, node);
            var nodeUsage = inBody ? NodeUsage.OperatorBody : NodeUsage.Normal;
            var key = CreateBinderCacheKey(node, nodeUsage);

            if (!_binderCache.TryGetValue(key, out var resultBinder)) {
                resultBinder = VisitCore(node.parent);

                var method = GetMethodSymbol(node, resultBinder);

                if (method is not null && inBody)
                    resultBinder = new InMethodBinder(method, resultBinder);

                _binderCache.TryAdd(key, resultBinder);
            }

            return resultBinder;
        }

        internal override Binder VisitFieldDeclaration(FieldDeclarationSyntax node) {
            return VisitCore(node.parent);
        }

        internal override Binder VisitClassDeclaration(ClassDeclarationSyntax node) {
            return VisitTypeDeclarationCore(node);
        }

        internal override Binder VisitStructDeclaration(StructDeclarationSyntax node) {
            return VisitTypeDeclarationCore(node);
        }
    }
}
