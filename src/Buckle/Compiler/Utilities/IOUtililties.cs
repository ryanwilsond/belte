using System;
using System.IO;
using System.Threading;

namespace Buckle.Utilities;

internal static class IOUtilities {
    internal static bool TryIOOperation(Action action) {
        for (var j = 1; j < 4; j++) {
            try {
                action();
                return true;
            } catch (IOException) {
                if (j < 3)
                    Thread.Sleep(j * 10);
            }
        }

        return false;
    }
}
