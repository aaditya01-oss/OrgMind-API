using Microsoft.EntityFrameworkCore;
using OrgMind.API.Data;

namespace OrgMind.API.Services;

public record ThreadAccess(string Owner, string? Department, bool IsClosed, bool IsOwner, bool IsStaff);

public static class Threads
{
  public const int MaxLength = 1000;

  // Who may take part in the conversation about one ticket or submission.
  // Returns null when it does not exist or this person may not see it (the same answer for both).
  public static async Task<ThreadAccess?> OpenAsync(OrgMindDbContext db, Caller me, string kind, int id)
  {
    string owner;
    string? department;
    bool closed;

    if (kind == "ticket")
    {
      var t = await db.Tickets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.OrgId == me.OrgId);
      if (t is null) return null;
      owner = t.CreatedBy;
      department = t.Department;
      closed = t.Status == "resolved";
    }
    else if (kind == "submission")
    {
      var s = await db.FormSubmissions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.OrgId == me.OrgId);
      if (s is null) return null;
      if (string.IsNullOrEmpty(s.SubmittedBy)) return null;
      owner = s.SubmittedBy;
      department = s.Department;
      closed = s.Status == "approved" || s.Status == "rejected" || s.Status == "done";
    }
    else
    {
      return null;
    }

    var isOwner = owner == me.Email;
    // Staff are an admin, or an employee of the handling department. Never the owner of the thing itself.
    var isStaff = !isOwner && (me.IsAdmin ||
        (me.IsDeptStaff && !string.IsNullOrEmpty(department) && department == me.Department));
    if (!isOwner && !isStaff) return null;

    return new ThreadAccess(owner, department, closed, isOwner, isStaff);
  }
}