using System;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal readonly struct MemberResolutionResult<T> where T : Symbol {
    private readonly T _member;
    private readonly T _leastOverriddenMember;
    private readonly MemberAnalysisResult _result;

    internal readonly bool hasTemplateArgumentInferredFromFunctionType;

    internal MemberResolutionResult(
        T member,
        T leastOverriddenMember,
        MemberAnalysisResult result,
        bool hasTypeArgumentInferredFromFunctionType) {
        _member = member;
        _leastOverriddenMember = leastOverriddenMember;
        _result = result;
        hasTemplateArgumentInferredFromFunctionType = hasTypeArgumentInferredFromFunctionType;
    }

    internal MemberResolutionResult<T> WithResult(MemberAnalysisResult result) {
        return new MemberResolutionResult<T>(
            member,
            leastOverriddenMember,
            result,
            hasTemplateArgumentInferredFromFunctionType
        );
    }

    internal bool isNull => _member is null;

    internal bool isNotNull => _member is not null;

    internal T member => _member;

    internal T leastOverriddenMember => _leastOverriddenMember;

    internal MemberResolutionKind resolution => result.kind;

    internal bool isValid => result.isValid;

    internal bool isApplicable => result.isApplicable;

    internal MemberAnalysisResult result => _result;

    internal MemberResolutionResult<T> Worse() {
        return WithResult(MemberAnalysisResult.Worse());
    }

    internal MemberResolutionResult<T> Worst() {
        return WithResult(MemberAnalysisResult.Worst());
    }

    public override bool Equals(object obj) {
        throw new NotSupportedException();
    }

    public override int GetHashCode() {
        throw new NotSupportedException();
    }
}
