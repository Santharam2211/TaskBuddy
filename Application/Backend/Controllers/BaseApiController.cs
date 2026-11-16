using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace TimeBuddy.Backend.Controllers
{
    [ApiController]
    public abstract class BaseApiController : ControllerBase
    {
        protected int CurrentUserId
        {
            get
            {
                var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(claim, out int id))
                {
                    return id;
                }
                return 0;
            }
        }
    }
}
