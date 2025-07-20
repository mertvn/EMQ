using System.Collections.Generic;
using EMQ.Shared.Core.SharedDbEntities;

namespace EMQ.Shared.Library.Entities.Concrete;

public class CollectionContainer
{
    public CollectionContainer(Collection collection, List<CollectionUsers> collectionUsers,
        List<CollectionEntity> collectionEntities)
    {
        Collection = collection;
        CollectionUsers = collectionUsers;
        CollectionEntities = collectionEntities;
    }

    public Collection Collection { get; }

    public List<CollectionUsers> CollectionUsers { get; }

    public List<CollectionEntity> CollectionEntities { get; }
}
