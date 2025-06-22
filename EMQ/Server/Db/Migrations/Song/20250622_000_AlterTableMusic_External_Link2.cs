using System;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20250622_000)]
public class AlterTableMusic_External_Link2 : Migration
{
    private string tableName = "music_external_link";

    public override void Up()
    {
        Alter.Table(tableName).AddColumn("attributes").AsInt32().NotNullable().WithDefaultValue(0);
        Alter.Table(tableName).AddColumn("lineage").AsInt32().NotNullable().WithDefaultValue(0);
        Alter.Table(tableName).AddColumn("comment").AsString(4096).NotNullable().WithDefaultValue("");
    }

    public override void Down() => throw new NotImplementedException();
}
