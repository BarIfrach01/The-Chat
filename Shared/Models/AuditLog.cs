namespace Programmin2_classroom.Shared.Models;

public class AuditLog
{
    public string Username { get; set; }
    public string Action { get; set; }
    public DateTime TimeAction { get; set; }
}