
namespace Hokm.Domain.Entities
{
    public class BaseEntity
    {
        public Guid Id { get; private set; }
        public DateTime CreateDate { get; private set; }
        public Guid RowVersion { get; private set; }
        protected BaseEntity()
        {
            Id = Guid.NewGuid();
            CreateDate = DateTime.UtcNow;
            RowVersion = Guid.NewGuid();
        }
        public void IncrementVersion()
        {
            RowVersion = Guid.NewGuid();
        }
        protected BaseEntity(Guid? id = null)
        {
            Id = id ?? Guid.NewGuid();
        }
    }
}
