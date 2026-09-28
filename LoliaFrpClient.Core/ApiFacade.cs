using LoliaFrpClient.Api;

namespace LoliaFrpClient.Core;

/// <summary>
///     所有门面类的基类,负责把会话传递给具体门面。
/// </summary>
public abstract class ApiFacade
{
    /// <summary>使用当前会话构造。</summary>
    protected ApiFacade() : this(ApiSession.Current)
    {
    }

    /// <summary>使用指定会话构造。</summary>
    protected ApiFacade(ApiSession session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
    }

    /// <summary>本门面绑定的会话。实体类由父对象产出时,会继承同一个会话,凭证自然贯通。</summary>
    protected ApiSession Session { get; }

    /// <summary>本门面绑定的会话。供派生类型之外读取(例如把子实体绑定到同一会话)。</summary>
    public ApiSession Api => Session;

    /// <summary>生成的 Kiota 客户端。</summary>
    protected ApiClient Client => Session.Client;
}
