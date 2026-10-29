using CommandLine;

public static class Program {
    public static int Main(string[] args) {
#if SINGLE_FILE_BUILD
        DllImportHelper.ExtractAndLoadDlls();
#endif

        return BuckleCommandLine.ProcessArgs(args);
    }
}
