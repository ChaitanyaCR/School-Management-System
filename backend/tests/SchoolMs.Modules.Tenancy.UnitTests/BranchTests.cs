using SchoolMs.Modules.Tenancy.Domain;

namespace SchoolMs.Modules.Tenancy.UnitTests;

public class BranchTests
{
    private static readonly Guid OrganisationId = Guid.CreateVersion7();

    [Fact]
    public void Create_UppercasesCodeAndStartsActive()
    {
        var branch = Branch.Create(OrganisationId, " North Campus ", " north ", TimeProvider.System).Value;

        branch.OrganisationId.ShouldBe(OrganisationId);
        branch.Name.ShouldBe("North Campus");
        branch.Code.ShouldBe("NORTH");
        branch.IsActive.ShouldBeTrue();
    }

    [Theory]
    [InlineData("N")]
    [InlineData("NORTH-1")]
    [InlineData("ABCDEFGHIJK")]
    public void Create_RejectsInvalidCodes(string code)
    {
        Branch.Create(OrganisationId, "North Campus", code, TimeProvider.System).Error.ShouldBe(TenancyErrors.InvalidBranchCode);
    }

    [Fact]
    public void Deactivate_MarksInactive()
    {
        var branch = Branch.Create(OrganisationId, "North Campus", "NORTH", TimeProvider.System).Value;

        branch.Deactivate();

        branch.IsActive.ShouldBeFalse();
    }
}
