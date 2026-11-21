using Microsoft.EntityFrameworkCore;
using OrgMind.API.Data;
using OrgMind.API.Models;

namespace OrgMind.API.Services;

public record HandoverRequest(string? ToDepartment, string? Reason);
public record HandoverOutcome(int Status, string? Error, string? From = null, string? To = null);

public static class HandoverRules
{
    public const int MinReason = 5;
    public const int MaxReason = 300;

    public static string? CheckReason(string? reason, out string clean)
    {
        clean = (reason ?? "").Trim();
        if (clean.Length < MinReason) return "Please say why you are handing this over (a few words).";
        if (clean.Length > MaxReason) return $"The reason is too long ({MaxReason} characters at most).";
        return null;
    }
}

public static class HandoverOps
{
    // Moves a ticket or form request to another department and records why.
    // Status: 200 done, 400 not allowed as asked, 403 your own item, 404 missing or not yours to hand over.
    public static async Task<HandoverOutcome> HandOverAsync(
        OrgMindDbContext db, Caller me, string kind, int id, string? toDepartment, string? reason)
    {
        if (!me.IsKnown) return new HandoverOutcome(401, "Please sign in.");
        var orgId = me.OrgId!;

        var problem = HandoverRules.CheckReason(reason, out var cleanReason);
        if (problem is not null) return new HandoverOutcome(400, problem);

        var target = (toDepartment ?? "").Trim().ToLowerInvariant();
        if (target.Length == 0) return new HandoverOutcome(400, "Choose a department.");

        Ticket? ticket = null;
        FormSubmission? submission = null;
        string? current;

        if (kind == "ticket")
        {
            ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == id && t.OrgId == orgId);
            if (ticket is null || !AccessRules.IsTicketHandler(me, ticket.Department))
                return new HandoverOutcome(404, "Not found.");
            if (ticket.CreatedBy == me.Email)
                return new HandoverOutcome(403, "You cannot hand over your own ticket. Someone else must handle it.");
            if (ticket.Status == "resolved")
                return new HandoverOutcome(400, "This ticket is already resolved.");
            current = ticket.Department;
        }
        else if (kind == "submission")
        {
            submission = await db.FormSubmissions.FirstOrDefaultAsync(s => s.Id == id && s.OrgId == orgId);
            if (submission is null || !AccessRules.IsSubmissionHandler(me, submission.Department))
                return new HandoverOutcome(404, "Not found.");
            if (submission.SubmittedBy == me.Email)
                return new HandoverOutcome(403, "You cannot hand over your own request. Someone else must handle it.");
            if (AccessRules.IsClosedSubmission(submission.Status))
                return new HandoverOutcome(400, "This request is already closed.");
            current = submission.Department;
        }
        else
        {
            return new HandoverOutcome(404, "Not found.");
        }

        if (!await DepartmentOps.ExistsAsync(db, orgId, target))
            return new HandoverOutcome(400, "That department does not exist.");
        if (target == (current ?? "").ToLowerInvariant())
            return new HandoverOutcome(400, "It is already with that department.");

        db.Handovers.Add(new Handover
        {
            OrgId = orgId,
            Kind = kind,
            RefId = id,
            FromDepartment = current,
            ToDepartment = target,
            Reason = cleanReason,
            HandedBy = me.Email!,
            CreatedAt = DateTime.UtcNow
        });

        if (ticket is not null) { ticket.Department = target; ticket.UpdatedAt = DateTime.UtcNow; }
        if (submission is not null) { submission.Department = target; submission.UpdatedAt = DateTime.UtcNow; }

        await db.SaveChangesAsync();
        return new HandoverOutcome(200, null, current, target);
    }
}