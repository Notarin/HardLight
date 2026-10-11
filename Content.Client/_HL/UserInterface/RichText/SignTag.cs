using Robust.Shared.Utility;

namespace Content.Client.UserInterface.RichText;

public sealed class SignTag : FillableTag
{
    protected override string GetText(MarkupNode node)
    {
        return $"[{node.Value.StringValue switch
        {
            "name" => $"{Loc.GetString("paper-tag-sign-name")}",
            "job" => $"{Loc.GetString("paper-tag-sign-job")}",
            _ => $"{Loc.GetString("paper-tag-sign-default")}",
        }}]";
    }

    protected override Dictionary<string, object>? GetArguments(MarkupNode node)
    {
        return new Dictionary<string, object>
        {
            ["type"] = node.Value.StringValue ?? "",
        };
    }
}
