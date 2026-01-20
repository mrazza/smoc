using System.Reflection;
using System.Runtime.CompilerServices;

namespace smoc.Tests.TestInfra;

public class ScreenshotDiffer {
  private readonly ITestOutputHelper _output;
  private readonly string _goldenRoot;
  private readonly bool _updateGoldens;
  private readonly string _projectRoot;

  public ScreenshotDiffer(ITestOutputHelper output, string goldenRoot = "goldens") {
    _output = output;
    _goldenRoot = goldenRoot;
    _projectRoot = Assembly.GetExecutingAssembly()
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == "ProjectDir")?.Value ?? throw new Exception("ProjectDir not found");
    _updateGoldens = Environment.GetEnvironmentVariable("UPDATE_GOLDENS") == "true";
  }

  public void AssertEqualsGolden(TerminalGuiFluentTesting.TestContext testContext, int goldenNum = 0, [CallerFilePath] string? callerPath = null, [CallerMemberName] string? callerMember = null) {
    if (callerPath == null || callerMember == null) throw new ArgumentNullException("callerPath and callerMember were not filled by the compiler");
    var callerFile = Path.GetFileNameWithoutExtension(callerPath);
    using var textWriter = new StringWriter();
    testContext.ScreenShot($"{callerFile}.{callerMember}_{goldenNum}", textWriter);
    string actual = textWriter.ToString();

    var goldenPath = Path.Combine(_projectRoot, _goldenRoot, callerFile);
    string goldenFile;
    if (goldenNum == 0) {
      goldenFile = Path.Combine(goldenPath, callerMember + ".golden");
    }
    else {
      goldenFile = Path.Combine(goldenPath, callerMember + $"_{goldenNum}.golden");
    }

    if (_updateGoldens) {
      Directory.CreateDirectory(goldenPath);
      File.WriteAllText(goldenFile, actual);
    }
    else {
      string golden = File.ReadAllText(goldenFile);

      try {
        Assert.Equal(golden, actual);
      }
      catch {
        _output.WriteLine("Expected:");
        _output.WriteLine(golden);
        _output.WriteLine("Actual:");
        _output.WriteLine(actual);
        throw;
      }
    }
  }
}