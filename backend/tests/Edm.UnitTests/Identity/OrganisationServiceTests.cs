using Edm.Audit.Application;
using Edm.Identity.Application.Organisation;
using Edm.Identity.Domain.Organisation;
using Edm.SharedKernel.Context;
using Edm.SharedKernel.Errors;
using Edm.UnitTests.Configuration;
using Edm.UnitTests.Support;

namespace Edm.UnitTests.Identity;

public sealed class OrganisationServiceTests
{
    private static readonly Guid Tenant = CategoryServiceTests.Tenant;
    private static readonly Guid Actor = CategoryServiceTests.Actor;
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 4, 0, 0, TimeSpan.Zero);

    private sealed class Rig
    {
        public Rig()
        {
            var clock = new FixedClock(Now);
            var writer = new AuditWriter(
                Audit,
                clock,
                new FixedTenantContext(Tenant),
                new FixedActor(Actor, "Test Admin", "Administration", "SYSTEM_ADMINISTRATOR"),
                new FixedCorrelation("corr-organisation"));
            var tenant = new FixedTenantContext(Tenant);
            var actor = new FixedActor(Actor, "Test Admin", "Administration");

            Departments = new DepartmentService(Store, writer, tenant, actor, clock);
            Users = new DirectoryUserService(Store, writer, tenant, actor, clock);
        }

        public InMemoryOrganisationStore Store { get; } = new();

        public FakeAuditStore Audit { get; } = new();

        public DepartmentService Departments { get; }

        public DirectoryUserService Users { get; }
    }

    private static async Task<string> FieldMessage(Func<Task> action, string field)
    {
        var error = await Assert.ThrowsAsync<EdmException>(action);
        Assert.Equal(EdmErrorCodes.ValidationFailed, error.Error);
        return Assert.Single(error.FieldErrors![field]);
    }

    private static DirectoryUserCreate Person(string employeeId, Guid? departmentId, string? email = null)
        => new(employeeId, "Ali Khan", email, departmentId);

    [Fact]
    public async Task A_department_name_is_trimmed_and_stored()
    {
        var rig = new Rig();

        var created = await rig.Departments.CreateAsync(new DepartmentCreate("  Finance  "), default);

        Assert.Equal("Finance", created.Name);
        Assert.True(created.IsActive);
        Assert.Equal(1, created.RowVersion);
    }

    [Fact]
    public async Task A_duplicate_active_department_name_is_a_field_error_and_ignores_case()
    {
        var rig = new Rig();
        await rig.Departments.CreateAsync(new DepartmentCreate("Finance"), default);

        var message = await FieldMessage(() => rig.Departments.CreateAsync(new DepartmentCreate("  finance "), default), "name");

        Assert.Equal("A department with this name already exists.", message);
    }

    [Fact]
    public async Task A_deactivated_department_name_can_be_reused()
    {
        var rig = new Rig();
        var first = await rig.Departments.CreateAsync(new DepartmentCreate("Finance"), default);
        await rig.Departments.UpdateAsync(first.Id, new DepartmentUpdate(null, IsActive: false, first.RowVersion), default);

        var second = await rig.Departments.CreateAsync(new DepartmentCreate("Finance"), default);

        Assert.Equal("Finance", second.Name);
    }

    [Fact]
    public async Task A_department_with_active_people_cannot_be_deactivated()
    {
        var rig = new Rig();
        var department = await rig.Departments.CreateAsync(new DepartmentCreate("Finance"), default);
        await rig.Users.CreateAsync(Person("EMP1", department.Id), default);

        var message = await FieldMessage(
            () => rig.Departments.UpdateAsync(department.Id, new DepartmentUpdate(null, IsActive: false, department.RowVersion), default),
            "isActive");

        Assert.Equal("Move or deactivate the people in this department first.", message);
    }

    [Fact]
    public async Task A_department_with_only_inactive_people_can_be_deactivated()
    {
        var rig = new Rig();
        var department = await rig.Departments.CreateAsync(new DepartmentCreate("Finance"), default);
        var person = await rig.Users.CreateAsync(Person("EMP1", department.Id), default);
        await rig.Users.UpdateAsync(person.Id, new DirectoryUserUpdate(null, null, null, null, IsActive: false, person.RowVersion), default);

        var updated = await rig.Departments.UpdateAsync(
            department.Id,
            new DepartmentUpdate(null, IsActive: false, department.RowVersion),
            default);

        Assert.False(updated!.IsActive);
    }

    [Fact]
    public async Task A_stale_department_version_is_refused_and_nothing_changes()
    {
        var rig = new Rig();
        var department = await rig.Departments.CreateAsync(new DepartmentCreate("Finance"), default);
        await rig.Departments.UpdateAsync(department.Id, new DepartmentUpdate("Finance Office", null, department.RowVersion), default);

        var error = await Assert.ThrowsAsync<EdmException>(() =>
            rig.Departments.UpdateAsync(department.Id, new DepartmentUpdate("Other", null, department.RowVersion), default));

        Assert.Equal(EdmErrorCodes.StaleUpdate, error.Error);
        Assert.Equal("Finance Office", rig.Store.Departments.Single().Name);
    }

    [Fact]
    public async Task Department_changes_are_audited_with_old_and_new_values()
    {
        var rig = new Rig();
        var department = await rig.Departments.CreateAsync(new DepartmentCreate("Finance"), default);
        await rig.Departments.UpdateAsync(department.Id, new DepartmentUpdate("Finance Office", null, department.RowVersion), default);
        await rig.Departments.UpdateAsync(department.Id, new DepartmentUpdate(null, IsActive: false, 2), default);

        var actions = rig.Audit.Events.Select(e => e.Action).ToArray();
        Assert.Equal(new[] { "Created", "Updated", "Deactivated" }, actions);
        Assert.All(rig.Audit.Events, e => Assert.Equal("Organisation", e.Module));
        Assert.Contains("Finance Office", rig.Audit.Events[1].AfterJson);
        Assert.Contains("Finance", rig.Audit.Events[1].BeforeJson);
        Assert.DoesNotContain("Finance Office", rig.Audit.Events[1].BeforeJson);
    }

    [Fact]
    public async Task A_person_needs_an_active_department()
    {
        var rig = new Rig();
        var department = await rig.Departments.CreateAsync(new DepartmentCreate("Finance"), default);
        await rig.Departments.UpdateAsync(department.Id, new DepartmentUpdate(null, IsActive: false, department.RowVersion), default);

        var message = await FieldMessage(() => rig.Users.CreateAsync(Person("EMP1", department.Id), default), "departmentId");

        Assert.Equal("Choose an active department.", message);
    }

    [Fact]
    public async Task A_person_without_a_department_is_refused()
    {
        var rig = new Rig();

        var message = await FieldMessage(() => rig.Users.CreateAsync(Person("EMP1", null), default), "departmentId");

        Assert.Equal("Choose a department.", message);
    }

    [Fact]
    public async Task A_duplicate_employee_id_is_refused_even_when_the_existing_person_is_inactive()
    {
        var rig = new Rig();
        var department = await rig.Departments.CreateAsync(new DepartmentCreate("Finance"), default);
        var person = await rig.Users.CreateAsync(Person("emp1", department.Id), default);
        await rig.Users.UpdateAsync(person.Id, new DirectoryUserUpdate(null, null, null, null, IsActive: false, person.RowVersion), default);

        var message = await FieldMessage(() => rig.Users.CreateAsync(Person("EMP1", department.Id), default), "employeeId");

        Assert.Equal("This employee ID already exists. Reactivate the existing record instead.", message);
    }

    [Fact]
    public async Task A_person_is_stored_with_an_upper_case_employee_id_and_no_email()
    {
        var rig = new Rig();
        var department = await rig.Departments.CreateAsync(new DepartmentCreate("Finance"), default);

        var created = await rig.Users.CreateAsync(Person(" emp1024 ", department.Id, "  "), default);

        Assert.Equal("EMP1024", created.EmployeeId);
        Assert.Null(created.Email);
        Assert.True(created.IsActive);
    }

    [Fact]
    public async Task An_invalid_email_is_a_field_error()
    {
        var rig = new Rig();
        var department = await rig.Departments.CreateAsync(new DepartmentCreate("Finance"), default);

        var message = await FieldMessage(() => rig.Users.CreateAsync(Person("EMP1", department.Id, "not-an-email"), default), "email");

        Assert.Equal("Enter a valid email address.", message);
    }

    [Fact]
    public async Task Reactivating_a_person_into_a_deactivated_department_is_refused()
    {
        var rig = new Rig();
        var department = await rig.Departments.CreateAsync(new DepartmentCreate("Finance"), default);
        var person = await rig.Users.CreateAsync(Person("EMP1", department.Id), default);
        await rig.Users.UpdateAsync(person.Id, new DirectoryUserUpdate(null, null, null, null, IsActive: false, person.RowVersion), default);
        var deactivated = await rig.Store.FindUserAsync(Tenant, person.Id, default);
        await rig.Departments.UpdateAsync(department.Id, new DepartmentUpdate(null, IsActive: false, department.RowVersion), default);

        var message = await FieldMessage(
            () => rig.Users.UpdateAsync(person.Id, new DirectoryUserUpdate(null, null, null, null, IsActive: true, deactivated!.RowVersion), default),
            "departmentId");

        Assert.Equal("Choose an active department.", message);
    }

    [Fact]
    public async Task Moving_a_person_is_audited_and_a_stale_version_is_refused()
    {
        var rig = new Rig();
        var finance = await rig.Departments.CreateAsync(new DepartmentCreate("Finance"), default);
        var hr = await rig.Departments.CreateAsync(new DepartmentCreate("HR"), default);
        var person = await rig.Users.CreateAsync(Person("EMP1", finance.Id), default);

        var moved = await rig.Users.UpdateAsync(
            person.Id,
            new DirectoryUserUpdate(null, null, null, hr.Id, null, person.RowVersion),
            default);

        Assert.Equal(hr.Id, moved!.DepartmentId);
        Assert.Equal(new[] { "Created", "Created", "Created", "Updated" }, rig.Audit.Events.Select(e => e.Action).ToArray());

        var error = await Assert.ThrowsAsync<EdmException>(() =>
            rig.Users.UpdateAsync(person.Id, new DirectoryUserUpdate(null, "Other", null, null, null, person.RowVersion), default));
        Assert.Equal(EdmErrorCodes.StaleUpdate, error.Error);
    }

    [Fact]
    public async Task Listing_hides_inactive_people_unless_asked_and_can_filter_by_department()
    {
        var rig = new Rig();
        var finance = await rig.Departments.CreateAsync(new DepartmentCreate("Finance"), default);
        var hr = await rig.Departments.CreateAsync(new DepartmentCreate("HR"), default);
        await rig.Users.CreateAsync(Person("EMP1", finance.Id), default);
        var gone = await rig.Users.CreateAsync(Person("EMP2", finance.Id), default);
        await rig.Users.UpdateAsync(gone.Id, new DirectoryUserUpdate(null, null, null, null, IsActive: false, gone.RowVersion), default);
        await rig.Users.CreateAsync(Person("EMP3", hr.Id), default);

        Assert.Equal(2, (await rig.Users.ListAsync(null, includeInactive: false, default)).Count);
        Assert.Equal(3, (await rig.Users.ListAsync(null, includeInactive: true, default)).Count);
        Assert.Single(await rig.Users.ListAsync(hr.Id, includeInactive: false, default));
    }
}
