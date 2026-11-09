using System.Collections.Immutable;
using System.Diagnostics;

namespace Buckle.CodeAnalysis;

public sealed partial class Compilation {
    internal sealed partial class ReferenceManager {
        [DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
        private readonly struct ResolvedReference {
            private readonly MetadataImageKind _kind;
            private readonly int _index;
            private readonly ImmutableArray<string> _aliasesOpt;
            private readonly ImmutableArray<string> _recursiveAliasesOpt;
            private readonly ImmutableArray<MetadataReference> _mergedReferencesOpt;

            internal ResolvedReference(int index, MetadataImageKind kind) {
                Debug.Assert(index >= 0);
                _index = index + 1;
                _kind = kind;
                _aliasesOpt = default;
                _recursiveAliasesOpt = default;
                _mergedReferencesOpt = default;
            }

            internal ResolvedReference(
                int index,
                MetadataImageKind kind,
                ImmutableArray<string> aliasesOpt,
                ImmutableArray<string> recursiveAliasesOpt,
                ImmutableArray<MetadataReference> mergedReferences)
                : this(index, kind) {
                Debug.Assert(!aliasesOpt.IsDefault || !recursiveAliasesOpt.IsDefault);
                Debug.Assert(!mergedReferences.IsDefault);

                _aliasesOpt = aliasesOpt;
                _recursiveAliasesOpt = recursiveAliasesOpt;
                _mergedReferencesOpt = mergedReferences;
            }

            private bool _isUninitialized
                => (_aliasesOpt.IsDefault && _recursiveAliasesOpt.IsDefault) || _mergedReferencesOpt.IsDefault;

            internal ImmutableArray<string> aliasesOpt {
                get {
                    Debug.Assert(!_isUninitialized);
                    return _aliasesOpt;
                }
            }

            internal ImmutableArray<string> recursiveAliasesOpt {
                get {
                    Debug.Assert(!_isUninitialized);
                    return _recursiveAliasesOpt;
                }
            }

            internal ImmutableArray<MetadataReference> mergedReferences {
                get {
                    Debug.Assert(!_isUninitialized);
                    return _mergedReferencesOpt;
                }
            }

            internal bool isSkipped => _index == 0;

            internal MetadataImageKind kind {
                get {
                    Debug.Assert(!isSkipped);
                    return _kind;
                }
            }

            internal int index {
                get {
                    Debug.Assert(!isSkipped);
                    return _index - 1;
                }
            }

            private string GetDebuggerDisplay() {
                return isSkipped
                    ? "<skipped>"
                    : $"{(_kind == MetadataImageKind.Assembly ? "A" : "M")}[{index}]:{DisplayAliases(_aliasesOpt, "aliases")}{DisplayAliases(_recursiveAliasesOpt, "recursive-aliases")}";
            }

            private static string DisplayAliases(ImmutableArray<string> aliasesOpt, string name) {
                return aliasesOpt.IsDefault ? "" : $" {name} = '{string.Join("','", aliasesOpt)}'";
            }
        }
    }
}
