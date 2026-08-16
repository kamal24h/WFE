using System;
using System.Collections.Generic;
using System.Linq;
using WFE.Core.Actions;
using WFE.Core.Conditions;
using WFE.Core.Runtime;
using WFE.Core.Schema;

namespace WFE.Runtime
{
    /// <summary>
    /// Resolves an ActionRef's NameRef to its IActionExecutor. Built-in implementations
    /// (registered in DI, Program.cs) are checked first; if none match, falls back to a
    /// same-named &lt;CodeAction Type="Action"&gt; declared in the CURRENT SCHEMA's own
    /// &lt;CodeActions&gt; block (see ParametersAndExpressions.xml for the shape) - but only if
    /// CodeActionOptions.Enabled is true (see that class for why it defaults to false).
    /// Built-ins always win on a name collision. If neither exists, that's a deploy-time/schema
    /// config error surfaced immediately rather than a silent no-op in production.
    /// </summary>
    public class ActionExecutorRegistry
    {
        private readonly Dictionary<string, IActionExecutor> _byName;
        private readonly CodeActionCompiler _compiler;
        private readonly CodeActionOptions _codeActionOptions;

        public ActionExecutorRegistry(IEnumerable<IActionExecutor> executors, CodeActionCompiler compiler, CodeActionOptions codeActionOptions)
        {
            _byName = executors.ToDictionary(e => e.Name, StringComparer.Ordinal);
            _compiler = compiler;
            _codeActionOptions = codeActionOptions;
        }

        public IActionExecutor Resolve(ResolvedProcessSchema schema, string nameRef)
        {
            if (_byName.TryGetValue(nameRef, out var executor))
                return executor;

            var codeAction = schema.Raw.CodeActions.FirstOrDefault(c =>
                string.Equals(c.Name, nameRef, StringComparison.Ordinal) &&
                string.Equals(c.Type, "Action", StringComparison.Ordinal));

            if (codeAction != null)
            {
                if (!_codeActionOptions.Enabled)
                    throw new InvalidOperationException(
                        $"ActionRef '{nameRef}' resolves to a CodeAction, but CodeAction execution is " +
                        "disabled (set CodeActions:Enabled=true in appsettings.json to allow it - see " +
                        "CodeActionOptions for the security implications first).");

                return _compiler.GetActionExecutor(schema, codeAction);
            }

            throw new InvalidOperationException(
                $"No IActionExecutor is registered for ActionRef NameRef '{nameRef}', and no matching " +
                "CodeAction was found in the current schema. Register an implementation in Program.cs, " +
                "add a CodeAction with this Name to the schema, or fix the schema.");
        }
    }

    public class ConditionExecutorRegistry
    {
        private readonly Dictionary<string, IConditionExecutor> _byName;
        private readonly CodeActionCompiler _compiler;
        private readonly CodeActionOptions _codeActionOptions;

        public ConditionExecutorRegistry(IEnumerable<IConditionExecutor> executors, CodeActionCompiler compiler, CodeActionOptions codeActionOptions)
        {
            _byName = executors.ToDictionary(e => e.Name, StringComparer.Ordinal);
            _compiler = compiler;
            _codeActionOptions = codeActionOptions;
        }

        public IConditionExecutor Resolve(ResolvedProcessSchema schema, string nameRef)
        {
            if (_byName.TryGetValue(nameRef, out var executor))
                return executor;

            var codeAction = schema.Raw.CodeActions.FirstOrDefault(c =>
                string.Equals(c.Name, nameRef, StringComparison.Ordinal) &&
                string.Equals(c.Type, "Condition", StringComparison.Ordinal));

            if (codeAction != null)
            {
                if (!_codeActionOptions.Enabled)
                    throw new InvalidOperationException(
                        $"Condition NameRef '{nameRef}' resolves to a CodeAction, but CodeAction execution " +
                        "is disabled (set CodeActions:Enabled=true in appsettings.json to allow it - see " +
                        "CodeActionOptions for the security implications first).");

                return _compiler.GetConditionExecutor(schema, codeAction);
            }

            throw new InvalidOperationException(
                $"No IConditionExecutor is registered for Condition NameRef '{nameRef}', and no matching " +
                "CodeAction was found in the current schema. Register an implementation in Program.cs, " +
                "add a CodeAction (Type=\"Condition\") with this Name to the schema, or fix the schema.");
        }
    }
}
