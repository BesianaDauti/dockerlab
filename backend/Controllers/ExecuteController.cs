using Microsoft.AspNetCore.Mvc;
using backend.Models;
using backend.Services;
using backend.Controllers;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExecuteController : ControllerBase
{
    private readonly DockerService _docker;
    private readonly HttpClient _http;

    public ExecuteController(DockerService docker, IHttpClientFactory factory)
    {
        _docker = docker;
        _http   = factory.CreateClient();
        _http.Timeout = TimeSpan.FromSeconds(90);
    }

    [HttpPost]
    public async Task<IActionResult> Execute(
        [FromHeader(Name="X-Token")] string token,
        [FromBody] ExecuteRequest req)
    {
        if (!AuthController.Sessions.TryGetValue(token, out var username))
            return Unauthorized(new { error = "Jo i kyçur" });

        try
        {
            var (_, port) = await _docker.GetOrCreateContainer(username);
            var url = $"http://localhost:{port}/execute";

            var response = await _http.PostAsJsonAsync(url, new {
                code     = req.Code,
                language = req.Language
            });

            var result = await response.Content.ReadAsStringAsync();
            return Content(result, "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}