using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using CoreInternalSyntax = Buckle.CodeAnalysis.Syntax.InternalSyntax;

namespace Buckle.CodeAnalysis;

using BoxedMemberNames = StrongBox<ImmutableSegmentedHashSet<string>>;

internal sealed class DeclarationTreeBuilder : SyntaxVisitor<SingleNamespaceOrTypeDeclaration> {
    private static readonly ConditionalWeakTable<GreenNode, BoxedMemberNames> NodeToMemberNames = [];
    private static readonly BoxedMemberNames EmptyMemberNames
        = new BoxedMemberNames(ImmutableSegmentedHashSet<string>.Empty);

    private readonly SyntaxTree _syntaxTree;
    private readonly string _scriptClassName;
    private readonly bool _isSubmission;
    private readonly OneOrMany<WeakReference<BoxedMemberNames>> _previousMemberNames;

    private int _currentTypeIndex;

    private DeclarationTreeBuilder(
        SyntaxTree syntaxTree,
        OneOrMany<WeakReference<BoxedMemberNames>> previousMemberNames) {
        _syntaxTree = syntaxTree;
        _scriptClassName = "";
        _isSubmission = false;
        _previousMemberNames = previousMemberNames;
    }

    internal static RootSingleNamespaceDeclaration ForTree(
        SyntaxTree syntaxTree,
        OneOrMany<WeakReference<BoxedMemberNames>>? previousMemberNames = null) {
        var builder = new DeclarationTreeBuilder(
            syntaxTree,
            previousMemberNames ?? OneOrMany<WeakReference<BoxedMemberNames>>.Empty);
        return (RootSingleNamespaceDeclaration)builder.Visit(syntaxTree.GetRoot());
    }

    internal static bool CachesComputedMemberNames(SingleTypeDeclaration typeDeclaration) {
        return typeDeclaration.kind switch {
            DeclarationKind.Namespace => throw ExceptionUtilities.Unreachable(),
            DeclarationKind.Class or
            DeclarationKind.ImplicitClass or
            DeclarationKind.Struct => true,
            _ => throw ExceptionUtilities.UnexpectedValue(typeDeclaration.kind)
        };
    }

    private ImmutableArray<SingleNamespaceOrTypeDeclaration> VisitNamespaceChildren(
        BelteSyntaxNode node,
        SyntaxList<MemberDeclarationSyntax> members,
        CoreInternalSyntax.SyntaxList<CoreInternalSyntax.MemberDeclarationSyntax> internalMembers) {
        if (members.Count == 0)
            return [];

        var hasGlobalMembers = false;
        var acceptSimpleProgram = node.kind == SyntaxKind.CompilationUnit /*&& _syntaxTree.kind == SourceCodeKind.Regular*/;
        var hasAwaitExpressions = false;
        var isIterator = false;
        var hasReturnWithExpression = false;
        GlobalStatementSyntax firstGlobalStatement = null;
        var hasNonEmptyGlobalStatement = false;

        var childrenBuilder = ArrayBuilder<SingleNamespaceOrTypeDeclaration>.GetInstance();
        foreach (var member in members) {
            var namespaceOrType = Visit(member);

            if (namespaceOrType is not null) {
                childrenBuilder.Add(namespaceOrType);
            } else if (acceptSimpleProgram && member.kind == SyntaxKind.GlobalStatement) {
                var global = (GlobalStatementSyntax)member;
                firstGlobalStatement ??= global;
                var topLevelStatement = global.statement;

                if (topLevelStatement.kind != SyntaxKind.EmptyStatement)
                    hasNonEmptyGlobalStatement = true;

                if (!hasReturnWithExpression)
                    hasReturnWithExpression = SyntaxFacts.HasReturnWithExpression(topLevelStatement);

                // TODO Incomplete member?
            } else if (!hasGlobalMembers /*&& member.kind != SyntaxKind.IncompleteMember*/) {
                hasGlobalMembers = true;
            }
        }

        if (firstGlobalStatement is not null) {
            var diagnostics = ImmutableArray<BelteDiagnostic>.Empty;

            if (!hasNonEmptyGlobalStatement) {
                // TODO error
                var queue = BelteDiagnosticQueue.GetInstance();
                // bag.Add(ErrorCode.ERR_SimpleProgramIsEmpty, ((EmptyStatementSyntax)firstGlobalStatement.Statement).SemicolonToken.GetLocation());
                diagnostics = queue.ToImmutableAndFree();
            }

            childrenBuilder.Add(CreateSimpleProgram(firstGlobalStatement, hasReturnWithExpression, diagnostics));
        }

        if (hasGlobalMembers) {
            var declFlags = SingleTypeDeclaration.TypeDeclarationFlags.None;
            var memberNames = GetNonTypeMemberNames(node, internalMembers, ref declFlags, skipGlobalStatements: acceptSimpleProgram);
            var container = new SyntaxReference(node);

            childrenBuilder.Add(CreateImplicitClass(memberNames, container, declFlags));
        }

        return childrenBuilder.ToImmutableAndFree();
    }

    private static SingleNamespaceOrTypeDeclaration CreateImplicitClass(
        BoxedMemberNames memberNames,
        SyntaxReference container,
        SingleTypeDeclaration.TypeDeclarationFlags declFlags) {
        return new SingleTypeDeclaration(
            kind: DeclarationKind.ImplicitClass,
            name: "",
            arity: 0,
            modifiers: DeclarationModifiers.Public | DeclarationModifiers.Sealed,
            declFlags: declFlags,
            syntaxReference: container,
            nameLocation: container.location,
            memberNames: memberNames,
            children: [],
            diagnostics: []
        );
    }

    private static SingleNamespaceOrTypeDeclaration CreateSimpleProgram(
        GlobalStatementSyntax firstGlobalStatement,
        bool hasReturnWithExpression,
        ImmutableArray<BelteDiagnostic> diagnostics) {
        var nameLocation = firstGlobalStatement.GetFirstToken().location;

        // TODO Our system doesn't allow a check like this
        // if (nameLocation.sourceTree is null) {
        //     nameLocation = new SourceLocation(firstGlobalStatement.GetFirstToken(includeSkipped: true));
        // }

        return new SingleTypeDeclaration(
            kind: DeclarationKind.Class,
            name: WellKnownMemberNames.TopLevelStatementsEntryPointTypeName,
            arity: 0,
            modifiers: DeclarationModifiers.None,
            declFlags: (hasReturnWithExpression
                            ? SingleTypeDeclaration.TypeDeclarationFlags.HasReturnWithExpression
                            : SingleTypeDeclaration.TypeDeclarationFlags.None) |
                       SingleTypeDeclaration.TypeDeclarationFlags.IsSimpleProgram,
            syntaxReference: new SyntaxReference(firstGlobalStatement.parent),
            nameLocation: nameLocation,
            memberNames: EmptyMemberNames,
            children: [],
            diagnostics: diagnostics
        );
    }

    private RootSingleNamespaceDeclaration CreateScriptRootDeclaration(CompilationUnitSyntax compilationUnit) {
        var members = compilationUnit.members;
        var rootChildren = ArrayBuilder<SingleNamespaceOrTypeDeclaration>.GetInstance();
        var scriptChildren = ArrayBuilder<SingleTypeDeclaration>.GetInstance();

        foreach (var member in members) {
            var decl = Visit(member);

            if (decl is not null) {
                if (decl.kind == DeclarationKind.Namespace)
                    rootChildren.Add(decl);
                else
                    scriptChildren.Add((SingleTypeDeclaration)decl);
            }
        }

        var declFlags = SingleTypeDeclaration.TypeDeclarationFlags.None;
        var memberNames = GetNonTypeMemberNames(
            compilationUnit,
            ((CoreInternalSyntax.CompilationUnitSyntax)compilationUnit.green).members,
            ref declFlags
        );

        rootChildren.Add(
            CreateScriptClass(
                compilationUnit,
                scriptChildren.ToImmutableAndFree(),
                memberNames,
                declFlags
            )
        );

        return CreateRootSingleNamespaceDeclaration(
            compilationUnit,
            rootChildren.ToImmutableAndFree(),
            isForScript: true
        );
    }

    private SingleNamespaceOrTypeDeclaration CreateScriptClass(
        CompilationUnitSyntax parent,
        ImmutableArray<SingleTypeDeclaration> children,
        BoxedMemberNames memberNames,
        SingleTypeDeclaration.TypeDeclarationFlags declFlags) {
        var parentReference = new SyntaxReference(parent);
        var fullName = _scriptClassName.Split('.');

        SingleNamespaceOrTypeDeclaration decl = new SingleTypeDeclaration(
            kind: _isSubmission ? DeclarationKind.Submission : DeclarationKind.Script,
            name: fullName.Last(),
            arity: 0,
            modifiers: DeclarationModifiers.Public | DeclarationModifiers.Sealed,
            declFlags: declFlags,
            syntaxReference: parentReference,
            nameLocation: parentReference.location,
            memberNames: memberNames,
            children: children,
            diagnostics: []
        );

        for (var i = fullName.Length - 2; i >= 0; i--) {
            decl = SingleNamespaceDeclaration.Create(
                name: fullName[i],
                hasUsings: false,
                hasExternAliases: false,
                syntaxReference: parentReference,
                nameLocation: parentReference.location,
                children: [decl],
                diagnostics: []
            );
        }

        return decl;
    }

    internal override SingleNamespaceOrTypeDeclaration VisitCompilationUnit(CompilationUnitSyntax compilationUnit) {
        // TODO The REPL kind of just assumes the compiler treats the submissions as regular
        // TODO And to compensate, the compiler assumes it might be compiling scripts at any time
        // TODO Perhaps this is not optimal
        // if (_syntaxTree.kind != SourceCodeKind.Regular) {
        //     return CreateScriptRootDeclaration(compilationUnit);
        // }
        var children = VisitNamespaceChildren(
            compilationUnit,
            compilationUnit.members,
            ((CoreInternalSyntax.CompilationUnitSyntax)compilationUnit.green).members
        );

        return CreateRootSingleNamespaceDeclaration(compilationUnit, children, isForScript: false);
    }

    private RootSingleNamespaceDeclaration CreateRootSingleNamespaceDeclaration(
        CompilationUnitSyntax compilationUnit,
        ImmutableArray<SingleNamespaceOrTypeDeclaration> children,
        bool isForScript) {
        var hasUsings = false;
        var hasGlobalUsings = false;
        var reportedGlobalUsingOutOfOrder = false;

        var diagnostics = BelteDiagnosticQueue.GetInstance();

        // foreach (var directive in compilationUnit.Usings) {
        //     if (directive.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword)) {
        //         hasGlobalUsings = true;

        //         if (hasUsings && !reportedGlobalUsingOutOfOrder) {
        //             reportedGlobalUsingOutOfOrder = true;
        //             diagnostics.Add(ErrorCode.ERR_GlobalUsingOutOfOrder, directive.GlobalKeyword.GetLocation());
        //         }
        //     } else {
        //         hasUsings = true;
        //     }
        // }

        // var globalAliasedQuickAttributes = GetQuickAttributes(compilationUnit.Usings, global: true);

        return new RootSingleNamespaceDeclaration(
            hasGlobalUsings: hasGlobalUsings,
            hasUsings: hasUsings,
            // hasExternAliases: compilationUnit.Externs.Any(),
            hasExternAliases: false,
            treeNode: new SyntaxReference(compilationUnit),
            children: children,
            // referenceDirectives: isForScript ? GetReferenceDirectives(compilationUnit) : ImmutableArray<ReferenceDirective>.Empty,
            diagnostics: diagnostics.ToImmutableAndFree()
        );
    }

    // internal override SingleNamespaceOrTypeDeclaration VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
    //     => this.VisitBaseNamespaceDeclaration(node);

    // private SingleNamespaceDeclaration VisitBaseNamespaceDeclaration(BaseNamespaceDeclarationSyntax node) {
    //     var children = VisitNamespaceChildren(node, node.Members, ((Syntax.InternalSyntax.BaseNamespaceDeclarationSyntax)node.Green).Members);

    //     bool hasUsings = node.Usings.Any();
    //     bool hasExterns = node.Externs.Any();
    //     NameSyntax name = node.Name;
    //     CSharpSyntaxNode currentNode = node;
    //     while (name is QualifiedNameSyntax dotted) {
    //         var ns = SingleNamespaceDeclaration.Create(
    //             name: dotted.Right.Identifier.ValueText,
    //             hasUsings: hasUsings,
    //             hasExternAliases: hasExterns,
    //             syntaxReference: _syntaxTree.GetReference(currentNode),
    //             nameLocation: new SourceLocation(dotted.Right),
    //             children: children,
    //             diagnostics: ImmutableArray<Diagnostic>.Empty);

    //         children = ImmutableArray.Create<SingleNamespaceOrTypeDeclaration>(ns);
    //         currentNode = name = dotted.Left;
    //         hasUsings = false;
    //         hasExterns = false;
    //     }

    //     var diagnostics = DiagnosticBag.GetInstance();

    //     if (node is FileScopedNamespaceDeclarationSyntax) {
    //         MessageID.IDS_FeatureFileScopedNamespace.CheckFeatureAvailability(diagnostics, node, node.NamespaceKeyword.GetLocation());

    //         if (node.Parent is FileScopedNamespaceDeclarationSyntax) {
    //             // Happens when user writes:
    //             //      namespace A.B;
    //             //      namespace X.Y;
    //             diagnostics.Add(ErrorCode.ERR_MultipleFileScopedNamespace, node.Name.GetLocation());
    //         } else if (node.Parent is NamespaceDeclarationSyntax) {
    //             // Happens with:
    //             //
    //             //      namespace A.B
    //             //      {
    //             //          namespace X.Y;
    //             diagnostics.Add(ErrorCode.ERR_FileScopedAndNormalNamespace, node.Name.GetLocation());
    //         } else {
    //             // Happens with cases like:
    //             //
    //             //      namespace A.B { }
    //             //      namespace X.Y;
    //             //
    //             // or even
    //             //
    //             //      class C { }
    //             //      namespace X.Y;

    //             Debug.Assert(node.Parent is CompilationUnitSyntax);
    //             var compilationUnit = (CompilationUnitSyntax)node.Parent;
    //             if (node != compilationUnit.Members[0]) {
    //                 diagnostics.Add(ErrorCode.ERR_FileScopedNamespaceNotBeforeAllMembers, node.Name.GetLocation());
    //             }
    //         }
    //     } else {
    //         Debug.Assert(node is NamespaceDeclarationSyntax);

    //         //      namespace X.Y;
    //         //      namespace A.B { }
    //         if (node.Parent is FileScopedNamespaceDeclarationSyntax) {
    //             diagnostics.Add(ErrorCode.ERR_FileScopedAndNormalNamespace, node.Name.GetLocation());
    //         }
    //     }

    //     if (ContainsGeneric(node.Name)) {
    //         // We're not allowed to have generics.
    //         diagnostics.Add(ErrorCode.ERR_UnexpectedGenericName, node.Name.GetLocation());
    //     }

    //     if (ContainsAlias(node.Name)) {
    //         diagnostics.Add(ErrorCode.ERR_UnexpectedAliasedName, node.Name.GetLocation());
    //     }

    //     if (node.AttributeLists.Count > 0) {
    //         diagnostics.Add(ErrorCode.ERR_BadModifiersOnNamespace, node.AttributeLists[0].GetLocation());
    //     }

    //     if (node.Modifiers.Count > 0) {
    //         diagnostics.Add(ErrorCode.ERR_BadModifiersOnNamespace, node.Modifiers[0].GetLocation());
    //     }

    //     foreach (var directive in node.Usings) {
    //         if (directive.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword)) {
    //             diagnostics.Add(ErrorCode.ERR_GlobalUsingInNamespace, directive.GlobalKeyword.GetLocation());
    //             break;
    //         }
    //     }

    //     CheckFeatureAvailabilityForUsings(diagnostics, node.Usings);
    //     CheckFeatureAvailabilityForExterns(diagnostics, node.Externs);

    //     // NOTE: *Something* has to happen for alias-qualified names.  It turns out that we
    //     // just grab the part after the colons (via GetUnqualifiedName, below).  This logic
    //     // must be kept in sync with NamespaceSymbol.GetNestedNamespace.
    //     return SingleNamespaceDeclaration.Create(
    //         name: name.GetUnqualifiedName().Identifier.ValueText,
    //         hasUsings: hasUsings,
    //         hasExternAliases: hasExterns,
    //         syntaxReference: _syntaxTree.GetReference(currentNode),
    //         nameLocation: new SourceLocation(name),
    //         children: children,
    //         diagnostics: diagnostics.ToReadOnlyAndFree());
    // }

    private static bool ContainsAlias(NameSyntax name) {
        switch (name.kind) {
            case SyntaxKind.TemplateName:
                return false;
            // case SyntaxKind.AliasQualifiedName:
            //     return true;
            case SyntaxKind.QualifiedName:
                var qualifiedName = (QualifiedNameSyntax)name;
                return ContainsAlias(qualifiedName.left);
        }

        return false;
    }

    private static bool ContainsTemplate(NameSyntax name) {
        switch (name.kind) {
            case SyntaxKind.TemplateName:
                return true;
            // case SyntaxKind.AliasQualifiedName:
            //     return ContainsGeneric(((AliasQualifiedNameSyntax)name).Name);
            case SyntaxKind.QualifiedName:
                var qualifiedName = (QualifiedNameSyntax)name;
                return ContainsTemplate(qualifiedName.left) || ContainsTemplate(qualifiedName.right);
        }

        return false;
    }

    internal override SingleNamespaceOrTypeDeclaration VisitClassDeclaration(ClassDeclarationSyntax node) {
        return VisitTypeDeclaration(node, DeclarationKind.Class);
    }

    internal override SingleNamespaceOrTypeDeclaration VisitStructDeclaration(StructDeclarationSyntax node) {
        return VisitTypeDeclaration(node, DeclarationKind.Struct);
    }

    private SingleTypeDeclaration VisitTypeDeclaration(TypeDeclarationSyntax node, DeclarationKind kind) {
        // var declFlags = node.attributeLists.Any()
        //     ? SingleTypeDeclaration.TypeDeclarationFlags.HasAnyAttributes
        //     : SingleTypeDeclaration.TypeDeclarationFlags.None;
        var declFlags = SingleTypeDeclaration.TypeDeclarationFlags.None;

        if (node is ClassDeclarationSyntax cds && cds.baseType is not null)
            declFlags |= SingleTypeDeclaration.TypeDeclarationFlags.HasBaseDeclarations;

        var diagnostics = BelteDiagnosticQueue.GetInstance();

        if (node.arity == 0) {
            // TODO error
            // Symbol.ReportErrorIfHasConstraints(node.ConstraintClauses, diagnostics);
        }

        var memberNames = GetNonTypeMemberNames(
            node, ((CoreInternalSyntax.TypeDeclarationSyntax)node.green).members,
            ref declFlags
        );

        // var modifiers = node.modifiers.ToDeclarationModifiers(isForTypeDeclaration: true, diagnostics: diagnostics);
        var modifiers = ModifierHelpers.CreateModifiers(node.modifiers, diagnostics, out _);

        return new SingleTypeDeclaration(
            kind: kind,
            name: node.identifier.text,
            arity: node.arity,
            modifiers: modifiers,
            declFlags: declFlags,
            syntaxReference: new SyntaxReference(node),
            nameLocation: node.identifier.location,
            memberNames: memberNames,
            children: VisitTypeChildren(node),
            diagnostics: diagnostics.ToImmutableAndFree()
        );
    }

    private ImmutableArray<SingleTypeDeclaration> VisitTypeChildren(TypeDeclarationSyntax node) {
        if (node.members.Count == 0)
            return [];

        var children = ArrayBuilder<SingleTypeDeclaration>.GetInstance();

        foreach (var member in node.members) {
            var typeDecl = Visit(member) as SingleTypeDeclaration;
            children.AddIfNotNull(typeDecl);
        }

        return children.ToImmutableAndFree();
    }

    private BoxedMemberNames GetNonTypeMemberNames(
        BelteSyntaxNode parent,
        CoreInternalSyntax.SyntaxList<CoreInternalSyntax.MemberDeclarationSyntax> members,
        ref SingleTypeDeclaration.TypeDeclarationFlags declFlags,
        bool skipGlobalStatements = false) {
        var anyNonTypeMembers = false;
        var anyRequiredMembers = false;

        foreach (var member in members) {
            if (!anyNonTypeMembers && HasAnyNonTypeMemberNames(member, skipGlobalStatements))
                anyNonTypeMembers = true;

            if (anyNonTypeMembers && anyRequiredMembers) {
                break;
            }
        }

        if (anyNonTypeMembers) {
            declFlags |= SingleTypeDeclaration.TypeDeclarationFlags.HasAnyNonTypeMembers;
        }

        if (anyRequiredMembers) {
            declFlags |= SingleTypeDeclaration.TypeDeclarationFlags.HasRequiredMembers;
        }

        return GetOrComputeMemberNames(
            parent,
            static (memberNamesBuilder, tuple) => {
                foreach (var member in tuple.members)
                    AddNonTypeMemberNames(member, memberNamesBuilder);
            },
            (members, false));
    }

    private BoxedMemberNames GetOrComputeMemberNames<TData>(
        SyntaxNode parent,
        Action<HashSet<string>, TData> addMemberNames,
        TData data) {
        var result = GetOrComputeMemberNamesWorker();
        _currentTypeIndex++;
        return result;

        BoxedMemberNames GetOrComputeMemberNamesWorker() {
            var greenNode = parent.green;

            if (!NodeToMemberNames.TryGetValue(greenNode, out var memberNames)) {
                var memberNamesBuilder = PooledHashSet<string>.GetInstance();
                addMemberNames(memberNamesBuilder, data);

                var previousMemberNames = _currentTypeIndex < _previousMemberNames.Count &&
                    _previousMemberNames[_currentTypeIndex].TryGetTarget(out var previousNames)
                        ? previousNames
                        : EmptyMemberNames;

                memberNames = previousMemberNames.Value.Count == memberNamesBuilder.Count && previousMemberNames.Value.SetEquals(memberNamesBuilder)
                    ? previousMemberNames
                    : memberNamesBuilder.Count == 0
                        ? EmptyMemberNames
                        : new BoxedMemberNames(ImmutableSegmentedHashSet.CreateRange(memberNamesBuilder));

                memberNamesBuilder.Free();

                if (memberNames.Value.Count > 0) {
                    using PooledDelegates.Releaser _ = PooledDelegates.GetPooledCreateValueCallback(
                        static (GreenNode _, BoxedMemberNames memberNames) => memberNames, memberNames, out var pooledCallback);
                    memberNames = NodeToMemberNames.GetValue(greenNode, pooledCallback);
                }
            }

            return memberNames;
        }
    }

    private static bool CheckMethodMemberForExtensionSyntax(CoreInternalSyntax.BelteSyntaxNode member) {
        if (member.kind == SyntaxKind.MethodDeclaration) {
            var methodDecl = (CoreInternalSyntax.MethodDeclarationSyntax)member;

            var paramList = methodDecl.parameterList;
            if (paramList != null) {
                var parameters = paramList.parameters;

                if (parameters.Count != 0) {
                    var firstParameter = parameters[0];

                    foreach (var modifier in firstParameter.modifiers) {
                        if (modifier.kind == SyntaxKind.ThisKeyword)
                            return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool CheckMemberForAttributes(CoreInternalSyntax.BelteSyntaxNode member) {
        switch (member.kind) {
            case SyntaxKind.ClassDeclaration:
            case SyntaxKind.StructDeclaration:
                return ((CoreInternalSyntax.TypeDeclarationSyntax)member).attributeLists.Any();
            case SyntaxKind.FieldDeclaration:
                return ((CoreInternalSyntax.FieldDeclarationSyntax)member).attributeLists.Any();
            case SyntaxKind.MethodDeclaration:
            case SyntaxKind.OperatorDeclaration:
            case SyntaxKind.ConstructorDeclaration:
                return ((CoreInternalSyntax.BaseMethodDeclarationSyntax)member).attributeLists.Any();
        }

        return false;
    }

    private static void AddNonTypeMemberNames(CoreInternalSyntax.BelteSyntaxNode member, HashSet<string> set) {
        switch (member.kind) {
            case SyntaxKind.FieldDeclaration:
                set.Add(((CoreInternalSyntax.FieldDeclarationSyntax)member).declaration.identifier.text);
                break;
            case SyntaxKind.MethodDeclaration:
                var methodDecl = (CoreInternalSyntax.MethodDeclarationSyntax)member;
                // TODO Assuming this is always null, so true
                // if (methodDecl.ExplicitInterfaceSpecifier == null) {
                //     set.Add(methodDecl.Identifier.ValueText);
                // }
                set.Add(methodDecl.identifier.text);
                break;
            case SyntaxKind.ConstructorDeclaration:
                set.Add(WellKnownMemberNames.InstanceConstructorName);
                break;
            case SyntaxKind.OperatorDeclaration:
                var opDecl = (CoreInternalSyntax.OperatorDeclarationSyntax)member;
                var name = SyntaxFacts.GetOperatorMemberName(opDecl);
                set.Add(name);
                break;
        }
    }

    private static bool HasAnyNonTypeMemberNames(CoreInternalSyntax.BelteSyntaxNode member, bool skipGlobalStatements) {
        switch (member.kind) {
            case SyntaxKind.FieldDeclaration:
            case SyntaxKind.MethodDeclaration:
            case SyntaxKind.ConstructorDeclaration:
            case SyntaxKind.OperatorDeclaration:
                return true;
            case SyntaxKind.GlobalStatement:
                return !skipGlobalStatements;
        }

        return false;
    }
}
