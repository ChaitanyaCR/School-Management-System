namespace SchoolMs.Modules.Identity.UnitTests;

public class IdentityModuleTests
{
    [Fact]
    public void Name_IsLowercaseSlug()
    {
        new IdentityModule().Name.ShouldBe("identity");
    }
}
