using Belte.CommandLine;

namespace Belte;

public static class Program {
    public static int Main(string[] args) {
        var appSettings = Setup.SetupProgram();
        return BuckleCommandLine.ProcessArgs(args, appSettings);
    }
}
