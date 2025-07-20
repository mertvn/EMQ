using System;
using System.Collections.Generic;

namespace EMQ.Shared.Library.Entities.Concrete.Dto;

public class ResGetCollectionContainers
{
    public Dictionary<int, string> UsernamesDict { get; set; } = new();

    public CollectionContainer[] CollectionContainers { get; set; } = Array.Empty<CollectionContainer>();
}
