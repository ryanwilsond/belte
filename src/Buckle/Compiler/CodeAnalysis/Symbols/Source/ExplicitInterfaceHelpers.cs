using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal static class ExplicitInterfaceHelpers {
    internal static ImmutableArray<T> SubstituteExplicitInterfaceImplementations<T>(
        ImmutableArray<T> unsubstitutedExplicitInterfaceImplementations,
        TemplateMap map)
        where T : Symbol {
        var builder = ArrayBuilder<T>.GetInstance();

        foreach (var unsubstitutedPropertyImplemented in unsubstitutedExplicitInterfaceImplementations)
            builder.Add(SubstituteExplicitInterfaceImplementation(unsubstitutedPropertyImplemented, map));

        return builder.ToImmutableAndFree();
    }

    internal static string GetMemberNameAndInterfaceSymbol(
        Binder binder,
        SyntaxTokenList modifiers,
        ExplicitInterfaceSpecifierSyntax explicitInterfaceSpecifier,
        string name,
        BelteDiagnosticQueue diagnostics,
        out TypeSymbol explicitInterfaceType,
        out string aliasQualifier) {
        if (explicitInterfaceSpecifier is null) {
            explicitInterfaceType = null;
            aliasQualifier = null;
            return name;
        }

        binder = binder.WithAdditionalFlags(BinderFlags.SuppressConstraintChecks);

        var explicitInterfaceName = explicitInterfaceSpecifier.name;
        explicitInterfaceType = binder.BindType(explicitInterfaceName, diagnostics).type;
        aliasQualifier = explicitInterfaceName.GetAliasQualifier();
        return GetMemberName(name, explicitInterfaceType, aliasQualifier);
    }

    internal static string GetMemberName(string name, TypeSymbol explicitInterfaceTypeOpt, string aliasQualifierOpt) {
        if (explicitInterfaceTypeOpt is null)
            return name;

        // TODO Inspect this display format closely
        // var interfaceName = explicitInterfaceTypeOpt.ToDisplayString(SymbolDisplayFormat.ExplicitInterfaceImplementationFormat);
        var interfaceName = explicitInterfaceTypeOpt.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat);

        var pooled = PooledStringBuilder.GetInstance();
        var builder = pooled.Builder;

        if (!string.IsNullOrEmpty(aliasQualifierOpt)) {
            builder.Append(aliasQualifierOpt);
            builder.Append("::");
        }

        foreach (var ch in interfaceName) {
            if (ch != ' ')
                builder.Append(ch);
        }

        builder.Append('.');
        builder.Append(name);

        return pooled.ToStringAndFree();
    }

    internal static void FindExplicitlyImplementedMemberVerification(
        this Symbol implementingMember,
        Symbol implementedMember,
        BelteDiagnosticQueue diagnostics) {
        if (implementedMember is null)
            return;

        if (implementingMember.ContainsTupleNames() &&
            MemberSignatureComparer.ConsideringTupleNamesCreatesDifference(implementingMember, implementedMember)) {
            var memberLocation = implementingMember.location;
            diagnostics.Push(Error.ImplBadTupleNames(memberLocation, implementingMember, implementedMember));
        }

        FindExplicitImplementationCollisions(implementingMember, implementedMember, diagnostics);
    }

    private static void FindExplicitImplementationCollisions(
        Symbol implementingMember,
        Symbol implementedMember,
        BelteDiagnosticQueue diagnostics) {
        if (implementedMember is null)
            return;

        var explicitInterfaceType = implementedMember.containingType;
        var explicitInterfaceTypeIsDefinition = explicitInterfaceType.isDefinition;

        foreach (var collisionCandidateMember in explicitInterfaceType.GetMembers(implementedMember.name)) {
            if (collisionCandidateMember.kind == implementingMember.kind &&
                implementedMember != collisionCandidateMember) {
                if (!explicitInterfaceTypeIsDefinition &&
                    MemberSignatureComparer.RuntimeIgnoreRefComparer
                        .Equals(implementedMember, collisionCandidateMember)) {
                    var foundMismatchedRefKind = false;
                    var implementedMemberParameters = implementedMember.GetParameters();
                    var collisionCandidateParameters = collisionCandidateMember.GetParameters();
                    var numParams = implementedMemberParameters.Length;

                    for (var i = 0; i < numParams; i++) {
                        if (implementedMemberParameters[i].refKind != collisionCandidateParameters[i].refKind) {
                            foundMismatchedRefKind = true;
                            break;
                        }
                    }

                    if (foundMismatchedRefKind) {
                        diagnostics.Push(Error.ExplicitImplCollisionOnRefOut(
                            explicitInterfaceType.location,
                            explicitInterfaceType,
                            implementedMember
                        ));
                    } else {
                        // TODO Interfaces Warning
                        // diagnostics.Add(ErrorCode.WRN_ExplicitImplCollision, implementingMember.GetFirstLocation(), implementingMember);
                    }
                    break;
                } else {
                    if (MemberSignatureComparer.ExplicitImplementationComparer
                            .Equals(implementedMember, collisionCandidateMember)) {
                        // TODO Interfaces Warning
                        // diagnostics.Add(ErrorCode.WRN_ExplicitImplCollision, implementingMember.GetFirstLocation(), implementingMember);
                    }
                }
            }
        }
    }

    internal static T SubstituteExplicitInterfaceImplementation<T>(
        T unsubstitutedPropertyImplemented,
        TemplateMap map)
        where T : Symbol {
        var unsubstitutedInterfaceType = unsubstitutedPropertyImplemented.containingType;
        var explicitInterfaceType = map.SubstituteNamedType(unsubstitutedInterfaceType);
        var name = unsubstitutedPropertyImplemented.name;

        foreach (var candidateMember in explicitInterfaceType.GetMembers(name)) {
            if (candidateMember.originalDefinition == unsubstitutedPropertyImplemented.originalDefinition)
                return (T)candidateMember;
        }

        throw ExceptionUtilities.Unreachable();
    }

    internal static MethodSymbol FindExplicitlyImplementedMethod(
        this MethodSymbol implementingMethod,
        bool isOperator,
        TypeSymbol explicitInterfaceType,
        string interfaceMethodName,
        ExplicitInterfaceSpecifierSyntax explicitInterfaceSpecifierSyntax,
        BelteDiagnosticQueue diagnostics) {
        return (MethodSymbol)FindExplicitlyImplementedMember(
            implementingMethod,
            isOperator,
            explicitInterfaceType,
            interfaceMethodName,
            explicitInterfaceSpecifierSyntax,
            diagnostics
        );
    }

    private static Symbol FindExplicitlyImplementedMember(
        Symbol implementingMember,
        bool isOperator,
        TypeSymbol explicitInterfaceType,
        string interfaceMemberName,
        ExplicitInterfaceSpecifierSyntax explicitInterfaceSpecifierSyntax,
        BelteDiagnosticQueue diagnostics) {
        if (explicitInterfaceType is null)
            return null;

        var memberLocation = implementingMember.location;
        var containingType = implementingMember.containingType;

        switch (containingType.typeKind) {
            case TypeKind.Class:
            case TypeKind.Struct:
            case TypeKind.Interface:
                break;

            default:
                diagnostics.Push(Error.ExplicitInterfaceImplementationInNonClassOrStruct(
                    memberLocation,
                    implementingMember
                ));

                return null;
        }

        if (!explicitInterfaceType.IsInterfaceType()) {
            var explicitInterfaceSyntax = explicitInterfaceSpecifierSyntax.name;

            diagnostics.Push(Error.ExplicitInterfaceImplementationNotInterface(
                explicitInterfaceSyntax.location,
                explicitInterfaceType
            ));

            return null;
        }

        var explicitInterfaceNamedType = (NamedTypeSymbol)explicitInterfaceType;

        var set = containingType.interfacesAndTheirBaseInterfaces[explicitInterfaceNamedType];
        var setCount = set.Count;

        if (setCount == 0 || !set.Contains(explicitInterfaceNamedType, SymbolEqualityComparer.Default)) {
            var explicitInterfaceSyntax = explicitInterfaceSpecifierSyntax.name;

            // TODO Pretty sure we care too much about nullability for this
            // if (setCount > 0 && set.Contains(explicitInterfaceNamedType, SymbolEqualityComparer.Default)) {
            //     // diagnostics.Add(ErrorCode.WRN_NullabilityMismatchInExplicitlyImplementedInterface, location);
            // } else {
            diagnostics.Push(Error.ClassDoesntImplementInterface(
                explicitInterfaceSyntax.location,
                implementingMember,
                explicitInterfaceNamedType
            ));
            // }
        }

        var foundMatchingMemberWithoutReturnTypeComparer = false;
        var foundMatchingMember = false;

        Symbol matchingMemberWithoutReturnTypeComparer = null;
        Symbol implementedMember = null;

        if (containingType == (object)explicitInterfaceNamedType.originalDefinition)
            return null;

        foreach (var interfaceMember in explicitInterfaceNamedType.GetMembers(interfaceMemberName)) {
            if (interfaceMember.kind != implementingMember.kind || !interfaceMember.IsImplementableInterfaceMember())
                continue;

            if (interfaceMember is MethodSymbol interfaceMethod &&
                (interfaceMethod.methodKind is MethodKind.Operator or MethodKind.Conversion) != isOperator) {
                continue;
            }

            if (!MemberSignatureComparer.ExplicitImplementationWithoutReturnTypeComparer
                .Equals(implementingMember, interfaceMember)) {
                continue;
            }

            var implementingMemberTypeMap = MemberSignatureComparer.GetTemplateMap(implementingMember);
            var interfaceMemberTypeMap = MemberSignatureComparer.GetTemplateMap(interfaceMember);

            if (MemberSignatureComparer.HaveSameReturnTypes(
                    implementingMember,
                    implementingMemberTypeMap,
                    interfaceMember,
                    interfaceMemberTypeMap,
                    TypeCompareKind.AllIgnoreOptions)) {
                foundMatchingMember = true;
                implementedMember = interfaceMember;
                break;
            } else {
                foundMatchingMemberWithoutReturnTypeComparer = true;
                matchingMemberWithoutReturnTypeComparer = interfaceMember;
            }
        }

        if (!foundMatchingMember) {
            if (foundMatchingMemberWithoutReturnTypeComparer) {
                var returnType = matchingMemberWithoutReturnTypeComparer.GetTypeOrReturnType();

                if (implementingMember.kind == SymbolKind.Method) {
                    diagnostics.Push(Error.ExplicitInterfaceMemberReturnTypeMismatch(
                        memberLocation,
                        implementingMember,
                        returnType,
                        matchingMemberWithoutReturnTypeComparer
                    ));
                } else {
                    diagnostics.Push(Error.ExplicitInterfaceMemberTypeMismatch(
                        memberLocation,
                        implementingMember,
                        returnType,
                        matchingMemberWithoutReturnTypeComparer
                    ));
                }
            } else {
                diagnostics.Push(Error.InterfaceMemberNotFound(memberLocation, implementingMember));
            }
        }

        if (implementedMember is not null) {
            if (!AccessCheck.IsSymbolAccessible(implementedMember, implementingMember.containingType))
                diagnostics.Push(Error.MemberIsInaccessible(memberLocation, implementedMember));
        }

        return implementedMember;
    }
}
