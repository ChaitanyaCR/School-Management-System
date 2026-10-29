namespace SchoolMs.Modules.Tenancy.UnitTests;

public class TenancyModuleTests
{
    [Fact]
    public void Name_IsLowercaseSlug()
    {
        new TenancyModule().Name.ShouldBe("tenancy");
    }
}
