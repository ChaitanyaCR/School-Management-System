namespace SchoolMs.Modules.ModuleName.UnitTests;

public class ModuleNameModuleTests
{
    [Fact]
    public void Name_IsLowercaseSlug()
    {
        new ModuleNameModule().Name.ShouldBe("module-slug");
    }
}
