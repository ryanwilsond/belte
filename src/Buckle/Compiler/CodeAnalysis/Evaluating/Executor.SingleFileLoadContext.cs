using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Buckle.CodeAnalysis.Evaluating;

internal sealed partial class Executor {
    private sealed class SingleFileLoadContext : AssemblyLoadContext {
        private readonly string _dir;

        internal SingleFileLoadContext(string dir) : base(isCollectible: true) {
            _dir = dir;
        }

        protected override Assembly Load(AssemblyName assemblyName) {
            var path = Path.Combine(_dir, $"{assemblyName.Name}.dll");

            if (File.Exists(path))
                return LoadFromAssemblyPath(path);

            return null;
        }
    }
}
