using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using WFE.Core.Runtime;
using WFE.Core.Schema;
using WFE.Models;

namespace WFE.Runtime.CodeActions
{
    /// <summary>
    /// Compiles a &lt;CodeAction&gt; definition into a cached, reusable delegate. Compilation
    /// is expensive (Roslyn parse + emit); the cache key is a content hash of the code itself
    /// (name+type+async+usings+body), so editing a CodeAction's body in the designer and
    /// re-publishing automatically invalidates the old compiled version - no manual cache
    /// busting needed, and two schemes with an identical CodeAction body share one compilation.
    /// </summary>
    public class CodeActionCompiler
    {
        private readonly CodeActionExecutionOptions _options;
        private readonly ConcurrentDictionary<string, Lazy<CompiledCodeAction>> _cache = new();

        public CodeActionCompiler(CodeActionExecutionOptions options)
        {
            _options = options;
        }

        public CompiledCodeAction GetOrCompile(CodeActionDefinitionXml definition)
        {
            if (!_options.Enabled)
                throw new InvalidOperationException(
                    $"Dynamic CodeActions are disabled - set CodeActions:Enabled=true in appsettings.json to " +
                    $"allow CodeAction '{definition.Name}' to run. CodeActions execute arbitrary C# with full " +
                    "application privileges; only enable this if you trust everyone who can author or import a " +
                    "workflow schema.");

            var key = ComputeCacheKey(definition);
            return _cache.GetOrAdd(key, _ => new Lazy<CompiledCodeAction>(
                () => Compile(definition), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        }

        private static string ComputeCacheKey(CodeActionDefinitionXml def)
        {
            var raw = $"{def.Name}|{def.Type}|{def.IsAsync}|{def.Usings}|{def.ActionCode}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash);
        }

        private static CompiledCodeAction Compile(CodeActionDefinitionXml def)
        {
            var isCondition = string.Equals(def.Type, "Condition", StringComparison.OrdinalIgnoreCase);
            var className = $"Generated_{SanitizeIdentifier(def.Name)}_{Guid.NewGuid():N}";

            var usings = (def.Usings ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(u => $"using {u};");

            string methodSignature;
            string trailingReturn = "";
            if (isCondition)
            {
                methodSignature = def.IsAsyncValue
                    ? "public async System.Threading.Tasks.Task<bool> Run()"
                    : "public System.Threading.Tasks.Task<bool> Run()";
                // Condition CodeActions are expected to end with their own "return <bool>;" -
                // documented in README, not enforced here (a missing return is a normal C#
                // compile error the caller will see clearly).
            }
            else
            {
                methodSignature = def.IsAsyncValue
                    ? "public async System.Threading.Tasks.Task Run()"
                    : "public System.Threading.Tasks.Task Run()";
                if (!def.IsAsyncValue)
                    trailingReturn = "\nreturn System.Threading.Tasks.Task.CompletedTask;";
            }

            var source = $@"
{string.Join("\n", usings)}
namespace WFE.Runtime.CodeActions.Generated
{{
    public class {className} : WFE.Runtime.CodeActions.CodeActionBase
    {{
        public {className}(WFE.Models.WfeProcessInstance processInstance, string parameter,
            WFE.Core.Runtime.IWorkflowParameterStore store, System.Threading.CancellationToken cancellationToken)
            : base(processInstance, parameter, store, cancellationToken) {{ }}

        {methodSignature}
        {{
{def.ActionCode}
{trailingReturn}
        }}
    }}
}}";

            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            // Reference every already-loaded assembly (matches the pattern in the WfeRuntime.cs
            // you shared) - this covers the BCL, EF Core, Newtonsoft.Json, and every WFE.*
            // project automatically. It does NOT cover vendor-specific usings your designer
            // might export (e.g. OptimaJet.Workflow.*, WF.Sample.*) since those assemblies were
            // never part of this solution and so are never loaded - a CodeAction using those
            // will fail to compile with a clear "type or namespace not found" error rather than
            // silently doing something unexpected.
            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
                .ToList();

            var compilation = CSharpCompilation.Create(
                $"WfeCodeAction_{className}",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);

            if (!result.Success)
            {
                var errors = string.Join("\n", result.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString()));
                throw new InvalidOperationException(
                    $"CodeAction '{def.Name}' failed to compile:\n{errors}\n\n--- Generated source ---\n{source}");
            }

            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());
            var type = assembly.GetType($"WFE.Runtime.CodeActions.Generated.{className}")
                ?? throw new InvalidOperationException($"CodeAction '{def.Name}' compiled but its generated type could not be loaded.");
            var runMethod = type.GetMethod("Run")
                ?? throw new InvalidOperationException($"CodeAction '{def.Name}' compiled but its Run() method could not be found.");

            if (isCondition)
            {
                Func<WfeProcessInstance, string, IWorkflowParameterStore, CancellationToken, Task<bool>> invoker =
                    async (instance, param, store, ct) =>
                    {
                        var obj = Activator.CreateInstance(type, instance, param, store, ct);
                        return await (Task<bool>)runMethod.Invoke(obj, null);
                    };
                return new CompiledCodeAction { IsCondition = true, ConditionInvoker = invoker };
            }
            else
            {
                Func<WfeProcessInstance, string, IWorkflowParameterStore, CancellationToken, Task> invoker =
                    async (instance, param, store, ct) =>
                    {
                        var obj = Activator.CreateInstance(type, instance, param, store, ct);
                        await (Task)runMethod.Invoke(obj, null);
                    };
                return new CompiledCodeAction { IsCondition = false, ActionInvoker = invoker };
            }
        }

        private static string SanitizeIdentifier(string name)
        {
            var chars = (name ?? "CodeAction").Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray();
            var sanitized = new string(chars);
            return sanitized.Length == 0 || char.IsDigit(sanitized[0]) ? "_" + sanitized : sanitized;
        }
    }
}
