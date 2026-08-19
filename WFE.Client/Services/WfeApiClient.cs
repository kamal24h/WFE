using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace WFE.Client.Services
{
    /// <summary>Mirrors WFE.Core.Runtime.WorkflowExecutionResult's JSON shape - not a shared
    /// project reference on purpose (this client talks to the engine purely over HTTP, the way
    /// a real external system would; it doesn't get to peek at the engine's internal types).</summary>
    public class WorkflowExecutionResultDto
    {
        [JsonPropertyName("instanceId")] public long InstanceId { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; }
        [JsonPropertyName("activity")] public string Activity { get; set; }
        [JsonPropertyName("state")] public string State { get; set; }
        [JsonPropertyName("faultReason")] public string FaultReason { get; set; }
        [JsonPropertyName("message")] public string Message { get; set; }
        [JsonPropertyName("availableCommandNames")] public List<string> AvailableCommandNames { get; set; }
    }

    /// <summary>Mirrors WFE.Models.WfeProcessInstance's JSON shape, trimmed to what the client
    /// actually needs - just enough to find and identify Waiting instances.</summary>
    public class InstanceSummaryDto
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("activity")] public string Activity { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; }
    }

    /// <summary>Mirrors WFE.Models.WfeCommand's JSON shape. Title IS the command name to pass
    /// to ExecuteCommandAsync (the engine's CommandService sets Title to the same string
    /// FindCommandTransition matches against) - there's no separate "Name" field.</summary>
    public class CommandDto
    {
        [JsonPropertyName("title")] public string Title { get; set; }
    }

    public class WfeIngestResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public WorkflowExecutionResultDto Engine { get; set; }
        public long ElapsedMs { get; set; }
    }

    public interface IWfeApiClient
    {
        Task<WfeIngestResult> IngestPacketAsync(
            string tag, string value, IDictionary<string, string> metadata, CancellationToken cancellationToken = default);

        Task<List<InstanceSummaryDto>> GetWaitingInstancesAsync(int take, CancellationToken cancellationToken = default);

        Task<List<CommandDto>> GetAvailableCommandsAsync(
            long instanceId, string actorId, CancellationToken cancellationToken = default);

        Task<WfeIngestResult> ExecuteCommandAsync(
            long instanceId, string commandName, string actorId, CancellationToken cancellationToken = default);
    }

    public class WfeApiClient : IWfeApiClient
    {
        private readonly HttpClient _http;
        private readonly WfeApiOptions _options;

        public WfeApiClient(HttpClient http, WfeApiOptions options)
        {
            _http = http;
            _options = options;
        }

        public async Task<WfeIngestResult> IngestPacketAsync(
            string tag, string value, IDictionary<string, string> metadata, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var payload = new
            {
                WfeSchemeId = _options.WfeSchemeId,
                ActorId = _options.ActorId,
                Tag = tag,
                Value = value,
                Metadata = metadata
            };

            return await PostForResultAsync("api/ingestion/packets", payload, stopwatch, cancellationToken);
        }

        public async Task<List<InstanceSummaryDto>> GetWaitingInstancesAsync(int take, CancellationToken cancellationToken = default)
        {
            var response = await _http.GetFromJsonAsync<List<InstanceSummaryDto>>(
                $"api/instances?status=Waiting&take={take}", cancellationToken);
            return response ?? new List<InstanceSummaryDto>();
        }

        public async Task<List<CommandDto>> GetAvailableCommandsAsync(
            long instanceId, string actorId, CancellationToken cancellationToken = default)
        {
            var response = await _http.GetFromJsonAsync<List<CommandDto>>(
                $"api/instances/{instanceId}/commands?actorId={Uri.EscapeDataString(actorId)}", cancellationToken);
            return response ?? new List<CommandDto>();
        }

        public async Task<WfeIngestResult> ExecuteCommandAsync(
            long instanceId, string commandName, string actorId, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var payload = new { ActorId = actorId };
            return await PostForResultAsync(
                $"api/instances/{instanceId}/commands/{Uri.EscapeDataString(commandName)}", payload, stopwatch, cancellationToken);
        }

        private async Task<WfeIngestResult> PostForResultAsync(
            string url, object payload, Stopwatch stopwatch, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _http.PostAsJsonAsync(url, payload, cancellationToken);
                stopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    return new WfeIngestResult
                    {
                        Success = false,
                        ErrorMessage = $"HTTP {(int)response.StatusCode}: {body}",
                        ElapsedMs = stopwatch.ElapsedMilliseconds
                    };
                }

                var engineResult = await response.Content.ReadFromJsonAsync<WorkflowExecutionResultDto>(
                    cancellationToken: cancellationToken);
                return new WfeIngestResult
                {
                    Success = true,
                    Engine = engineResult,
                    ElapsedMs = stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                return new WfeIngestResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    ElapsedMs = stopwatch.ElapsedMilliseconds
                };
            }
        }
    }
}

