using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Content.Client.Paper.UI;
using Content.Client.RichText;
using Content.Shared.Paper;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Input;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.RichText;

/// <summary>
/// Abstract base class for tags that create interactive controls in a markup text.
/// </summary>
/// <remarks>
/// Every concrete implementation must be added to <see cref="UserFormattableTags"/>.
/// The handling control is expected to subscribe to <see cref="OnTagClicked"/> with the desired action and
/// use <see cref="IndexedText"/> before adding the markup.
/// </remarks>
/// <seealso cref="PaperWindow.OnTagClicked"/> <seealso cref="PaperSystem"/>
public abstract class FillableTag : IMarkupTagHandler
{
    private static readonly IReadOnlyDictionary<Type, string> FillTagNames = UserFormattableTags.FillableTagNames;

    public string Name => FillTagNames.GetValueOrDefault(GetType())
                            ?? throw new NotImplementedException("This tag hasn't implemented its name in UserFormattableTags.cs");

    /// <summary>
    /// Enables or disable all interactive tags.
    /// When disabled, fillable tags will usually return plain labels of their text.
    /// Or another non-interactive representation.
    /// </summary>
    public static bool Enabled { get; set; }


    /// <summary>
    /// Bounced back to the owning control since we can't
    /// access the controls if we don't.
    /// Invoked with the tag's control, its name, its index, and a dictionary for named arguments.
    /// </summary>
    /// <seealso cref="PaperWindow"/>
    public static event Action<Control, string, int, Dictionary<string, object>?>? OnTagClicked;

    // Not 100% but I don't really want to handle the last edge case, and it has inconsistent behaviour when parsed anyway.
    private static readonly Regex Replacer = new($"""\[\s*(({string.Join('|', FillTagNames.Values.Select(Regex.Escape))})(?:\s*=\s*(?:[A-Za-z0-9#]+|"[^"]*"))?(?:\s+\w+\s*(?:=\s*(?:[A-Za-z0-9#]+|"[^"]*"))?)*)\s*\]""");
    private static readonly Regex BugIndex = new(@"\si=\S+");

    /// <summary>
    /// Inject all the indexes for all fillable tags in the text provided and returns the result.
    /// It will add a node attribute 'i' to all fillable tags and aggressively overwrite it
    /// if already defined.<br />
    /// Relies on <see cref="UserFormattableTags.FillableTagNames"/>.
    /// </summary>
    /// <remarks>
    /// Overlooks an edge case where node string attributes that never close
    /// " are valid according to Robust but not according to this method or
    /// the replacer function.
    /// </remarks>
    /// <param name="text">The text with fillable markup tags</param>
    /// <returns>The tagged text. with indices</returns>
    public static string IndexedText(string text)
    {
        var counts = new Dictionary<string, int>();

        var result = Replacer.Replace(text, match =>
        {
            var tag = match.Groups[2].Value;

            var count = counts.GetValueOrDefault(tag);
            counts[tag] = count + 1;

            return $"[{BugIndex.Replace(match.Groups[1].Value, _ => "")} i={count}]";

        });

        return result;
    }

    /// <summary>
    /// Expected text for the label built by <see cref="TryCreateControl"/>.
    /// </summary>
    /// <param name="node">the markup node passed to <see cref="TryCreateControl"/></param>
    /// <remarks>Do not rely on the 'i' node attribute. It is reserved for indexing.</remarks>
    protected virtual string GetText(MarkupNode node) => Name;

    /// <summary>
    /// Additional arguments for <see cref="OnTagClicked"/>.
    /// </summary>
    /// <param name="node">the markup node passed to <see cref="TryCreateControl"/></param>
    /// <remarks>Do not rely on the 'i' node attribute. It is reserved for indexing.</remarks>
    protected virtual Dictionary<string, object>? GetArguments(MarkupNode node) => default;

    /// <summary>
    /// Helper to get the index of a markup tag.
    /// Here to limit the dependency of a <see cref="TryCreateControl"/> override
    /// on <see cref="IndexedText"/>.
    /// </summary>
    /// <param name="node">the markup node passed to <see cref="TryCreateControl"/></param>
    /// <returns>The index used for replacing the markuptag.</returns>
    protected static long? GetIndexOrNull(MarkupNode node)
    {
        return node.Attributes.GetValueOrDefault("i").LongValue;
    }

    /// <inheritdoc />
    public virtual bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        var text = GetText(node);

        var index = GetIndexOrNull(node);
        if (index is null)
            return false;

        if (!Enabled)
        {
            control = new Label { Text = text };
            return true;
        }

        var label = new Label
        {
            Text = text,
            MouseFilter = Control.MouseFilterMode.Stop,
            FontColorOverride = Color.CornflowerBlue,
            DefaultCursorShape = Control.CursorShape.Hand,
            Margin = new Thickness(2, 0)
        };

        label.OnMouseEntered += _ => label.FontColorOverride = Color.LightSkyBlue;
        label.OnMouseExited += _ => label.FontColorOverride = Color.CornflowerBlue;

        label.OnKeyBindDown += args =>
        {
            if (args.Function != EngineKeyFunctions.UIClick)
                return;

            OnTagClicked?.Invoke(label, Name, (int)index, GetArguments(node));
        };

        control = label;
        return true;

    }
}
