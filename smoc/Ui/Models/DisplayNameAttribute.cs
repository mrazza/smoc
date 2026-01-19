namespace Smoc.Ui.Models;

[AttributeUsage(AttributeTargets.Field)]
public class DisplayNameAttribute(string displayName) : System.Attribute {
  public string DisplayName { get; } = displayName;
}
