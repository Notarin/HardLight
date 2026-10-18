using Content.Shared.Players.PlayTimeTracking;

namespace Content.Client.Players.PlayTimeTracking;

public sealed partial class JobRequirementsManager
{
    private bool _whitelisted = false;

    private void RxWhitelist(MsgWhitelist message)
    {
        _sawmill.Debug($"Received new whitelist status: {message.Whitelisted}, previously {_whitelisted}");
        _whitelisted = message.Whitelisted;
    }

    public bool IsWhitelisted()
    {
        return _whitelisted;
    }
}
