using HanabePhotoManager.Core.Collage;
using Xunit;
namespace HanabePhotoManager.Core.Tests;
public class CollageLayoutTests
{
    [Theory]
    [InlineData(CollageLayout.Grid)]
    [InlineData(CollageLayout.Adaptive)]
    [InlineData(CollageLayout.Magazine)]
    [InlineData(CollageLayout.Long)]
    public void PlansPreserveEveryInputAndBounds(CollageLayout layout)
    {
        var photos = Enumerable.Range(0, 7).Select(i => new CollagePhoto(i.ToString(), 100 + i * 30, 200)).ToArray();
        var plans = CollagePlanner.Candidates(photos, new CollageSettings(1200, 1600, layout));
        Assert.Equal(3, plans.Count);
        foreach (var plan in plans)
        {
            Assert.Equal(photos.Select(p => p.Id), plan.Slots.Select(s => s.Id));
            Assert.All(plan.Slots, s => { Assert.True(s.Width > 0 && s.Height > 0); Assert.True(s.X >= 0 && s.Y >= 0 && s.X + s.Width <= plan.Width && s.Y + s.Height <= plan.Height); });
        }
    }
    [Fact]
    public void MagazineHeroKeepsActualReadingOrder()
    {
        var photos = Enumerable.Range(0, 7).Select(i => new CollagePhoto(i.ToString(), 100, 200)).ToArray();
        var plan = CollagePlanner.Candidates(photos, new(1200, 1600, CollageLayout.Magazine, HeroId: "5"))[0];
        Assert.Equal(photos.Select(p => p.Id), plan.Slots.OrderBy(s => s.Y).ThenBy(s => s.X).Select(s => s.Id));
        Assert.True(plan.Slots[5].Width > plan.Slots[4].Width);
    }
    [Fact]
    public void CandidatesAndRegenerationProduceDifferentGeometry()
    {
        var photos = new[] { new CollagePhoto("a", 100, 100), new CollagePhoto("b", 100, 100) };
        var first = CollagePlanner.Candidates(photos, new(1200, 1600, CollageLayout.Long));
        Assert.NotEqual(first[0].Slots[0], first[1].Slots[0]);
        Assert.NotEqual(first[0].Slots[0], CollagePlanner.Candidates(photos, new(1200, 1600, CollageLayout.Long, Generation: 1))[0].Slots[0]);
    }
    [Fact]
    public void RejectsOverflowingMarginsAndHeroIsLargerThanEveryOtherSlot()
    {
        var photos = Enumerable.Range(0, 7).Select(i => new CollagePhoto(i.ToString(), 100, 100)).ToArray();
        Assert.Throws<ArgumentException>(() => CollagePlanner.Candidates(photos, new(1200, 1600, Margin: int.MaxValue)));
        Assert.Throws<ArgumentException>(() => CollagePlanner.Candidates(photos, new(1200, 1600, Gap: int.MaxValue)));
        var plan = CollagePlanner.Candidates(photos, new(1200, 1600, CollageLayout.Magazine, HeroId: "1"))[0]; var hero = plan.Slots[1];
        Assert.All(plan.Slots.Where(s => s.Id != "1"), s => Assert.True(hero.Width * hero.Height > s.Width * s.Height));
    }
    [Fact] public void MemoryBudgetIncludesIccConversionBuffers() { Assert.False(CollagePlanner.FitsMemoryBudget(60_000_000, 20_000_000)); Assert.True(CollagePlanner.FitsMemoryBudget(8_640_000, 10_000_000)); Assert.False(CollagePlanner.FitsMemoryBudget(long.MaxValue, 1)); }
    [Fact] public void RejectsBudgetBeforeAllocation() => Assert.Throws<ArgumentException>(() => CollagePlanner.Candidates([new("a", 10, 10)], new(100000, 100000)));
}



