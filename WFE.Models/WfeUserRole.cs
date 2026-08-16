
namespace WFE.Models
{
    // Minimal stub - flesh out if/when command authorization needs role checks
    // (e.g. only "PlantOperator" role can invoke a manual override Command).
    public partial class WfeUserRole
    {
        public long Id { get; set; }
        public long ActorId { get; set; }
        public long RoleId { get; set; }
        public string RoleName { get; set; }

        public virtual WfeActor Actor { get; set; }
        public virtual WfeRole Role { get; set; }
    }
}
