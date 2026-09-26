using System.Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.CodeGeneration;

internal sealed partial class ILOptimizer {
    internal class LocalDefUseInfo {
        private static readonly ObjectPool<LocalDefUseInfo> PoolInstance = CreatePool();

        private readonly ObjectPool<LocalDefUseInfo> _pool;
        private ArrayBuilder<LocalDefUseSpan> _localDefs;

        private LocalDefUseInfo(ObjectPool<LocalDefUseInfo> pool) {
            _pool = pool;
        }

        internal int stackAtDeclaration { get; private set; }

        internal ArrayBuilder<LocalDefUseSpan> localDefs {
            get {
                var result = _localDefs;

                if (result is null)
                    _localDefs = result = ArrayBuilder<LocalDefUseSpan>.GetInstance();

                return result;
            }
        }

        internal bool cannotSchedule { get; private set; }

        internal void ShouldNotSchedule() {
            cannotSchedule = true;
        }

        internal void Free() {
            _localDefs?.Free();
            _localDefs = null;
            _pool?.Free(this);
        }

        internal static ObjectPool<LocalDefUseInfo> CreatePool() {
            ObjectPool<LocalDefUseInfo> pool = null;
            pool = new ObjectPool<LocalDefUseInfo>(() => new LocalDefUseInfo(pool), 128);
            return pool;
        }

        internal static LocalDefUseInfo GetInstance(int stackAtDeclaration) {
            var instance = PoolInstance.Allocate();
            Debug.Assert(instance._localDefs == null);
            instance.stackAtDeclaration = stackAtDeclaration;
            instance.cannotSchedule = false;
            return instance;
        }
    }
}
