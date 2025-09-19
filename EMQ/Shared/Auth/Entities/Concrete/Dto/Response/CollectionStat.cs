using System;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Response;

public class CollectionStat
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public int OwnerUserId { get; set; }

    public string OwnerUsername { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public int NumEntities { get; set; }
}
