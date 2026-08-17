using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WFE.Client.Models;
using WFE.Client.Services;

namespace WFE.Client.Controllers
{
    public class HomeController : Controller
    {
        private readonly PacketActivityLog _log;
        private readonly IWfeApiClient _apiClient;
        private readonly RabbitMqOptions _rabbitMqOptions;
        private readonly WfeApiOptions _wfeOptions;

        public HomeController(PacketActivityLog log, IWfeApiClient apiClient, RabbitMqOptions rabbitMqOptions, WfeApiOptions wfeOptions)
        {
            _log = log;
            _apiClient = apiClient;
            _rabbitMqOptions = rabbitMqOptions;
            _wfeOptions = wfeOptions;
        }

        public IActionResult Index()
        {
            return View(new DashboardViewModel
            {
                RecentEntries = _log.GetRecent(50),
                RabbitMqEndpoint = $"{_rabbitMqOptions.HostName}:{_rabbitMqOptions.Port}{_rabbitMqOptions.VirtualHost}",
                RabbitMqQueueName = _rabbitMqOptions.QueueName,
                RabbitMqAutoConnect = _rabbitMqOptions.AutoConnect,
                WfeBaseUrl = _wfeOptions.BaseUrl,
                WfeSchemeId = _wfeOptions.WfeSchemeId
            });
        }

        /// <summary>Simulates a broker message without needing an actual broker running -
        /// the fastest way to evaluate the engine's behavior end-to-end.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendTestPacket(string tag, string value, CancellationToken cancellationToken)
        {
            var result = await _apiClient.IngestPacketAsync(tag, value, null, cancellationToken);
            _log.Add(new PacketLogEntry { Source = "Manual", Tag = tag, Value = value, Result = result });
            return RedirectToAction(nameof(Index));
        }
    }
}
