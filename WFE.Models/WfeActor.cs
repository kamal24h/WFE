using System.Collections.Generic;

namespace WFE.Models
{
    // Replaces "WfeUser is the only kind of actor" assumption. On a plant floor most
    // transitions/commands are invoked by a scheduler or an automation service, not a person -
    // ActorId (already a plain string throughout the schema/history tables) can reference any
    // row here regardless of type, so the engine itself never special-cases "human vs not".
    public partial class WfeActor
    {        
        public long Id { get; set; }
        public string Name { get; set; }

        // "User" | "Scheduler" | "System" | "Device"
        public string ActorType { get; set; }

        public bool Enabled { get; set; }

        // Only meaningful when ActorType == "User" (maps to an external auth provider's id).
        public long? IntegrationAuthenticateId { get; set; }

        public virtual ICollection<WfeUserRole> WfeUserRoles { get; set; }
        public virtual ICollection<WfeProcessTransitionHistory> WfeProcessTransitionHistories { get; set; }

        // Intentionally no WfeInboxes collection by default: inbox/task-list semantics only
        // matter for human actors with something to look at and click. If you add
        // human-approval activities later, add WfeInbox back and scope it to ActorType == "User".
    }
}
