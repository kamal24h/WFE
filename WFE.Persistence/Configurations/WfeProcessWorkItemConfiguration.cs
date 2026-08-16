using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WFE.Models;

namespace WFE.Persistence.Configurations
{
    public class WfeProcessWorkItemConfiguration : IEntityTypeConfiguration<WfeProcessWorkItem>
    {
        public void Configure(EntityTypeBuilder<WfeProcessWorkItem> builder)
        {
            builder.HasKey(w => w.Id);
            builder.Property(w => w.Status).IsRequired().HasMaxLength(32);
            builder.Property(w => w.RowVersion).IsRowVersion();

            builder.HasOne(w => w.ParentInstance)
                .WithMany()
                .HasForeignKey(w => w.ParentInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            // The worker's poll query: "give me Pending items, oldest first".
            builder.HasIndex(w => new { w.Status, w.CreatedDateTime });

            // CheckAllSubprocessesCompleted's "how many did this parent spawn" query.
            builder.HasIndex(w => w.ParentInstanceId);
        }
    }
}
