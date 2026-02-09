using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Shared.Core.SharedDbEntities;

[Table("collection_users")]
public class CollectionUsers
{
    [Key]
    [Required]
    public int collection_id { get; set; }

    [Key]
    [Required]
    public int user_id { get; set; }

    [Required]
    [EnumDataType(typeof(CollectionUsersRoleKind))]
    public CollectionUsersRoleKind role { get; set; }
}

public enum CollectionUsersRoleKind
{
    None = 0,
    Editor = 1,
    Owner = 2,
}
