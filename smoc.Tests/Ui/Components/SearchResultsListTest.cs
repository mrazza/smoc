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

  [Fact]
  public void Constructor_RemovesSpaceKeyBinding() {
    var searchList = new SearchResultsList<string>();
    Assert.False(searchList.KeyBindings.TryGet(Key.Space, out _));
  }

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
