using Edm.Identity.Domain.Organisation;

namespace Edm.UnitTests.Identity;

public sealed class OrganisationRulesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_department_name_is_refused(string? name)
        => Assert.Equal("Enter a department name.", OrganisationRules.DepartmentNameError(name));

    [Fact]
    public void A_department_name_over_200_characters_is_refused()
        => Assert.Equal("The department name must be 200 characters or fewer.", OrganisationRules.DepartmentNameError(new string('a', 201)));

    [Theory]
    [InlineData("EMP1024")]
    [InlineData("e10")]
    public void Letters_and_numbers_are_accepted_as_employee_ids(string employeeId)
        => Assert.Null(OrganisationRules.EmployeeIdError(employeeId));

    [Theory]
    [InlineData("EMP-1024")]
    [InlineData("EMP 1024")]
    [InlineData("EMP_1")]
    public void Other_characters_are_refused_in_employee_ids(string employeeId)
        => Assert.Equal("Use letters and numbers only, for example EMP1024.", OrganisationRules.EmployeeIdError(employeeId));

    [Fact]
    public void An_employee_id_over_32_characters_is_refused()
        => Assert.Equal("The employee ID must be 32 characters or fewer.", OrganisationRules.EmployeeIdError(new string('1', 33)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_missing_email_is_allowed(string? email)
        => Assert.Null(OrganisationRules.EmailError(email));

    [Theory]
    [InlineData("ali@example.com")]
    [InlineData("  ali.khan@bank.pk  ")]
    public void A_valid_email_is_accepted(string email)
        => Assert.Null(OrganisationRules.EmailError(email));

    [Theory]
    [InlineData("ali")]
    [InlineData("ali@example")]
    [InlineData("ali @example.com")]
    public void An_invalid_email_is_refused(string email)
        => Assert.Equal("Enter a valid email address.", OrganisationRules.EmailError(email));

    [Fact]
    public void Employee_ids_are_stored_trimmed_and_upper_case()
        => Assert.Equal("EMP1024", OrganisationRules.NormaliseEmployeeId("  emp1024 "));
}
