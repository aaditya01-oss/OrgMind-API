using OrgMind.API.Services;
using Xunit;

namespace OrgMind.Tests;

public class AccessRulesTests
{
    static Caller Admin() => new("org1", "admin@acme.test", "admin", null);
    static Caller Employee(string email, string? dept) => new("org1", email, "member", dept);
    static Caller Customer(string email) => new("org1", email, "customer", null);

    // ---------- Tickets ----------

    [Fact]
    public void Employee_cannot_manage_a_ticket_they_raised_even_in_their_own_department()
    {
        var me = Employee("sita@acme.test", "support");
        Assert.False(AccessRules.CanManageTicket(me, "sita@acme.test", "support"));
    }

    [Fact]
    public void Employee_can_manage_a_colleagues_or_customers_ticket_in_their_department()
    {
        var me = Employee("sita@acme.test", "support");
        Assert.True(AccessRules.CanManageTicket(me, "maya@customer.test", "support"));
    }

    [Fact]
    public void Employee_cannot_manage_another_departments_ticket()
    {
        var me = Employee("sita@acme.test", "support");
        Assert.False(AccessRules.CanManageTicket(me, "maya@customer.test", "loans"));
    }

    [Fact]
    public void Employee_without_a_department_cannot_manage_any_ticket()
    {
        var me = Employee("ram@acme.test", null);
        Assert.False(AccessRules.CanManageTicket(me, "maya@customer.test", "support"));
        Assert.False(AccessRules.CanManageTicket(me, "maya@customer.test", null));
    }

    [Fact]
    public void Customer_cannot_manage_any_ticket()
    {
        var me = Customer("maya@customer.test");
        Assert.False(AccessRules.CanManageTicket(me, "maya@customer.test", "support"));
        Assert.False(AccessRules.CanManageTicket(me, "other@customer.test", "support"));
    }

    [Fact]
    public void Ticket_without_a_department_can_only_be_managed_by_an_admin()
    {
        Assert.True(AccessRules.CanManageTicket(Admin(), "maya@customer.test", null));
        Assert.False(AccessRules.CanManageTicket(Employee("sita@acme.test", "support"), "maya@customer.test", null));
    }

    [Fact]
    public void Admin_can_manage_others_tickets_but_not_their_own()
    {
        Assert.True(AccessRules.CanManageTicket(Admin(), "maya@customer.test", "support"));
        Assert.False(AccessRules.CanManageTicket(Admin(), "admin@acme.test", "support"));
    }

    // ---------- Form submissions ----------

    [Fact]
    public void Customer_can_see_their_own_submission_but_not_someone_elses()
    {
        var me = Customer("maya@customer.test");
        Assert.True(AccessRules.CanSeeSubmission(me, "maya@customer.test", "loans"));
        Assert.False(AccessRules.CanSeeSubmission(me, "other@customer.test", "loans"));
    }

    [Fact]
    public void Employee_sees_only_their_own_departments_submissions()
    {
        var me = Employee("sita@acme.test", "loans");
        Assert.True(AccessRules.CanSeeSubmission(me, "maya@customer.test", "loans"));
        Assert.False(AccessRules.CanSeeSubmission(me, "maya@customer.test", "support"));
        Assert.False(AccessRules.CanSeeSubmission(me, "maya@customer.test", null));
    }

    [Fact]
    public void Admin_sees_every_submission()
    {
        Assert.True(AccessRules.CanSeeSubmission(Admin(), "maya@customer.test", "loans"));
        Assert.True(AccessRules.CanSeeSubmission(Admin(), "maya@customer.test", null));
    }

    [Fact]
    public void Nobody_can_manage_their_own_submission_not_even_an_admin()
    {
        Assert.False(AccessRules.CanManageSubmission(Employee("sita@acme.test", "loans"), "sita@acme.test", "loans", "new"));
        Assert.False(AccessRules.CanManageSubmission(Admin(), "admin@acme.test", "loans", "new"));
    }

    [Theory]
    [InlineData("approved")]
    [InlineData("rejected")]
    [InlineData("done")]
    public void A_closed_submission_can_only_be_changed_by_an_admin(string status)
    {
        var employee = Employee("sita@acme.test", "loans");
        Assert.False(AccessRules.CanManageSubmission(employee, "maya@customer.test", "loans", status));
        Assert.True(AccessRules.CanManageSubmission(Admin(), "maya@customer.test", "loans", status));
    }

    [Theory]
    [InlineData("new")]
    [InlineData("in_progress")]
    public void Employee_can_manage_an_open_submission_in_their_department(string status)
    {
        var me = Employee("sita@acme.test", "loans");
        Assert.True(AccessRules.CanManageSubmission(me, "maya@customer.test", "loans", status));
    }

    [Fact]
    public void Protected_answers_can_only_be_revealed_by_admins_and_the_handling_department()
    {
        Assert.True(AccessRules.CanRevealAnswer(Admin(), "loans"));
        Assert.True(AccessRules.CanRevealAnswer(Employee("sita@acme.test", "loans"), "loans"));
        Assert.False(AccessRules.CanRevealAnswer(Employee("ram@acme.test", "support"), "loans"));
        Assert.False(AccessRules.CanRevealAnswer(Employee("ram@acme.test", null), "loans"));
        Assert.False(AccessRules.CanRevealAnswer(Customer("maya@customer.test"), "loans"));
    }
}