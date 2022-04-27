using Belte.CmdLine;

namespace Belte;

public static class Program {
    public static int Main(string[] args) {
        return CommandLine.ProcessArgs(args);
    }
}
