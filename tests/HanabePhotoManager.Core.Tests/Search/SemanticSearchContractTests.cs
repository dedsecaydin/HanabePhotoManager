namespace HanabePhotoManager.Core.Tests.Search;

public sealed class SemanticSearchContractTests
{
    [Fact]
    public void SearchContracts_AreExposedFromCore()
    {
        Assert.NotNull(typeof(HanabePhotoManager.Core.Imports.ImportPlanBuilder).Assembly.GetType("HanabePhotoManager.Core.Search.ISemanticSearchService"));
        Assert.NotNull(typeof(HanabePhotoManager.Core.Imports.ImportPlanBuilder).Assembly.GetType("HanabePhotoManager.Core.Search.ISemanticIndexStore"));
        Assert.NotNull(typeof(HanabePhotoManager.Core.Imports.ImportPlanBuilder).Assembly.GetType("HanabePhotoManager.Core.Search.SemanticSearchResult"));
    }
}
