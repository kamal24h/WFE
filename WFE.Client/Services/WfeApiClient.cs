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

            try
            {
                var response = await _http.PostAsJsonAsync("api/ingestion/packets", payload, cancellationToken);
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
