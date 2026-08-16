using System.Collections.Generic;
using System.Linq;
using WFE.Core.Schema;
using WFE.Models;

namespace WFE.Runtime
{
    public class CommandService
    {
        private readonly TransitionEngine _transitionEngine;

        public CommandService(TransitionEngine transitionEngine)
        {
            _transitionEngine = transitionEngine;
        }

        /// <summary>
        /// TODO(auth): once WfeUserRole is meaningful, filter here by whether `actorId`'s
        /// roles are permitted to invoke each transition. Currently every command reachable
        /// from the activity is returned regardless of actor.
        /// </summary>
        public IReadOnlyList<WfeCommand> BuildAvailableCommands(
            ResolvedProcessSchema schema, WfeProcessInstance instance, string actorId)
        {
            var transitions = _transitionEngine.GetCommandTransitions(schema, instance.Activity);

            return transitions.Select(t =>
            {
                var commandName = t.Triggers.First(tr => tr.Type == "Command").NameRef;
                return new WfeCommand
                {
                    Id = $"{instance.Id}:{t.Name}",
                    TransitionId = t.Name,
                    SchemeId = instance.ProcessSchemeId,
                    ActorId = actorId,
                    Activity = instance.Activity,
                    Title = schema.CommandsByName.TryGetValue(commandName, out var cmd) ? cmd.Name : commandName,
                    IsDynamic = false,
                    InstanceIds = new List<long> { instance.Id }
                };
            }).ToList();
        }
    }
}
