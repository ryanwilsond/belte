
namespace Buckle.Building;

public sealed class BuildResult {
    public bool succeeded;

    [Belte.CompilerServices.Nullability([1, 1])]
    public string[] buildArguments;

    [Belte.CompilerServices.Nullability([1, 1])]
    public string[] programArguments;

    [Belte.CompilerServices.Nullability([1, 1])]
    public LocalBuildResult[] buildResults;

    public BuildResult(
        bool succeeded,
        string[] buildArguments,
        string[] programArguments,
        LocalBuildResult[] buildResults) {
        this.succeeded = succeeded;
        this.buildArguments = buildArguments;
        this.programArguments = programArguments;
        this.buildResults = buildResults;
    }
}
