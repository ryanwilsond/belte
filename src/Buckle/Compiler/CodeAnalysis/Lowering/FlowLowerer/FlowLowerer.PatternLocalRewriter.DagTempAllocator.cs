using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class FlowLowerer {
    private abstract partial class PatternLocalRewriter {
        internal sealed class DagTempAllocator {
            private readonly FlowLowerer _flowLowerer;
            private readonly PooledDictionary<BoundDagTemp, BoundExpression> _map
                = PooledDictionary<BoundDagTemp, BoundExpression>.GetInstance();
            private readonly ArrayBuilder<DataContainerSymbol> _temps = ArrayBuilder<DataContainerSymbol>.GetInstance();
            private readonly SyntaxNode _node;

            internal DagTempAllocator(FlowLowerer flowLowerer, SyntaxNode node) {
                _flowLowerer = flowLowerer;
                _node = node;
            }

            internal void Free() {
                _temps.Free();
                _map.Free();
            }

            internal BoundExpression GetTemp(BoundDagTemp dagTemp) {
                if (!_map.TryGetValue(dagTemp, out var result)) {
                    var temp = _flowLowerer.GenerateTempLocal(dagTemp.type);
                    result = BoundFactory.Local(_node, temp);
                    _map.Add(dagTemp, result);
                    _temps.Add(temp);
                }

                return result;
            }

            internal bool TrySetTemp(BoundDagTemp dagTemp, BoundExpression translation) {
                if (!_map.ContainsKey(dagTemp)) {
                    _map.Add(dagTemp, translation);
                    return true;
                }

                return false;
            }

            internal ImmutableArray<DataContainerSymbol> AllTemps() {
                return _temps.ToImmutableArray();
            }
        }
    }
}
