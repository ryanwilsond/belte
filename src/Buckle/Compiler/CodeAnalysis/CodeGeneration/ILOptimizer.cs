using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.CodeGeneration;

// TODO
internal sealed partial class ILOptimizer {
    internal static BoundBlockStatement Optimize(
        BoundBlockStatement src,
        bool debugFriendly,
        out HashSet<DataContainerSymbol> stackLocals) {
        stackLocals = [];
        var locals = PooledDictionary<DataContainerSymbol, LocalDefUseInfo>.GetInstance();
        src = (BoundBlockStatement)StackOptimizerPass1.Analyze(src, locals, debugFriendly);

        FilterValidStackLocals(locals);

        BoundBlockStatement result;

        if (locals.Count == 0) {
            stackLocals = null;
            result = src;
        } else {
            stackLocals = new HashSet<DataContainerSymbol>(locals.Keys);
            result = StackOptimizerPass2.Rewrite(src, locals);
        }

        foreach (var info in locals.Values)
            info.Free();

        locals.Free();

        return result;
    }

    private static void FilterValidStackLocals(Dictionary<DataContainerSymbol, LocalDefUseInfo> info) {
        var dummies = ArrayBuilder<LocalDefUseInfo>.GetInstance();

        foreach (var local in info.Keys.ToArray()) {
            var locInfo = info[local];

            if (local.synthesizedKind == SynthesizedLocalKind.OptimizerTemp) {
                dummies.Add(locInfo);
                info.Remove(local);
            } else if (locInfo.cannotSchedule) {
                locInfo.Free();
                info.Remove(local);
            }
        }

        if (info.Count != 0)
            RemoveIntersectingLocals(info, dummies);

        foreach (var dummy in dummies)
            dummy.Free();

        dummies.Free();
    }

    private static void RemoveIntersectingLocals(
        Dictionary<DataContainerSymbol, LocalDefUseInfo> info,
        ArrayBuilder<LocalDefUseInfo> dummies) {
        var defs = ArrayBuilder<LocalDefUseSpan>.GetInstance(dummies.Count);

        foreach (var dummy in dummies) {
            foreach (var def in dummy.localDefs) {
                if (def.start != def.end)
                    defs.Add(def);
            }
        }

        var dummyCnt = defs.Count;

        var ordered = from i in info
                      from d in i.Value.localDefs
                      orderby d.end - d.start, d.end ascending
                      select new { i = i.Key, d = d };

        foreach (var pair in ordered) {
            if (!info.ContainsKey(pair.i))
                continue;

            var newDef = pair.d;
            var cnt = defs.Count;

            bool intersects;

            if (cnt > 5000) {
                intersects = true;
            } else {
                intersects = false;

                for (var i = 0; i < dummyCnt; i++) {
                    var def = defs[i];

                    if (newDef.ConflictsWithDummy(def)) {
                        intersects = true;
                        break;
                    }
                }

                if (!intersects) {
                    for (var i = dummyCnt; i < cnt; i++) {
                        var def = defs[i];

                        if (newDef.ConflictsWith(def)) {
                            intersects = true;
                            break;
                        }
                    }
                }
            }

            if (intersects) {
                info[pair.i].localDefs.Free();
                info.Remove(pair.i);
            } else {
                defs.Add(newDef);
            }
        }

        defs.Free();
    }
}
