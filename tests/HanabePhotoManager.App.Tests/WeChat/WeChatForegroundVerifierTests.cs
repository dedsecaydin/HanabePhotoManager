using FluentAssertions;
using HanabePhotoManager.App.WeChat;
using Xunit;

namespace HanabePhotoManager.App.Tests.WeChat;

public sealed class WeChatForegroundVerifierTests
{
    [Fact]
    public void IsVerifiedForeground_RejectsPidOutsideVerifiedSet()
    {
        WeChatForegroundVerifier.IsVerifiedForeground(88, [42, 43]).Should().BeFalse();
    }

    [Fact]
    public void IsVerifiedForeground_AcceptsPidInsideVerifiedSet()
    {
        WeChatForegroundVerifier.IsVerifiedForeground(42, [42, 43]).Should().BeTrue();
    }
}
