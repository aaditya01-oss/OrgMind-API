namespace OrgMind.API.Services;

// Who may do what with tickets and form submissions. The endpoints call these
// functions, and the automated tests call the same ones, so there is one copy of each rule.
public static class AccessRules
{
    // ---- Tickets ----

    // Admins, and employees of the ticket's department.
    public static bool IsTicketHandler(Caller me, string? ticketDepartment) =>
        me.IsAdmin || (me.IsDeptStaff && ticketDepartment == me.Department);

    // A handler, but never for a ticket they raised themselves.
    public static bool CanManageTicket(Caller me, string? createdBy, string? ticketDepartment) =>
        createdBy != me.Email && IsTicketHandler(me, ticketDepartment);

    // ---- Form submissions ----

    public static bool IsClosedSubmission(string? status) =>
        status is "approved" or "rejected" or "done";

    // Admins, and employees of the handling department.
    public static bool IsSubmissionHandler(Caller me, string? department) =>
        me.IsAdmin || (me.IsDeptStaff && department == me.Department);

    // The person who sent it, or a handler.
    public static bool CanSeeSubmission(Caller me, string? submittedBy, string? department) =>
        submittedBy == me.Email || IsSubmissionHandler(me, department);

    // A handler, not for their own request, and a closed request only for an admin.
    public static bool CanManageSubmission(Caller me, string? submittedBy, string? department, string? status) =>
        submittedBy != me.Email &&
        (me.IsAdmin || (IsSubmissionHandler(me, department) && !IsClosedSubmission(status)));

    // Showing a protected answer: handlers only.
    public static bool CanRevealAnswer(Caller me, string? department) =>
        IsSubmissionHandler(me, department);
}