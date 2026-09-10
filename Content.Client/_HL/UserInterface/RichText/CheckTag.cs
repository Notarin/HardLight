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
            _ => "☐"
        };
    }

    protected override Dictionary<string, object>? GetArguments(MarkupNode node)
    {
        return new Dictionary<string, object>
        {
            ["value"] = node.Value.StringValue ?? ""
        };
    }
}