using SchoolMs.Modules.Identity.Domain;

namespace SchoolMs.Modules.Identity.UnitTests;

public class UserTests
{
    private static readonly Guid OrganisationId = Guid.CreateVersion7();

    [Fact]
    public void Invite_NormalisesInputAndStartsInvited()
    {
        var user = User.Invite(OrganisationId, "  Meena Iyer ", " Meena.Iyer@School.TEST ", "098765 43210", TimeProvider.System).Value;

        user.OrganisationId.ShouldBe(OrganisationId);
        user.FullName.ShouldBe("Meena Iyer");
        user.Email.ShouldBe("meena.iyer@school.test");
        user.PhoneNumber.ShouldBe("+919876543210");
        user.Status.ShouldBe(UserStatus.Invited);
        user.IsActive.ShouldBeFalse();
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("meena@localhost")]
    [InlineData("Meena <meena@school.test>")]
    [InlineData("meena@@school.test")]
    public void Invite_RejectsInvalidEmails(string email)
    {
        User.Invite(OrganisationId, "Meena Iyer", email, null, TimeProvider.System).Error.ShouldBe(IdentityErrors.InvalidEmail);
    }

    [Fact]
    public void Invite_RejectsInvalidNames()
    {
        User.Invite(OrganisationId, " M ", "meena@school.test", null, TimeProvider.System).Error.ShouldBe(IdentityErrors.InvalidName);
    }

    [Fact]
    public void Invite_RejectsInvalidPhoneNumbers()
    {
        User.Invite(OrganisationId, "Meena Iyer", "meena@school.test", "12345", TimeProvider.System).Error
            .ShouldBe(IdentityErrors.InvalidPhoneNumber);
    }

    [Fact]
    public void AcceptInvitation_Activates()
    {
        var user = User.Invite(OrganisationId, "Meena Iyer", "meena@school.test", null, TimeProvider.System).Value;

        user.AcceptInvitation(TimeProvider.System);

        user.IsActive.ShouldBeTrue();
        user.ActivatedAt.ShouldNotBeNull();
    }
}

public class PhoneNumbersTests
{
    [Theory]
    [InlineData("9876543210")]
    [InlineData("98765 43210")]
    [InlineData("098765-43210")]
    [InlineData("+91 98765 43210")]
    [InlineData("91 9876543210")]
    [InlineData("(+91) 98765-43210")]
    public void Normalise_AcceptsCommonFormats(string input)
    {
        PhoneNumbers.Normalise(input).Value.ShouldBe("+919876543210");
    }

    [Theory]
    [InlineData("5876543210")] // Indian mobile numbers start with 6–9
    [InlineData("987654321")]
    [InlineData("98765432101")]
    [InlineData("+1 415 555 0100")]
    [InlineData("98765.43210")]
    [InlineData("")]
    public void Normalise_RejectsOthers(string input)
    {
        PhoneNumbers.Normalise(input).Error.ShouldBe(IdentityErrors.InvalidPhoneNumber);
    }
}
