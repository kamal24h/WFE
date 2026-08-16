using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using WFE.Core.Actions;
using WFE.Core.Conditions;
using WFE.Core.Runtime;
using WFE.Core.Schema;

namespace WFE.Runtime
{
    /// <summary>
    /// Compiles &lt;CodeAction&gt; definitions into runnable delegates, cached per
    /// (schema, CodeAction name) for the app's lifetime - a schema is treated as immutable
    /// once published (same assumption EfProcessSchemeProvider already relies on), so a given
    /// CodeAction only ever compiles once.
    ///
    /// IMPORTANT - this is a NEW contract, not compatibility with the third-party runtime your
    /// sample XML's CodeAction body was written against (it references
    /// OptimaJet.Workflow.Core.Runtime, WF.Sample.Business.Workflow, etc. - assemblies that
    /// don't exist in this solution and never will). Pasting that exact sample body in will
    /// fail to compile with a clear "namespace not found" error - that's expected, not a bug.
    /// A CodeAction body written for THIS engine gets two variables in scope:
    ///   - context   : WorkflowExecutionContext (context.Instance, context.Parameters, ...)
    ///   - parameters: IReadOnlyDictionary&lt;string,string&gt; - this CodeAction's own declared
    ///                 &lt;Parameter&gt; values, already bound/defaulted/validated
    /// and must end in either `return;` (Action) or `return someBool;` (Condition).
    /// </summary>
    public class CodeActionCompiler
    {
        private static readonly Lazy<List<MetadataReference>> BaseReferences = new(BuildBaseReferences);

        private readonly ConcurrentDictionary<string, IActionExecutor> _actionCache = new();
        private readonly ConcurrentDictionary<string, IConditionExecutor> _conditionCache = new();

        public IActionExecutor GetActionExecutor(ResolvedProcessSchema schema, CodeActionDefinitionXml def)
        {
            if (!string.Equals(def.Type, "Action", StringComparison.Ordinal))
                throw new NotSupportedException(
                    $"CodeAction '{def.Name}' has Type '{def.Type}' - only 'Action' is supported via ActionRef.");

            var key = $"{schema.CacheKey}:action:{def.Name}";
            return _actionCache.GetOrAdd(key, _ =>
            {
                var compiled = Compile<Func<WorkflowExecutionContext, IReadOnlyDictionary<string, string>, Task>>(
                    def, isCondition: false);
                return new CompiledCodeAction(def, compiled);
            });
        }

        public IConditionExecutor GetConditionExecutor(ResolvedProcessSchema schema, CodeActionDefinitionXml def)
        {
            // "Condition" is MY assumption for the Type value a code-backed Condition would
            // use - no sample shows one (only Type="Action" appears). Adjust if your designer
            // emits something else.
            if (!string.Equals(def.Type, "Condition", StringComparison.Ordinal))
                throw new NotSupportedException(
                    $"CodeAction '{def.Name}' has Type '{def.Type}' - only 'Condition' is supported via Condition NameRef.");

            var key = $"{schema.CacheKey}:condition:{def.Name}";
            return _conditionCache.GetOrAdd(key, _ =>
            {
                var compiled = Compile<Func<WorkflowExecutionContext, IReadOnlyDictionary<string, string>, Task<bool>>>(
                    def, isCondition: true);
                return new CompiledCodeCondition(def, compiled);
            });
        }

        private static TDelegate Compile<TDelegate>(CodeActionDefinitionXml def, bool isCondition) where TDelegate : Delegate
        {
            var typeName = "CodeAction_" + SanitizeIdentifier(def.Name) + "_" + Guid.NewGuid().ToString("N");
            var source = BuildSource(def, typeName, isCondition);

            var syntaxTree = CSharpSyntaxTree.ParseText(source);
            var compilation = CSharpCompilation.Create(
                "WfeCodeAction_" + Guid.NewGuid().ToString("N"),
                new[] { syntaxTree },
                BaseReferences.Value,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);

            // This is the fix for the exact bug found reviewing the legacy WfeRuntime.cs: that
            // code threw BEFORE reading result.Diagnostics, so a compile failure gave zero
            // information about why. Here the diagnostics are gathered and included in the
            // exception message itself.
            if (!result.Success)
            {
                var errors = result.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString());
                throw new InvalidOperationException(
                    $"CodeAction '{def.Name}' failed to compile:\n" + string.Join("\n", errors));
            }

            ms.Seek(0, SeekOrigin.Begin);

            // Collectible so the compiled assembly CAN be unloaded if you ever add cache
            // invalidation later - not exercised today since schemas are treated as immutable.
            var loadContext = new AssemblyLoadContext("WfeCodeAction_" + def.Name, isCollectible: true);
            var assembly = loadContext.LoadFromStream(ms);

            var type = assembly.GetType("WFE.Runtime.CompiledCodeActions." + typeName)
                ?? throw new InvalidOperationException($"CodeAction '{def.Name}' compiled but the generated type could not be found.");
            var method = type.GetMethod("ExecuteAsync", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException($"CodeAction '{def.Name}' compiled but ExecuteAsync could not be found.");

            return (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), method);
        }

        private static string BuildSource(CodeActionDefinitionXml def, string typeName, bool isCondition)
        {
            var usings = new List<string>
            {
                "System", "System.Collections.Generic", "System.Linq", "System.Threading.Tasks",
                "System.Text.Json", "WFE.Core.Runtime"
            };
            if (!string.IsNullOrWhiteSpace(def.Usings))
                usings.AddRange(def.Usings.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            var usingsBlock = string.Join("\n", usings.Distinct().Select(u => $"using {u};"));
            var returnType = isCondition ? "Task<bool>" : "Task";

            return $@"
{usingsBlock}

namespace WFE.Runtime.CompiledCodeActions
{{
    public static class {typeName}
    {{
        public static async {returnType} ExecuteAsync(WorkflowExecutionContext context, IReadOnlyDictionary<string, string> parameters)
        {{
            {def.ActionCode}
        }}
    }}
}}";
        }

        private static string SanitizeIdentifier(string name) =>
            new string((name ?? "Anon").Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

        private static List<MetadataReference> BuildBaseReferences()
        {
            // Computed ONCE for the app's lifetime - the legacy code rebuilt this on every
            // single compilation, which is the "no caching" perf bug flagged during the
            // WfeRuntime.cs review.
            var refs = new List<MetadataReference>();

            var trustedPlatformAssemblies = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string)
                ?.Split(Path.PathSeparator) ?? Array.Empty<string>();
            foreach (var path in trustedPlatformAssemblies)
                refs.Add(MetadataReference.CreateFromFile(path));

            // Our own assemblies (for WorkflowExecutionContext etc.) aren't part of the
            // trusted-platform-assemblies list - pull them from what's already loaded.
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()
                         .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location)))
            {
                refs.Add(MetadataReference.CreateFromFile(asm.Location));
            }

            return refs;
        }

        /// <summary>Binds the CodeAction's own declared &lt;Parameter&gt; list against the
        /// incoming ActionParameter/Condition ActionParameter JSON - values from the JSON win,
        /// falling back to DefaultValue, then failing loudly if isRequired and still missing.</summary>
        internal static IReadOnlyDictionary<string, string> BindParameters(CodeActionDefinitionXml def, string rawJson)
        {
            var incoming = string.IsNullOrWhiteSpace(rawJson)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(rawJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var bound = new Dictionary<string, string>();
            foreach (var p in def.Parameters)
            {
                if (incoming.TryGetValue(p.Name, out var value))
                {
                    bound[p.Name] = value;
                }
                else if (p.DefaultValue != null)
                {
                    bound[p.Name] = p.DefaultValue;
                }
                else if (string.Equals(p.IsRequired, "true", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"CodeAction '{def.Name}' is missing required parameter '{p.Name}'.");
                }
            }
            return bound;
        }
    }

    internal class CompiledCodeAction : IActionExecutor
    {
        private readonly CodeActionDefinitionXml _def;
        private readonly Func<WorkflowExecutionContext, IReadOnlyDictionary<string, string>, Task> _compiled;

        public CompiledCodeAction(CodeActionDefinitionXml def, Func<WorkflowExecutionContext, IReadOnlyDictionary<string, string>, Task> compiled)
        {
            _def = def;
            _compiled = compiled;
        }

        public string Name => _def.Name;

        public Task ExecuteAsync(WorkflowExecutionContext context, string rawJsonParameters)
        {
            var bound = CodeActionCompiler.BindParameters(_def, rawJsonParameters);
            return _compiled(context, bound);
        }
    }

    internal class CompiledCodeCondition : IConditionExecutor
    {
        private readonly CodeActionDefinitionXml _def;
        private readonly Func<WorkflowExecutionContext, IReadOnlyDictionary<string, string>, Task<bool>> _compiled;

        public CompiledCodeCondition(CodeActionDefinitionXml def, Func<WorkflowExecutionContext, IReadOnlyDictionary<string, string>, Task<bool>> compiled)
        {
            _def = def;
            _compiled = compiled;
        }

        public string Name => _def.Name;

        public Task<bool> EvaluateAsync(WorkflowExecutionContext context, string rawJsonParameters)
        {
            var bound = CodeActionCompiler.BindParameters(_def, rawJsonParameters);
            return _compiled(context, bound);
        }
    }
}
