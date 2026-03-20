namespace Buckle.CodeAnalysis;

internal static partial class MetadataHelpers {
    internal readonly struct AssemblyQualifiedTypeName {
        internal readonly string topLevelType;
        internal readonly string[] nestedTypes;
        internal readonly AssemblyQualifiedTypeName[] typeArguments;
        internal readonly int pointerCount;

        internal readonly int[] arrayRanks;
        internal readonly string assemblyName;

        internal AssemblyQualifiedTypeName(
            string topLevelType,
            string[] nestedTypes,
            AssemblyQualifiedTypeName[] typeArguments,
            int pointerCount,
            int[] arrayRanks,
            string assemblyName) {
            this.topLevelType = topLevelType;
            this.nestedTypes = nestedTypes;
            this.typeArguments = typeArguments;
            this.pointerCount = pointerCount;
            this.arrayRanks = arrayRanks;
            this.assemblyName = assemblyName;
        }
    }
}
