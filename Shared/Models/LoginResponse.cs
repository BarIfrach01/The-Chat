namespace Programmin2_classroom.Shared.Models;

public class LoginResponse
{
    public string Username { get; set; }
    public bool IsAdmin { get; set; }
    public string Token { get; set; }
}