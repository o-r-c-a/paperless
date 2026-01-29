using Microsoft.AspNetCore.Mvc;

namespace Paperless.Rest.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get() => Ok(new { status = "OK" });
    }
}
