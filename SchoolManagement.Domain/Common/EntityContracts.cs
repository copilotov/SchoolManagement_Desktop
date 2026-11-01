namespace SchoolManagement.Domain.Common;

/// <summary>Every persisted entity uses the original integer auto-increment id.</summary>
public interface IEntity
{
    int Id { get; set; }
}

/// <summary>Entities that own a <c>created_at</c> column.</summary>
public interface ICreatedEntity : IEntity
{
    DateTime CreatedAt { get; set; }
}

/// <summary>Entities that own both <c>created_at</c> and <c>updated_at</c>.</summary>
public interface IAuditableEntity : ICreatedEntity
{
    DateTime UpdatedAt { get; set; }
}

/// <summary>Entities that only own an <c>updated_at</c> column.</summary>
public interface IUpdatedEntity : IEntity
{
    DateTime UpdatedAt { get; set; }
}
