using System.Collections.ObjectModel;
using Moq;
using smoc.Tests.Fakes;
using smoc.Tests.TestInfra;
using Smoc.Services;
using Smoc.Streaming;
using Smoc.Ui;
using Smoc.Ui.Components;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Xunit;

namespace smoc.Tests.Ui.Components;

/// <summary>
/// Unit tests for <see cref="SearchResultsList{T}"/>.
/// </summary>
public class SearchResultsListTest {
  private readonly FakeMainWindow _fakeMainWindow = new();
  private readonly Mock<IPlaybackQueueService> _mockPlaybackQueue = new();
  private readonly Mock<IStreamingClient> _mockStreamingClient = new();
  private readonly CommandService _commandService = new();

  /// <summary>
  /// Verifies that Space is removed from KeyBindings so it can bubble up to hotkeys.
  /// </summary>
  [Fact]
  public void Constructor_RemovesSpaceKeyBinding() {
    var searchList = new SearchResultsList<string>();
    Assert.False(searchList.KeyBindings.TryGet(Key.Space, out _));
  }

  /// <summary>
  /// Verifies that OnKeyDown returns false for all command and playback hotkeys,
  /// even when the list is populated with items whose labels start with those characters.
  /// </summary>
  [Fact]
  public void OnKeyDown_BypassedHotKeys_ReturnsFalse() {
    var searchList = new SearchResultsList<string>();
    searchList.SetSource(new ObservableCollection<string>([
      ":colon",
      "/slash",
      ",comma",
      ".period",
      "[bracket",
      "]bracket",
      " space",
      "normal",
    ]));

    var allHotKeys = MainWindow.CommandHotKeys.Concat(NowPlayingBar.PlaybackHotKeys);
    foreach (var key in allHotKeys) {
      Assert.False(searchList.NewKeyDownEvent(key), $"Key '{key}' should not be handled by SearchResultsList.");
    }
  }

  /// <summary>
  /// Verifies that pressing playback hotkeys when SearchResultsList has focus
  /// triggers the corresponding playback queue service actions.
  /// </summary>
  [Fact]
  public void PlaybackHotKeys_WhenFocused_TriggerPlaybackQueueActions() {
    using var context = With.A<Runnable>(100, 20, TestDriver.ANSI.ToString());
    var nowPlayingBar = new NowPlayingBar(_fakeMainWindow, _mockPlaybackQueue.Object, _commandService, _mockStreamingClient.Object);

    var searchList = new SearchResultsList<string> {
      Width = Dim.Fill(),
      Height = Dim.Fill(),
    };
    searchList.SetSource(new ObservableCollection<string>([
      "Alpha",
      ",Comma Band",
      ".Period Track",
      "[Soundtrack]",
      "]Right Bracket",
      " Space Track",
      ":Command",
      "/Filter",
    ]));

    var container = new View {
      Width = Dim.Fill(),
      Height = Dim.Fill(),
      CanFocus = true,
    };
    container.Add(nowPlayingBar, searchList);
    context.AddAndLayout(container);

    searchList.SetFocus();
    Assert.True(searchList.HasFocus);

    // Verify Space -> PlayPause
    _mockPlaybackQueue.Setup(q => q.PlayPause()).Returns(Task.CompletedTask).Verifiable(Times.Once());
    context.KeyDown(Key.Space);
    _mockPlaybackQueue.Verify(q => q.PlayPause(), Times.Once());

    // Verify Ctrl+Space -> Stop
    _mockPlaybackQueue.Setup(q => q.Stop()).Verifiable(Times.Once());
    context.KeyDown(Key.Space.WithCtrl);
    _mockPlaybackQueue.Verify(q => q.Stop(), Times.Once());

    // Verify , -> PreviousTrack
    _mockPlaybackQueue.Setup(q => q.PreviousTrack(false, null)).Returns(Task.CompletedTask).Verifiable(Times.Once());
    context.KeyDown(NowPlayingBar.PreviousKey);
    _mockPlaybackQueue.Verify(q => q.PreviousTrack(false, null), Times.Once());

    // Verify . -> NextTrack
    _mockPlaybackQueue.Setup(q => q.NextTrack()).Returns(Task.CompletedTask).Verifiable(Times.Once());
    context.KeyDown(NowPlayingBar.NextKey);
    _mockPlaybackQueue.Verify(q => q.NextTrack(), Times.Once());

    // Verify [ -> SeekBackward
    _mockPlaybackQueue.Setup(q => q.SeekBackward(TimeSpan.FromSeconds(10))).Verifiable(Times.Once());
    context.KeyDown(NowPlayingBar.SeekBackwardKey);
    _mockPlaybackQueue.Verify(q => q.SeekBackward(TimeSpan.FromSeconds(10)), Times.Once());

    // Verify ] -> SeekForward
    _mockPlaybackQueue.Setup(q => q.SeekForward(TimeSpan.FromSeconds(10))).Verifiable(Times.Once());
    context.KeyDown(NowPlayingBar.SeekForwardKey);
    _mockPlaybackQueue.Verify(q => q.SeekForward(TimeSpan.FromSeconds(10)), Times.Once());
  }

  /// <summary>
  /// Verifies that pressing Enter selects the focused search result item.
  /// </summary>
  [Fact]
  public void Enter_SelectsSearchResult() {
    using var context = With.A<Runnable>(100, 20, TestDriver.ANSI.ToString());
    var searchList = new SearchResultsList<string> {
      Width = Dim.Fill(),
      Height = Dim.Fill(),
    };
    searchList.SetSource(new ObservableCollection<string>(["First", "Second"]));
    searchList.SelectedItem = 0;
    context.AddAndLayout(searchList);
    searchList.SetFocus();

    string? selectedItem = null;
    searchList.SearchResultSelected += (_, item) => selectedItem = item;

    context.KeyDown(Key.Enter);

    Assert.Equal("First", selectedItem);
  }
}
