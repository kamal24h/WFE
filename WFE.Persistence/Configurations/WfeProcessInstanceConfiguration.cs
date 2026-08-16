using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WFE.Models;

namespace WFE.Persistence.Configurations
{
    public class WfeProcessInstanceConfiguration : IEntityTypeConfiguration<WfeProcessInstance>
    {
        public void Configure(EntityTypeBuilder<WfeProcessInstance> builder)
        {
            builder.HasKey(i => i.Id);

            builder.Property(i => i.Activity).IsRequired().HasMaxLength(256);
            builder.Property(i => i.State).HasMaxLength(256);
            builder.Property(i => i.Status).IsRequired().HasMaxLength(32);
            builder.Property(i => i.CorrelationId).HasMaxLength(256);

            // RowVersion is the optimistic-concurrency token (see WorkflowRuntime's
            // command-vs-auto-transition race protection) - EF Core picks this up
            // automatically from the [Timestamp] attribute, this just makes it explicit.
            builder.Property(i => i.RowVersion).IsRowVersion();

            builder.HasOne(i => i.ProcessScheme)
                .WithMany()
                .HasForeignKey(i => i.ProcessSchemeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Self-referencing parent/child fork relationship (Phase 3). Restrict, not
            // Cascade: SQL Server rejects multiple cascade paths to the same table, and
            // deleting a parent should be a deliberate decision about its children anyway,
            // not an automatic side effect.
            builder.HasOne(i => i.ParentInstance)
                .WithMany(i => i.ChildInstances)
                .HasForeignKey(i => i.ParentInstanceId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(i => i.ForkTransitionName).HasMaxLength(256);
            builder.HasIndex(i => i.ParentInstanceId);
            builder.HasIndex(i => i.RootInstanceId);

            // ScheduleWorker's poll query: "Waiting instances whose scheduled time has passed".
            builder.HasIndex(i => new { i.Status, i.NextScheduledCheckTime });

            // Every packet lookup ("has tag X been processed", "show me recent instances for
            // this scheme") goes through these two - index both. Status is low-cardinality but
            // "find all Waiting instances" is a common poll query for the schedule/command UI.
            builder.HasIndex(i => i.CorrelationId);
            builder.HasIndex(i => new { i.ProcessSchemeId, i.Status });
            builder.HasIndex(i => i.CreationDateTime);
        }
    }
}
