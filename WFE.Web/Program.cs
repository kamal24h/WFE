using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WFE.Core.Actions;
using WFE.Core.Conditions;
using WFE.Core.Rules;
using WFE.Core.Runtime;
using WFE.Core.Schema;
using WFE.Persistence;
using WFE.Runtime;
using WFE.Runtime.BuiltInActions;
using WFE.Runtime.BuiltInConditions;
using WFE.Runtime.Scheduling;
using WFE.Web.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// --- Persistence ---
builder.Services.AddDbContext<WfeDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IWorkflowParameterStore, EfWorkflowParameterStore>();
builder.Services.AddScoped<IProcessInstanceStore, EfProcessInstanceStore>();
builder.Services.AddScoped<IProcessSchemeProvider, EfProcessSchemeProvider>();

// Single shared instance per scope for both interfaces this class implements.
builder.Services.AddScoped<EfProcessWorkItemStore>();
builder.Services.AddScoped<IProcessWorkItemStore>(sp => sp.GetRequiredService<EfProcessWorkItemStore>());
builder.Services.AddScoped<ISubprocessTracker>(sp => sp.GetRequiredService<EfProcessWorkItemStore>());

// --- Schema parsing (stateless, thread-safe, app-lifetime cache) ---
builder.Services.AddSingleton<ProcessSchemaLoader>();
builder.Services.AddSingleton<ExpressionConditionEvaluator>();
builder.Services.AddSingleton<IRuleEvaluator, RuleEvaluator>();

// --- Action execution policy (fail-fast by default, per-action retry overrides from config) ---
var policyOptions = new ActionExecutionPolicyOptions();
builder.Configuration.GetSection("ActionExecutionPolicy").Bind(policyOptions);
builder.Services.AddSingleton(policyOptions);

// --- Runtime options (MaxAutoHops - see WorkflowRuntimeOptions for why this isn't hardcoded) ---
var runtimeOptions = new WorkflowRuntimeOptions();
builder.Configuration.GetSection("WorkflowRuntime").Bind(runtimeOptions);
builder.Services.AddSingleton(runtimeOptions);

// --- Dynamic CodeActions (Phase 4) - disabled by default, see CodeActionOptions ---
var codeActionOptions = new CodeActionOptions();
builder.Configuration.GetSection("CodeActions").Bind(codeActionOptions);
builder.Services.AddSingleton(codeActionOptions);
builder.Services.AddSingleton<CodeActionCompiler>();

// --- Subprocess worker (Phase 3 - real async "AnotherThread" spawns) ---
var subprocessWorkerOptions = new SubprocessWorkerOptions();
builder.Configuration.GetSection("SubprocessWorker").Bind(subprocessWorkerOptions);
builder.Services.AddSingleton(subprocessWorkerOptions);
builder.Services.AddHostedService<SubprocessWorker>();

// --- Schedule trigger worker ---
var scheduleWorkerOptions = new ScheduleWorkerOptions();
builder.Configuration.GetSection("ScheduleWorker").Bind(scheduleWorkerOptions);
builder.Services.AddSingleton(scheduleWorkerOptions);
builder.Services.AddHostedService<ScheduleWorker>();

// --- Message broker: REPLACE LoggingMessageBroker before going to production ---
builder.Services.AddSingleton<IMessageBroker, LoggingMessageBroker>();

// --- File actions: confines FileWrite/FileRead/FileDelete to a sandboxed root ---
var fileActionOptions = new FileActionOptions();
builder.Configuration.GetSection("FileActions").Bind(fileActionOptions);
builder.Services.AddSingleton(fileActionOptions);

// --- HTTPRequest action: named client so it goes through IHttpClientFactory, not a raw
// HttpClient per call (avoids socket exhaustion under load) ---
builder.Services.AddHttpClient("Wfe");

// --- Built-in actions/conditions ---
// Register every IActionExecutor/IConditionExecutor implementation here.
builder.Services.AddScoped<IActionExecutor, PublishMessageAction>();
builder.Services.AddScoped<IActionExecutor, SetParameterAction>();
builder.Services.AddScoped<IActionExecutor, RemoveParameterAction>();
builder.Services.AddScoped<IActionExecutor, AddNumberToParameterAction>();
builder.Services.AddScoped<IActionExecutor, FileWriteAction>();
builder.Services.AddScoped<IActionExecutor, FileReadAction>();
builder.Services.AddScoped<IActionExecutor, FileDeleteAction>();
builder.Services.AddScoped<IActionExecutor, HttpRequestAction>();
builder.Services.AddScoped<IActionExecutor, StartLoopForAction>();
builder.Services.AddScoped<IActionExecutor, StartLoopForeachAction>();

builder.Services.AddScoped<IConditionExecutor, EvaluateRuleCondition>();
builder.Services.AddScoped<IConditionExecutor, CheckParameterCondition>();
builder.Services.AddScoped<IConditionExecutor, CheckHttpRequestCondition>();
builder.Services.AddScoped<IConditionExecutor, LoopIsNotCompletedAndBrokenCondition>();
builder.Services.AddScoped<IConditionExecutor, CheckAllSubprocessesCompletedCondition>();

builder.Services.AddScoped<ActionExecutorRegistry>();
builder.Services.AddScoped<ConditionExecutorRegistry>();

// --- Engine ---
builder.Services.AddScoped<TransitionEngine>();
builder.Services.AddScoped<CommandService>();
builder.Services.AddScoped<IWorkflowRuntime, WorkflowRuntime>();

// --- Web ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "WFE Workflow Engine API",
        Version = "v1"
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(); // served at /swagger
    // https://localhost:51113/swagger
}

app.MapControllers();

app.Run();
