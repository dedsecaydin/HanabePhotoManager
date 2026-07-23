using FluentAssertions;
using HanabePhotoManager.Core.Cloud;

namespace HanabePhotoManager.Core.Tests.Cloud;

public sealed class CloudSchedulingPolicyTests
{
    public static TheoryData<bool, bool, bool, bool, bool, bool> AllSchedulingStates
    {
        get
        {
            var states = new TheoryData<bool, bool, bool, bool, bool, bool>();
            foreach (var importing in BooleanValues)
            foreach (var quarkRunning in BooleanValues)
            foreach (var highResolutionPreview in BooleanValues)
            foreach (var networkBusy in BooleanValues)
            foreach (var baiduCapacityAvailable in BooleanValues)
            foreach (var baiduAuthenticated in BooleanValues)
            {
                states.Add(
                    importing,
                    quarkRunning,
                    highResolutionPreview,
                    networkBusy,
                    baiduCapacityAvailable,
                    baiduAuthenticated);
            }

            return states;
        }
    }

    private static bool[] BooleanValues => [false, true];

    [Theory]
    [MemberData(nameof(AllSchedulingStates))]
    public void CanRunBaidu_MatchesCompleteSchedulingTruthTable(
        bool importing,
        bool quarkRunning,
        bool highResolutionPreview,
        bool networkBusy,
        bool baiduCapacityAvailable,
        bool baiduAuthenticated)
    {
        var state = new CloudSchedulingState(
            importing,
            quarkRunning,
            highResolutionPreview,
            networkBusy,
            baiduCapacityAvailable,
            baiduAuthenticated);
        var expected =
            !importing &&
            !quarkRunning &&
            !highResolutionPreview &&
            !networkBusy &&
            baiduCapacityAvailable &&
            baiduAuthenticated;

        CloudSchedulingPolicy.CanRunBaidu(state).Should().Be(expected);
    }

    [Fact]
    public void CanRunBaidu_AllowsRunAfterQuarkIsNoLongerRunning()
    {
        var stateAfterQuarkCompletion = new CloudSchedulingState(
            ImportRunning: false,
            QuarkRunning: false,
            HighResolutionPreviewRunning: false,
            NetworkBusy: false,
            BaiduCapacityAvailable: true,
            BaiduAuthenticated: true);

        CloudSchedulingPolicy.CanRunBaidu(stateAfterQuarkCompletion).Should().BeTrue();
    }

    [Fact]
    public void CanRunBaidu_RejectsNullState()
    {
        var act = () => CloudSchedulingPolicy.CanRunBaidu(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CanRunBaidu_IsStatelessUnderConcurrentCalls()
    {
        var idleState = new CloudSchedulingState(false, false, false, false, true, true);
        var busyState = new CloudSchedulingState(false, false, false, true, true, true);
        var results = new bool[1_000];

        Parallel.For(0, results.Length, index =>
        {
            var state = index % 2 == 0 ? idleState : busyState;
            results[index] = CloudSchedulingPolicy.CanRunBaidu(state);
        });

        results.Where((_, index) => index % 2 == 0).Should().OnlyContain(value => value);
        results.Where((_, index) => index % 2 != 0).Should().OnlyContain(value => !value);
    }
}
