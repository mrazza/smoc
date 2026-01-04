namespace Smoc.Ui.Models;

internal record SearchResultRow<T>(T Item, string DisplayText)
{
    public override string ToString() => DisplayText;
}
