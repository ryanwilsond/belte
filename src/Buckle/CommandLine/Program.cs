using CommandLine;

public static class Program {
    public static int Main(string[] args) {
#if !DEBUG
        DllImportHelper.ExtractAndLoadDlls();
#endif
        return BuckleCommandLine.ProcessArgs(args);
    }
}
