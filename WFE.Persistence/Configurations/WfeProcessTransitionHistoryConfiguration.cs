using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WFE.Models;

namespace WFE.Persistence.Configurations
{
    public class WfeProcessTransitionHistoryConfiguration : IEntityTypeConfiguration<WfeProcessTransitionHistory>
    {
        public void Configure(EntityTypeBuilder<WfeProcessTransitionHistory> builder)
        {
            builder.HasKey(h => h.Id);

            builder.HasOne(h => h.ProcessInstance)
                .WithMany(i => i.WfeProcessTransitionsHistory)
                .HasForeignKey(h => h.ProcessInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(h => h.ExecutorActor)
                .WithMany(a => a.WfeProcessTransitionHistories)
                .HasForeignKey(h => h.ExecutorActorId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(h => h.ProcessInstanceId);
            builder.HasIndex(h => h.StartTransitionTime);
        }
    }
}
