using Belte.CommandLine;

namespace Belte;

public static class Program {
    public static int Main(string[] args) {
        Setup.SetupConfiguration();

        return BuckleCommandLine.ProcessArgs(args);
    }
}
