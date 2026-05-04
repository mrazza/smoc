using System.Drawing;
using Smoc.Services;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Smoc.Ui.Components;

/// <summary>
/// A context menu for songs in the <see cref="SongTable" />, providing playback options.
/// </summary>
public sealed class SongContextMenu : PopoverMenu {
  /// <summary>
  /// Messages for the song context menu. Internally visible for testing purposes.
  /// </summary>
  internal static class Messages {
    public const string PLAY_ALL = "_play all from here";
    public const string PLAY_SELECTION = "play selection _only";
    public const string PLAY_NEXT = "queue _next";
    public const string ADD_TO_QUEUE = "_queue last";
  }

  private readonly SongTable _songTable;

  /// <summary>
  /// Initializes a new instance of the <see cref="SongContextMenu"/> class.
  /// </summary>
  /// <param name="playerService">The player service to control playback.</param>
  /// <param name="songTable">The song table to get selection from.</param>
  public SongContextMenu(IPlaybackQueueService playbackQueueService, SongTable songTable)
      : base(CreateMenuItems(playbackQueueService, songTable)) {
    _songTable = songTable;

    // Default PopoverMenu binds Left/Right for navigation (Bar behavior).
    // specific to Menu behavior we want Up/Down for vertical list.
    // Map Up to Previous (Backward)
    AddCommand(Command.Up, ctx => AdvanceFocus(NavigationDirection.Backward, TabBehavior.TabStop));
    KeyBindings.Add(Key.CursorUp, Command.Up);
    // Map Down to Next (Forward)
    AddCommand(Command.Down, ctx => AdvanceFocus(NavigationDirection.Forward, TabBehavior.TabStop));
    KeyBindings.Add(Key.CursorDown, Command.Down);
    VimKeyBindings.AddDirectionalKeyBindings(KeyBindings, bindLeftRight: false);
    KeyBindings.Remove(Key.CursorLeft);
    KeyBindings.Remove(Key.CursorRight);

    // Map Esc to Quit (Close Popover)
    KeyBindings.ReplaceCommands(Key.Esc, Command.Quit);

    Accepting += (sender, e) => {
      Visible = false;
    };
    Visible = false;
  }

  public override void EndInit() {
    base.EndInit();

    App!.Popovers?.Register(this);
  }

  /// <summary>
  /// The height required to display all menu items.
  /// </summary>
  public int RequiredHeight => Root?.SubViews.Count ?? 0;

  /// <summary>
  /// Makes the context menu visible for the given song table constrained within the bounds of the provided view.
  /// </summary>
  /// <remarks>
  /// The context menu will be displayed at the bottom of the selected row if there is enough space below it
  /// in the provided view, otherwise it will be displayed above the selected row.
  /// </remarks>
  /// <param name="view">The view to display the context menu in.</param>
  public void MakeVisibleInView(View view) {
    var tableAdornments = _songTable.GetAdornmentsThickness();
    var yPos = _songTable.GetSelectedRowFramePosition().Y + tableAdornments.Top + 1;
    int menuHeight = RequiredHeight;

    if (yPos + menuHeight > view.Frame.Height) {
      // Not enough space below the row, so put it above
      // Let's position it so the bottom of the menu ends at p.Y
      yPos = yPos - menuHeight - 1;
    }

    MakeVisible(new Point(_songTable.Frame.X + tableAdornments.Left, yPos));
  }

  private static List<MenuItem> CreateMenuItems(IPlaybackQueueService playbackQueueService, SongTable songTable) {
    var menuItems = new List<MenuItem> {
      new(Messages.PLAY_ALL, action: async () => {
        playbackQueueService.ClearPlaybackQueue();
        playbackQueueService.QueueLast(songTable.GetSongs());
        await playbackQueueService.ChangeTrack(songTable.SelectedRow);
        await playbackQueueService.Play();
      }),
      new(Messages.PLAY_SELECTION, action: async () => {
        playbackQueueService.ClearPlaybackQueue();
        playbackQueueService.QueueLast(songTable.GetSelectedSongs());
        await playbackQueueService.ChangeTrack(0);
        await playbackQueueService.Play();
      }),
      new(Messages.PLAY_NEXT, action: () => {
        playbackQueueService.QueueNext(songTable.GetSelectedSongs());
      }),
      new(Messages.ADD_TO_QUEUE, action: () => {
        playbackQueueService.QueueLast(songTable.GetSelectedSongs());
      })
    };

    return menuItems;
  }

  protected override void Dispose(bool disposing) {
    if (disposing && (App?.Popovers?.IsRegistered(this) ?? false)) {
      App?.Popovers?.DeRegister(this);
    }

    base.Dispose(disposing);
  }
}
