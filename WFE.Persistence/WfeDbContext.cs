using Microsoft.EntityFrameworkCore;
using WFE.Models;

namespace WFE.Persistence
{
    public class WfeDbContext : DbContext
    {
        public WfeDbContext(DbContextOptions<WfeDbContext> options) : base(options) { }

        public DbSet<WfeBusinessProcess> WfeBusinessProcesses { get; set; }
        public DbSet<WfeScheme> WfeSchemes { get; set; }
        public DbSet<WfeProcessScheme> WfeProcessSchemes { get; set; }
        public DbSet<WfeProcessInstance> WfeProcessInstances { get; set; }
        public DbSet<WfeProcessInstanceParameter> WfeProcessInstanceParameters { get; set; }
        public DbSet<WfeProcessTransitionHistory> WfeProcessTransitionsHistory { get; set; }
        // WfeCommand is intentionally NOT a DbSet - it's a transient DTO computed on the fly by
        // CommandService.BuildAvailableCommands from the schema + an instance's current
        // activity, never persisted. If you actually want a durable "pending commands / inbox"
        // table (e.g. for a human task list), that's a legitimate but DIFFERENT feature - it'd
        // need its own entity (with a real FK to WfeProcessInstance, proper indexing, and a
        // lifecycle for when entries get consumed/expired), not a bare DbSet<WfeCommand>. Let me
        // know if that's what you're after and I'll design it properly.
        public DbSet<WfeActor> WfeActors { get; set; }
        public DbSet<WfeRole> WfeRoles { get; set; }
        public DbSet<WfeUserRole> WfeUserRoles { get; set; }
        public DbSet<WfeProcessWorkItem> WfeProcessWorkItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Picks up every IEntityTypeConfiguration<T> in Configurations/ automatically -
            // add a new config class there and it's wired in with no changes needed here.
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(WfeDbContext).Assembly);
        }
    }
}
