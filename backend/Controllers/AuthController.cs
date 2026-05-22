using Microsoft.AspNetCore.Mvc;
using backend.Models;
using System.Collections.Concurrent;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private static readonly Dictionary<string,string> _users = new()
    {
        {"user1","pass1"}, {"user2","pass2"}, {"user3","pass3"},
        {"user4","pass4"}, {"user5","pass5"}
    };

    public static readonly ConcurrentDictionary<string,string> Sessions = new();

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest req)
    {
        if (_users.TryGetValue(req.Username, out var pwd) && pwd == req.Password)
        {
            var token = Guid.NewGuid().ToString();
            Sessions[token] = req.Username;
            return Ok(new { token, username = req.Username });
        }
        return Unauthorized(new { error = "Kredenciale të gabuara" });
    }

    [HttpPost("logout")]
    public IActionResult Logout([FromHeader(Name="X-Token")] string token,
                                [FromServices] backend.Services.DockerService docker)
    {
        if (Sessions.TryRemove(token, out var username))
        {
            docker.RemoveContainer(username);
            return Ok(new { message = "Logout i suksesshëm" });
        }
        return BadRequest();
    }
}