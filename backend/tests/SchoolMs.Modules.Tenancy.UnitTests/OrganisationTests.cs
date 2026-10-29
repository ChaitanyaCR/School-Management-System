using SchoolMs.Modules.Tenancy.Domain;

namespace SchoolMs.Modules.Tenancy.UnitTests;

public class OrganisationTests
{
    [Fact]
    public void Create_NormalisesSlugAndStartsActive()
    {
        var result = Organisation.Create("  Greenwood Group ", " GreenWood-Schools ", TimeProvider.System);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Greenwood Group");
        result.Value.Slug.ShouldBe("greenwood-schools");
        result.Value.IsActive.ShouldBeTrue();
    }

    [Theory]
    [InlineData("ab")] // too short
    [InlineData("-greenwood")] // leading hyphen
    [InlineData("greenwood-")] // trailing hyphen
    [InlineData("green wood")]
    [InlineData("green.wood")] // would create a nested subdomain
    [InlineData("grëenwood")]
    [InlineData("abcdefghijklmnopqrstuvwxyz12345")] // 31 characters
    public void Create_RejectsInvalidSlugs(string slug)
    {
        var result = Organisation.Create("Greenwood", slug, TimeProvider.System);

        result.Error.ShouldBe(TenancyErrors.InvalidSlug);
    }

    [Theory]
    [InlineData("www")]
    [InlineData("api")]
    [InlineData("admin")]
    public void Create_RejectsReservedSlugs(string slug)
    {
        Organisation.Create("Greenwood", slug, TimeProvider.System).Error.ShouldBe(TenancyErrors.ReservedSlug);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" A ")]
    public void Create_RejectsInvalidNames(string name)
    {
        Organisation.Create(name, "greenwood", TimeProvider.System).Error.ShouldBe(TenancyErrors.InvalidName);
    }

    [Fact]
    public void SuspendAndActivate_ToggleStatus()
    {
        var organisation = Organisation.Create("Greenwood", "greenwood", TimeProvider.System).Value;

        organisation.Suspend();
        organisation.IsActive.ShouldBeFalse();

        organisation.Activate();
        organisation.IsActive.ShouldBeTrue();
    }
}
