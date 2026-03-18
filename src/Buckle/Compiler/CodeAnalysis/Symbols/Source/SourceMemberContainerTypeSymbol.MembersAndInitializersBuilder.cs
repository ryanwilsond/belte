using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class SourceMemberContainerTypeSymbol {
    private sealed class MembersAndInitializersBuilder {
        private ArrayBuilder<Symbol> _nonTypeMembers;
        private ArrayBuilder<FieldInitializer> _instanceInitializersForPositionalMembers;

        internal MembersAndInitializers ToReadOnlyAndFree(DeclaredMembersAndInitializers declaredMembers) {
            var nonTypeMembers = _nonTypeMembers?.ToImmutableAndFree() ?? declaredMembers.nonTypeMembers;

            var instanceInitializers = _instanceInitializersForPositionalMembers is null
                ? declaredMembers.instanceInitializers
                : MergeInitializers();

            return new MembersAndInitializers(
                nonTypeMembers,
                instanceInitializers,
                declaredMembers.staticInitializers
            );

            ImmutableArray<ImmutableArray<FieldInitializer>> MergeInitializers() {
                var groupCount = declaredMembers.instanceInitializers.Length;

                if (groupCount == 0)
                    return [_instanceInitializersForPositionalMembers.ToImmutableAndFree()];

                var compilation = declaredMembers.compilation;
                var sortKey = new LexicalSortKey(_instanceInitializersForPositionalMembers.First().syntax, compilation);

                int insertAt;

                for (insertAt = 0; insertAt < groupCount; insertAt++) {
                    if (LexicalSortKey.Compare(
                        sortKey,
                        new LexicalSortKey(declaredMembers.instanceInitializers[insertAt][0].syntax, compilation)) < 0) {
                        break;
                    }
                }

                ArrayBuilder<ImmutableArray<FieldInitializer>> groupsBuilder;

                if (insertAt != groupCount &&
                    declaredMembers.declarationWithParameters.syntaxTree ==
                        declaredMembers.instanceInitializers[insertAt][0].syntax.syntaxTree &&
                    declaredMembers.declarationWithParameters.span
                        .Contains(declaredMembers.instanceInitializers[insertAt][0].syntax.span.start)) {
                    var declaredInitializers = declaredMembers.instanceInitializers[insertAt];
                    var insertedInitializers = _instanceInitializersForPositionalMembers;
                    insertedInitializers.AddRange(declaredInitializers);

                    groupsBuilder = ArrayBuilder<ImmutableArray<FieldInitializer>>.GetInstance(groupCount);
                    groupsBuilder.AddRange(declaredMembers.instanceInitializers, insertAt);
                    groupsBuilder.Add(insertedInitializers.ToImmutableAndFree());
                    groupsBuilder.AddRange(
                        declaredMembers.instanceInitializers,
                        insertAt + 1,
                        groupCount - (insertAt + 1)
                    );
                } else {
                    groupsBuilder = ArrayBuilder<ImmutableArray<FieldInitializer>>.GetInstance(groupCount + 1);
                    groupsBuilder.AddRange(declaredMembers.instanceInitializers, insertAt);
                    groupsBuilder.Add(_instanceInitializersForPositionalMembers.ToImmutableAndFree());
                    groupsBuilder.AddRange(declaredMembers.instanceInitializers, insertAt, groupCount - insertAt);
                }

                var result = groupsBuilder.ToImmutableAndFree();
                return result;
            }
        }

        internal void AddInstanceInitializerForPositionalMembers(FieldInitializer initializer) {
            _instanceInitializersForPositionalMembers ??= ArrayBuilder<FieldInitializer>.GetInstance();
            _instanceInitializersForPositionalMembers.Add(initializer);
        }

        internal IReadOnlyCollection<Symbol> GetNonTypeMembers(DeclaredMembersAndInitializers declaredMembers) {
            return _nonTypeMembers ?? (IReadOnlyCollection<Symbol>)declaredMembers.nonTypeMembers;
        }

        internal void AddNonTypeMember(Symbol member, DeclaredMembersAndInitializers declaredMembers) {
            if (_nonTypeMembers is null) {
                _nonTypeMembers = ArrayBuilder<Symbol>.GetInstance(declaredMembers.nonTypeMembers.Length + 1);
                _nonTypeMembers.AddRange(declaredMembers.nonTypeMembers);
            }

            _nonTypeMembers.Add(member);
        }

        internal void SetNonTypeMembers(ArrayBuilder<Symbol> members) {
            _nonTypeMembers?.Free();
            _nonTypeMembers = members;
        }

        internal static ImmutableArray<ImmutableArray<FieldInitializer>> ToReadOnlyAndFree(
            ArrayBuilder<ArrayBuilder<FieldInitializer>> initializers) {
            if (initializers.Count == 0) {
                initializers.Free();
                return [];
            }

            var builder = ArrayBuilder<ImmutableArray<FieldInitializer>>.GetInstance(initializers.Count);

            foreach (var group in initializers)
                builder.Add(group.ToImmutableAndFree());

            initializers.Free();
            return builder.ToImmutableAndFree();
        }

        internal void Free() {
            _nonTypeMembers?.Free();
            _instanceInitializersForPositionalMembers?.Free();
        }
    }
}
