namespace MovieRecApp.Server.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Route("api/[controller]")]
[ApiController]
public class ProtectedController : ControllerBase
{
    [Authorize]
    [HttpGet("secure-data")]
    public IActionResult GetSecureData()
    {
        return Ok(new { Message = "This is secure data" });
    }
}