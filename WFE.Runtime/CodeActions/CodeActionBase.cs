using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using WFE.Core.Runtime;
using WFE.Models;

namespace WFE.Runtime.CodeActions
{
    /// <summary>
    /// Every compiled CodeAction extends this. Deliberately mirrors the call convention seen
    /// in your ParametersAndExpressions.xml sample CodeAction (processInstance.GetParameter&lt;T&gt;,
    /// SetParameter, a "parameter" variable holding the raw ActionParameter JSON) so
    /// designer-exported CodeAction bodies need minimal rewriting - just backed by this
    /// engine's IWorkflowParameterStore instead of OptimaJet's runtime.
    /// </summary>
    public abstract class CodeActionBase
    {
        protected readonly WfeProcessInstance processInstance;
        protected readonly string parameter;
        protected readonly CancellationToken cancellationToken;

        private readonly IWorkflowParameterStore _store;

        protected CodeActionBase(
            WfeProcessInstance processInstance, string parameter,
            IWorkflowParameterStore store, CancellationToken cancellationToken)
        {
            this.processInstance = processInstance;
            this.parameter = parameter;
            _store = store;
            this.cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Synchronous convenience matching the designer's generated call style. This is safe
        /// from the classic "sync-over-async deadlock" here specifically because ASP.NET Core
        /// (Kestrel) has no SynchronizationContext to deadlock against - unlike classic ASP.NET
        /// or a WinForms/WPF app, where GetAwaiter().GetResult() on UI/request threads is
        /// genuinely dangerous. Still, prefer GetParameterAsync/SetParameterAsync in
        /// IsAsync="True" CodeActions if you're writing new ones.
        /// Only simple, string-convertible types are supported (string, int, double, bool,
        /// DateTime, decimal, ...) since parameters are stored as strings - for anything more
        /// complex, deserialize the raw string yourself (e.g. with Newtonsoft.Json, already
        /// referenced and available to CodeAction Usings).
        /// </summary>
        protected T GetParameter<T>(string name)
        {
            var raw = _store.GetAsync(processInstance.Id, name).GetAwaiter().GetResult();
            if (raw == null) return default;
            if (typeof(T) == typeof(string)) return (T)(object)raw;
            return (T)Convert.ChangeType(raw, typeof(T), CultureInfo.InvariantCulture);
        }

        protected void SetParameter(string name, string value) =>
            _store.SetAsync(processInstance.Id, name, value).GetAwaiter().GetResult();

        protected Task<string> GetParameterAsync(string name) => _store.GetAsync(processInstance.Id, name);
        protected Task SetParameterAsync(string name, string value) => _store.SetAsync(processInstance.Id, name, value);
    }
}
