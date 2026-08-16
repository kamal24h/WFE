using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WFE.Core.Actions;
using WFE.Core.Runtime;

namespace WFE.Runtime.BuiltInActions
{
    public class HttpRequestArgs
    {
        [JsonPropertyName("ParameterName")] public string ParameterName { get; set; }

        /// <summary>If true, current instance parameters are sent along with the request -
        /// as a JSON body for POST, or appended as a query string for GET.</summary>
        [JsonPropertyName("AddProcessInstanceParameters")] public bool AddProcessInstanceParameters { get; set; }

        [JsonPropertyName("ContentType")] public string ContentType { get; set; } = "Json";
        [JsonPropertyName("Url")] public string Url { get; set; }
        [JsonPropertyName("StoreResponse")] public bool StoreResponse { get; set; }
        [JsonPropertyName("Post")] public bool Post { get; set; }

        // "Parameters" also appears in your sample (as the literal string "true") but its
        // meaning is ambiguous - not used here, see class doc comment.
    }

    /// <summary>
    /// &lt;ActionRef NameRef="HTTPRequest"&gt; - matches HTTPRequest.xml. Deliberately ignores
    /// the sample's "Parameters":"true" field: AddProcessInstanceParameters is treated as the
    /// authoritative toggle for whether instance parameters go along with the request. Flag me
    /// if "Parameters" was meant to carry a literal request body/template instead.
    /// </summary>
    public class HttpRequestAction : IActionExecutor
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public HttpRequestAction(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public string Name => "HTTPRequest";

        public async Task ExecuteAsync(WorkflowExecutionContext context, string rawJsonParameters)
        {
            var args = JsonSerializer.Deserialize<HttpRequestArgs>(rawJsonParameters,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var client = _httpClientFactory.CreateClient("Wfe");
            var mediaType = string.Equals(args.ContentType, "Json", System.StringComparison.OrdinalIgnoreCase)
                ? "application/json"
                : "text/plain";

            HttpResponseMessage response;
            if (args.Post)
            {
                string body = null;
                if (args.AddProcessInstanceParameters)
                {
                    var allParams = await context.Parameters.GetAllAsync(context.Instance.Id);
                    body = JsonSerializer.Serialize(allParams);
                }
                var content = new StringContent(body ?? string.Empty, Encoding.UTF8, mediaType);
                response = await client.PostAsync(args.Url, content, context.CancellationToken);
            }
            else
            {
                var url = args.Url;
                if (args.AddProcessInstanceParameters)
                {
                    var allParams = await context.Parameters.GetAllAsync(context.Instance.Id);
                    var query = string.Join("&", allParams.Select(kv =>
                        $"{System.Uri.EscapeDataString(kv.Key)}={System.Uri.EscapeDataString(kv.Value ?? string.Empty)}"));
                    if (query.Length > 0)
                        url += (url.Contains('?') ? "&" : "?") + query;
                }
                response = await client.GetAsync(url, context.CancellationToken);
            }

            // Non-2xx throws HttpRequestException, which flows into WorkflowRuntime's normal
            // action failure/retry handling - the HTTPRequest-specific retry override in
            // appsettings.json (ActionExecutionPolicy:Overrides:HTTPRequest) applies here.
            response.EnsureSuccessStatusCode();

            if (args.StoreResponse)
            {
                var text = await response.Content.ReadAsStringAsync(context.CancellationToken);
                await context.Parameters.SetAsync(context.Instance.Id, args.ParameterName, text);
            }
        }
    }
}
