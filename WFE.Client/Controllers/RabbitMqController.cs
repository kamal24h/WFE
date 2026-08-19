using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WFE.Client.Services;

namespace WFE.Client.Controllers
{
    [Route("api/rabbitmq")]
    public class RabbitMqController : Controller
    {
        private readonly RabbitMqSubscriberService _subscriber;

        public RabbitMqController(RabbitMqSubscriberService subscriber)
        {
            _subscriber = subscriber;
        }

        /// <summary>Starts consuming from the configured queue. Redirects back to the
        /// dashboard so this also works as a plain HTML form submit (no JS needed) -
        /// call it directly (e.g. via curl/Postman) if you just want the JSON-free 302.</summary>
        [HttpPost("start")]
        public async Task<IActionResult> Start()
        {
            await _subscriber.StartSubscribingAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpPost("stop")]
        public async Task<IActionResult> Stop()
        {
            await _subscriber.StopSubscribingAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet("status")]
        public IActionResult Status() => Json(new { isRunning = _subscriber.IsRunning });
    }
}
