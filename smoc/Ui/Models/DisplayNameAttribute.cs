namespace Smoc.Ui.Models;

/// <summary>
/// Specifies a user-friendly display name for an enum value or field.
/// </summary>
/// <param name="displayName">The display name to use.</param>
[AttributeUsage(AttributeTargets.Field)]
public class DisplayNameAttribute(string displayName) : System.Attribute {
  /// <summary>
  /// Gets the display name.
  /// </summary>
  public string DisplayName { get; } = displayName;
}
