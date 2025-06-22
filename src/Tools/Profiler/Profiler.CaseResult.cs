namespace Profiling;

public static partial class Profiler {
    private class CaseResult {
        internal int eBt;
        internal int eEMt;
        internal int eEXt;
        internal int iIt;
        internal int evBt;
        internal int evEt;

        internal string eE;
        internal string iE;
        internal string evE;

        internal void Add(CaseResult other) {
            eBt += other.eBt;
            eEMt += other.eEMt;
            eEXt += other.eEXt;
            iIt += other.iIt;
            evBt += other.evBt;
            evEt += other.evEt;
        }
    }
}
