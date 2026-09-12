using System.Text.RegularExpressions;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.RichText;

public sealed class TimeTag : FillableTag
{
    protected override string GetText(MarkupNode node)
    {
        return node.Value.StringValue == "shift"
            ? "#______"
            : "__:__:__";
    }

    private static readonly Regex FormatValidator = new(@"^(?:hh(?:(?::|t)mm(?::ss)?)?|mm(?::ss)?|ss)$");

    protected override Dictionary<string, object>? GetArguments(MarkupNode node)
    {
        var format = node.Attributes.GetValueOrDefault("format").StringValue ?? "";

        format = format == "#" || FormatValidator.IsMatch(format)
            ? format.Replace(":", @"\:").Replace("t", @"\h")
            : "";

        return new Dictionary<string, object>
        {
            ["type"] = node.Value.StringValue ?? "",
            ["format"] = format,
        };
    }
}
