using System.Reflection;
using System.Runtime.CompilerServices;

namespace smoc.Tests.TestInfra;

/// <summary>
/// A class that can be used to compare screenshots of from the test contextto goldens.
/// </summary>
public class ScreenshotDiffer {
  private readonly ITestOutputHelper _output;
  private readonly string _goldenRoot;
  private readonly bool _updateGoldens;
  private readonly string _projectRoot;

  /// <summary>
  /// Creates a new <see cref="ScreenshotDiffer"/>.
  /// </summary>
  /// <param name="output">The test output helper.</param>
  /// <param name="goldenRoot">The root directory for goldens.</param>
  public ScreenshotDiffer(ITestOutputHelper output, string goldenRoot = "goldens") {
    _output = output;
    _goldenRoot = goldenRoot;
    _projectRoot = Assembly.GetExecutingAssembly()
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == "ProjectDir")?.Value ?? throw new Exception("ProjectDir not found");
    _updateGoldens = Environment.GetEnvironmentVariable("UPDATE_GOLDENS") == "true";
  }

  /// <summary>
  /// Asserts that the screenshot of the test context matches the golden.
  /// </summary>
  /// <param name="testContext">The test context.</param>
  /// <param name="goldenNum">The golden number.</param>
  /// <param name="ansiShot">If true, takes a screenshot with ansi characters useful if formatting is critical, otherwise basic text only; default false</param>
  /// <param name="callerPath">The caller path.</param>
  /// <param name="callerMember">The caller member.</param>
  public void AssertEqualsGolden(AppTestHelpers.AppTestHelper testContext, int goldenNum = 0, bool ansiShot = false, [CallerFilePath] string? callerPath = null, [CallerMemberName] string? callerMember = null) {
    if (callerPath == null || callerMember == null) throw new ArgumentNullException("callerPath and callerMember were not filled by the compiler");
    var callerFile = Path.GetFileNameWithoutExtension(callerPath);
    using var textWriter = new StringWriter();
    if (ansiShot) {
      testContext.AnsiScreenShot($"{callerFile}.{callerMember}_{goldenNum}_ansi", textWriter);
    } else {
      testContext.ScreenShot($"{callerFile}.{callerMember}_{goldenNum}", textWriter);
    }
    string actual = textWriter.ToString();

    var goldenPath = Path.Combine(_projectRoot, _goldenRoot, callerFile);
    string goldenFile;
    if (goldenNum == 0) {
      goldenFile = Path.Combine(goldenPath, callerMember + ".golden");
    } else {
      goldenFile = Path.Combine(goldenPath, callerMember + $"_{goldenNum}.golden");
    }

    if (_updateGoldens) {
      Directory.CreateDirectory(goldenPath);
      File.WriteAllText(goldenFile, actual);
    } else {
      string golden = "";

      try {
        golden = File.ReadAllText(goldenFile);
        Assert.Equal(golden, actual);
      } catch {
        _output.WriteLine("Expected:");
        _output.WriteLine(golden);
        _output.WriteLine("Actual:");
        _output.WriteLine(actual);
        throw;
      }
    }
  }
}