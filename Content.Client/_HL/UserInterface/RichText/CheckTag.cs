using Robust.Shared.Utility;

namespace Content.Client.UserInterface.RichText;

public sealed class CheckTag : FillableTag
{
    protected override string GetText(MarkupNode node)
    {
        return (node.Value.StringValue ?? "") switch
        {
            "ok" => "✓",
            "cross" => "✗ ",
            "" => "☐", //   
            _ => "☐",
        };
    }
}
