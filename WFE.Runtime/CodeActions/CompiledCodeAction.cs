using System;
using System.Threading;
using System.Threading.Tasks;
using WFE.Core.Runtime;
using WFE.Models;

namespace WFE.Runtime.CodeActions
{
    public class CompiledCodeAction
    {
        public bool IsCondition { get; init; }

        public Func<WfeProcessInstance, string, IWorkflowParameterStore, CancellationToken, Task> ActionInvoker { get; init; }

        public Func<WfeProcessInstance, string, IWorkflowParameterStore, CancellationToken, Task<bool>> ConditionInvoker { get; init; }
    }
}
