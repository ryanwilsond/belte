using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class DecisionDagBuilder {
    private readonly struct StateForCase {
        internal readonly int index;
        internal readonly SyntaxNode syntax;
        internal readonly Tests remainingTests;
        internal readonly ImmutableArray<BoundPatternBinding> bindings;
        internal readonly LabelSymbol caseLabel;

        internal StateForCase(
            int index,
            SyntaxNode syntax,
            Tests remainingTests,
            ImmutableArray<BoundPatternBinding> bindings,
            LabelSymbol caseLabel) {
            this.index = index;
            this.syntax = syntax;
            this.remainingTests = remainingTests;
            this.bindings = bindings;
            this.caseLabel = caseLabel;
        }

        internal bool isFullyMatched => remainingTests is Tests.True;

        internal bool patternIsSatisfied => remainingTests is Tests.True;

        internal bool isImpossible => remainingTests is Tests.False;

        public override bool Equals(object? obj) {
            throw ExceptionUtilities.Unreachable();
        }

        internal bool Equals(StateForCase other) {
            return index == other.index && remainingTests.Equals(other.remainingTests);
        }

        public override int GetHashCode() {
            return Hash.Combine(remainingTests.GetHashCode(), index);
        }

        internal StateForCase WithRemainingTests(Tests newRemainingTests) {
            return newRemainingTests.Equals(remainingTests)
                ? this
                : new StateForCase(index, syntax, newRemainingTests, bindings, caseLabel);
        }

        internal StateForCase RewriteNestedLengthTests() {
            return WithRemainingTests(remainingTests.RewriteNestedLengthTests());
        }
    }
}
