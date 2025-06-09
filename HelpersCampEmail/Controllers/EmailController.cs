using HelpersCampEmail.Models;
using HelpersCampEmail.Service;
using Microsoft.AspNetCore.Mvc;

namespace HelpersCampEmail.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmailController : ControllerBase
    {
        private readonly IEmailService _emails;

        public EmailController(IEmailService emails)
        {
            _emails = emails;
        }

        [HttpPost("send")]
        public async Task<ActionResult<List<EmailResult>>> SendToAll()
        {
            var report = await _emails.SendAllAsync();
            return Ok(report);
        }

        [HttpPost("send-single")]
        public async Task<ActionResult<EmailResult>> SendSingle([FromBody] SingleEmailRequest req)
        {
            var result = await _emails.SendSingleAsync(req.Code, req.Email, req.FullName, req.Accepted);
            return Ok(result);
        }
    }

    public class SingleEmailRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool Accepted { get; set; }
    }
}
