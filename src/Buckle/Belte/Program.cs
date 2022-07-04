using Belte.CommandLine;

namespace Belte;

public static class Program {
    public static int Main(string[] args) {
        return BuckleCommandLine.ProcessArgs(args);
    }
}
