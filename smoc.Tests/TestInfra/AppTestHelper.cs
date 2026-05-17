using System.Diagnostics;
using System.Drawing;
using System.Text;
using Microsoft.Extensions.Logging;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Terminal.Gui.Time;
using Terminal.Gui.ViewBase;

namespace smoc.Tests.TestInfra;

/// <summary>
///     Helper for building integration tests.
///     Create an instance using <see cref="With"/> static class.
/// </summary>
/// <remarks>
///     This is forked from Terminal.Gui.Tests.AppTestHelpers.
/// </remarks>
public partial class AppTestHelper : IDisposable {
  private readonly CancellationTokenSource _runCancellationTokenSource = new();
  private CancellationTokenSource? _timeoutCts;
  private readonly Task? _runTask;
  private readonly SemaphoreSlim _booting;
  private readonly object _cancellationLock = new();
  private volatile bool _finished;

  private readonly object _backgroundExceptionLock = new();
  private Exception? _backgroundException;

  private readonly AnsiInput _ansiInput = new();
  private IOutput? _output;

  /// <summary>
  ///     The IApplication instance that was created.
  /// </summary>
  public IApplication? App { get; private set; }

  /// <summary>
  ///     The ITimeProvider for time operations in tests.
  ///     Uses VirtualTimeProvider for fast, deterministic testing with full time control.
  /// </summary>
  public ITimeProvider TimeProvider { get; } = new VirtualTimeProvider();

  private readonly string _driverName;

  // ===== Test Configuration =====
  private readonly bool _runApplication;
  private TimeSpan _timeout;

  // ===== Logging =====
  private readonly object _logsLock = new();
  private readonly TextWriter? _logWriter;
  private ILogger? _testLogger;
  private IDisposable? _loggerScope;
  private StringBuilder? _logsSb;

  /// <summary>
  ///     Constructor for tests that only need Application.Init without running the main loop.
  ///     Uses the driver's default screen size instead of forcing a specific size.
  /// </summary>
  public AppTestHelper(string driverName, TextWriter? logWriter = null, TimeSpan? timeout = null) {
    _driverName = driverName;
    _logWriter = logWriter;
    _runApplication = false;
    _booting = new SemaphoreSlim(0, 1);

    // Don't force a size - let the driver determine it
    CommonInit(0, 0, timeout);
    _loggerScope = Logging.PushLogger(_testLogger!);

    try {
      App?.Init(driverName);
      _booting.Release();

      // After Init, Application.Screen should be set by the driver
      if (App?.Screen == Rectangle.Empty) {
        throw new InvalidOperationException("Driver bug: Application.Screen is empty after Init. The driver should set the screen size during Init.");
      }
    } catch (Exception ex) {
      lock (_backgroundExceptionLock) // NEW: Thread-safe exception handling
      {
        _backgroundException = ex;
      }

      if (_logWriter != null) {
        WriteOutLogs(_logWriter);
      }

      throw new Exception("Application initialization failed", ex);
    }

    lock (_backgroundExceptionLock) // NEW: Thread-safe check
    {
      if (_backgroundException != null) {
        throw new Exception("Application initialization failed", _backgroundException);
      }
    }
  }

  /// <summary>
  ///     Constructor for tests that need to run the application with Application.Run.
  /// </summary>
  internal AppTestHelper(Func<IRunnable> runnableBuilder, int width, int height, string driverName, TextWriter? logWriter = null, TimeSpan? timeout = null) {
    _driverName = driverName;
    _logWriter = logWriter;
    _runApplication = true;
    _booting = new SemaphoreSlim(0, 1);

    CommonInit(width, height, timeout);

    // Start the application in a background thread
    _runTask = Task.Run(() => {
      _loggerScope = Logging.PushLogger(_testLogger!);

      try {
        try {
          App?.Init(driverName);
        } catch (Exception e) {
          Logging.Error(e.Message);
          _runCancellationTokenSource.Cancel();
        } finally {
          _booting.Release();
        }

        if (App is { Initialized: true }) {
          IRunnable runnable = runnableBuilder();

          runnable.IsRunningChanged += (s, e) => {
            if (!e.Value) {
              Finished = true;
            }
          };
          App?.Run(runnable); // This will block, but it's on a background thread now

          if (runnable is View runnableView) {
            runnableView.Dispose();
          }

          //Logging.Trace ("Application.Run completed");
          App?.Dispose();
          _runCancellationTokenSource.Cancel();
        }
      } catch (OperationCanceledException) {
        Logging.Trace("OperationCanceledException");
      } catch (Exception ex) {
        _backgroundException = ex;
        _ansiInput.ExternalCancellationTokenSource!.Cancel();
      } finally {
        CleanupApplication();

        if (_logWriter != null) {
          WriteOutLogs(_logWriter);
        }
      }
    },
                         _runCancellationTokenSource.Token);

    // Wait for booting to complete with a timeout to avoid hangs
    if (!_booting.WaitAsync(_timeout).Result) {
      throw new TimeoutException($"Application failed to start within {_timeout}ms.");
    }

    ResizeConsole(width, height);

    if (_backgroundException is { }) {
      throw new Exception("Application crashed", _backgroundException);
    }
  }

  /// <summary>
  ///     Common initialization for both constructors.
  /// </summary>
  private void CommonInit(int width, int height, TimeSpan? timeout) {
    _timeout = timeout ?? TimeSpan.FromSeconds(30);
    _logsSb = new StringBuilder();

    _testLogger = LoggerFactory
                  .Create(builder => builder.SetMinimumLevel(LogLevel.Trace))
                  .CreateLogger("Test Logging");

    // ✅ Link _runCancellationTokenSource with a timeout
    // This creates a token that responds to EITHER the run cancellation OR timeout
    _timeoutCts = new CancellationTokenSource(_timeout);
    _ansiInput.ExternalCancellationTokenSource =
        CancellationTokenSource.CreateLinkedTokenSource(_runCancellationTokenSource.Token, _timeoutCts.Token);

    // Now when InputImpl.Run receives this ExternalCancellationTokenSource,
    // it will create ANOTHER linked token internally that combines:
    // - Its own runCancellationToken parameter
    // - The ExternalCancellationTokenSource (which is already linked)
    // This creates a chain: any of these triggers will stop input:
    // 1. _runCancellationTokenSource.Cancel() (normal stop)
    // 2. Timeout expires (test timeout)
    // 3. Direct cancel of ExternalCancellationTokenSource (hard stop/error)

    // Remove frame limit
    Application.MaximumIterationsPerSecond = ushort.MaxValue;

    IComponentFactory? cf = null;

    _output = new AnsiOutput();

    // Only set size if explicitly provided (width and height > 0)
    if (width > 0 && height > 0) {
      _output.SetSize(width, height);
    }

    cf = new AnsiComponentFactory(_ansiInput, _output);

    App = Application.Create(TimeProvider);

    //Logging.Trace ($"Driver: {_driverName}. Timeout: {_timeout}");
  }

  /// <summary>
  ///     Gets whether the application has finished running; aka Stop has been called and the main loop has exited.
  /// </summary>
  public bool Finished { get => _finished; private set => _finished = value; }

  /// <summary>
  ///     Performs the supplied <paramref name="doAction"/> immediately.
  ///     Enables running commands without breaking the Fluent API calls.
  /// </summary>
  /// <param name="doAction"></param>
  /// <returns></returns>
  public AppTestHelper Then(Action<IApplication> doAction) {
    try {
      //Logging.Trace ($"Invoking action via WaitIteration");
      WaitIteration(doAction);
    } catch (Exception ex) {
      _backgroundException = ex;
      HardStop();

      throw;
    }

    return this;
  }

  /// <summary>
  ///     Waits until the end of the current iteration of the main loop. Optionally
  ///     running a given <paramref name="action"/> action on the UI thread at that time.
  /// </summary>
  /// <param name="action"></param>
  /// <returns></returns>
  public AppTestHelper WaitIteration(Action<IApplication>? action = null) {
    // If application has already exited don't wait!
    if (Finished || _runCancellationTokenSource.Token.IsCancellationRequested || _ansiInput.ExternalCancellationTokenSource!.Token.IsCancellationRequested) {
      Logging.Warning("WaitIteration called after context was stopped");

      return this;
    }

    if (Thread.CurrentThread.ManagedThreadId == App?.MainThreadId) {
      throw new NotSupportedException("Cannot WaitIteration during Invoke");
    }

    //Logging.Trace ($"WaitIteration started");
    if (action is null) {
      action = app => { };
    }
    CancellationTokenSource ctsActionCompleted = new();

    App?.Invoke(app => {
      try {
        action(app);

        //Logging.Trace ("Action completed");
        ctsActionCompleted.Cancel();
      } catch (Exception e) {
        Logging.Warning($"Action failed with exception: {e}");
        _backgroundException = e;
        _ansiInput.ExternalCancellationTokenSource?.Cancel();
      }
    });

    // Blocks until any of these are cancelled:
    // - _runCancellationTokenSource: normal stop
    // - ExternalCancellationTokenSource: timeout or error (may fire independently)
    // - ctsActionCompleted: the action finished successfully
    WaitHandle.WaitAny([
        _runCancellationTokenSource.Token.WaitHandle,
        _ansiInput.ExternalCancellationTokenSource!.Token.WaitHandle,
        ctsActionCompleted.Token.WaitHandle]);

    // Logging.Trace ($"Return from WaitIteration");
    return this;
  }

  public AppTestHelper WaitUntil(Func<bool> condition) {
    AppTestHelper? c = null;
    var sw = Stopwatch.StartNew();

    //Logging.Trace ($"WaitUntil started with timeout {_timeout}");

    var count = 0;

    while (!condition()) {
      if (sw.Elapsed > _timeout) {
        throw new TimeoutException($"Failed to reach condition within {_timeout}ms");
      }

      c = WaitIteration();
      count++;
    }

    Logging.Trace($"WaitUntil completed after {sw.ElapsedMilliseconds}ms and {count} iterations");

    return c ?? this;
  }

  /// <summary>
  ///     Returns the last set position of the cursor.
  /// </summary>
  /// <returns></returns>
  public Point? GetCursorPosition() {
    App?.Navigation?.UpdateCursor();

    return _output!.GetCursor().Position;
  }

  /// <summary>
  ///     Simulates changing the console size e.g. by resizing window in your operating system
  /// </summary>
  /// <param name="width">new Width for the console.</param>
  /// <param name="height">new Height for the console.</param>
  /// <returns></returns>
  public AppTestHelper ResizeConsole(int width, int height) => WaitIteration(app => { app.Driver!.SetScreenSize(width, height); });

  public AppTestHelper ScreenShot(string title, TextWriter? writer) =>

      //Logging.Trace ($"{this.ToIdentifyingString ()}");
      WaitIteration(app => {
        writer?.WriteLine(title + ":");
        var text = app.Driver?.ToString();

        writer?.WriteLine(text);
      });

  public AppTestHelper AnsiScreenShot(string title, TextWriter? writer) =>

      //Logging.Trace ($"{this.ToIdentifyingString ()}");
      WaitIteration(app => {
        writer?.WriteLine(title + ":");
        string? text = app.Driver?.ToAnsi();

        writer?.WriteLine(text);
      });

  /// <summary>
  ///     Stops the application and waits for the background thread to exit.
  /// </summary>
  public AppTestHelper Stop() {
    Logging.Trace($"Stopping application for driver: {_driverName}");

    if (_runTask is null || _runTask.IsCompleted) {
      // If we didn't run the application, just cleanup
      if (!_runApplication && !Finished) {
        try {
          App?.Dispose();
        } catch {
          // Ignore errors during shutdown
        }

        CleanupApplication();
      }

      return this;
    }

    WaitIteration(app => { app.RequestStop(); });

    // Wait for the application to stop, but give it a 1-second timeout
    const int WAIT_TIMEOUT_MS = 1000;

    if (!_runTask.Wait(TimeSpan.FromMilliseconds(WAIT_TIMEOUT_MS))) {
      _runCancellationTokenSource.Cancel();

      // No need to manually cancel ExternalCancellationTokenSource

      // App is having trouble shutting down, try sending some more shutdown stuff from this thread.
      // If this doesn't work there will be test failures as the main loop continues to run during next test.
      try {
        App?.RequestStop();
        App?.Dispose();
      } catch (Exception ex) {
        Logging.Critical($"Application failed to stop in {WAIT_TIMEOUT_MS}. Then shutdown threw {ex}");
      } finally {
        Logging.Critical($"Application failed to stop in {WAIT_TIMEOUT_MS}. Exception was thrown: {_backgroundException}");
      }
    }

    _runCancellationTokenSource.Cancel();

    if (_backgroundException != null) {
      Logging.Critical($"Exception occurred: {_backgroundException}");

      //throw _ex; // Propagate any exception that happened in the background task
    }

    return this;
  }

  /// <summary>
  ///     Hard stops the application and waits for the background thread to exit.HardStop is used by the source generator for
  ///     wrapping Xunit assertions.
  /// </summary>
  public void HardStop(Exception? ex = null) {
    if (ex != null) {
      _backgroundException = ex;
    }

    Logging.Critical($"HardStop called with exception: {_backgroundException}");

    // With linked tokens, just cancelling ExternalCancellationTokenSource
    // will cascade to stop everything
    _ansiInput.ExternalCancellationTokenSource?.Cancel();
    WriteOutLogs(_logWriter);
    Stop();
  }

  /// <summary>
  ///     Writes all Terminal.Gui engine logs collected so far to the <paramref name="writer"/>
  /// </summary>
  /// <param name="writer"></param>
  /// <returns></returns>
  public AppTestHelper WriteOutLogs(TextWriter? writer) {
    if (writer is null) {
      return this;
    }

    lock (_logsLock) {
      writer.WriteLine(_logsSb!.ToString());
    }

    return this; //WaitIteration();
  }

  internal void Fail(string reason) {
    Logging.Error($"{reason}");
    WriteOutLogs(_logWriter);

    throw new Exception(reason);
  }

  private void CleanupApplication() {
    Logging.Trace("CleanupApplication");

    App?.RequestStop();
    App?.Dispose();
    _loggerScope?.Dispose();
    _loggerScope = null;
    Finished = true;

    Application.MaximumIterationsPerSecond = Application.DefaultMaximumIterationsPerSecond;
  }

  /// <summary>
  ///     Cleanup to avoid state bleed between tests
  /// </summary>
  public void Dispose() {
    //Logging.Trace ($"Disposing AppTestHelper");
    Stop();

    var shouldThrow = false;
    Exception? exToThrow = null;

    lock (_cancellationLock)
    {
      // Only throw if there was an actual background exception (error-based hard stop).
      // Normal shutdown also cancels ExternalCancellationTokenSource via the linked token chain,
      // so checking IsCancellationRequested alone would false-positive on normal teardown.
      lock (_backgroundExceptionLock) {
        if (_backgroundException != null) {
          shouldThrow = true;
          exToThrow = _backgroundException;
        }
      }

      // Dispose the linked token source
      _ansiInput.ExternalCancellationTokenSource?.Dispose();
    }

    _timeoutCts?.Dispose();
    _runCancellationTokenSource?.Dispose();
    _ansiInput.Dispose();
    _output?.Dispose();
    _booting.Dispose();

    if (shouldThrow) {
      throw new Exception("Application was hard stopped...", exToThrow);
    }
  }

  /// <summary>
  ///     Adds the given <paramref name="v"/> to the current top level view
  ///     and performs layout.
  /// </summary>
  /// <param name="v"></param>
  /// <returns></returns>
  public AppTestHelper Add(View v) {
    WaitIteration((app) => {
      View top = App?.TopRunnableView ?? throw new("Top was null so could not add view");
      top.Add(v);
      // BUGBUG: This Layout call is a hack to work around some bug in Layout.
      // BUGBUG: See https://github.com/gui-cs/Terminal.Gui/issues/4522
      top.Layout();
    });

    return this;
  }

  /// <summary>
  ///     Injects a key down event to the current driver's input processor.
  ///     Uses the new simplified injection API with virtual time support.
  /// </summary>
  /// <param name="key">The key to inject.</param>
  /// <returns>This AppTestHelper for fluent chaining.</returns>
  public AppTestHelper KeyDown(Key key) {
    //Logging.Trace ($"Injecting key: {key}");

    // Use the new injection infrastructure - same pattern as mouse injection
    WaitIteration(app => {
      if (app.Driver is { }) {
        // Use the simplified injection API with default Direct mode
        // This is faster and more reliable than Pipeline mode
        app.InjectKey(key);
      } else {
        Fail("Expected Application.Driver to be non-null.");
      }
    });

    // Wait for the event to be processed
    return WaitIteration();
  }
}
