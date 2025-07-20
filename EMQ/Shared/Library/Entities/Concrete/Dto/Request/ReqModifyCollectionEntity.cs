namespace EMQ.Shared.Library.Entities.Concrete.Dto.Request;

public class ReqModifyCollectionEntity
{
    public ReqModifyCollectionEntity(int collectionId, int entityId, bool isAdded)
    {
        CollectionId = collectionId;
        EntityId = entityId;
        IsAdded = isAdded;
    }

    public int CollectionId { get; set; }

    public int EntityId { get; set; }

    public bool IsAdded { get; set; }
}
