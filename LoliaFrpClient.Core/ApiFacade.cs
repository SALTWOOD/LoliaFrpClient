using LoliaFrpClient.Api;

namespace LoliaFrpClient.Core;

public abstract class ApiFacade
{
    protected ApiFacade() : this(ApiSession.Current)
    {
    }

    protected ApiFacade(ApiSession session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
    }

    protected ApiSession Session { get; }

    public ApiSession Api => Session;

    protected ApiClient Client => Session.Client;
}