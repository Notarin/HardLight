using Robust.Shared.Utility;

namespace Content.Client.UserInterface.RichText;

/// <summary>
/// Tag that makes a form.
/// </summary>
public sealed class FormTag : FillableTag
{
    private const int DefaultLength = 25;

    protected override string GetText(MarkupNode node)
    {
        return new string('_', (int)(node.Value.LongValue ?? DefaultLength));
    }

    protected override Dictionary<string, object>? GetArguments(MarkupNode node)
    {
        return new Dictionary<string, object>
        {
            ["maxLen"] = (int)(node.Value.LongValue ?? DefaultLength),
        };
    }
}
