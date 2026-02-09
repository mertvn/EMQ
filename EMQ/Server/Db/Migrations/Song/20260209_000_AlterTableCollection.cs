using System;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20260209_000)]
public class AlterTableCollection : Migration
{
    private string tableName = "collection";

    public override void Up()
    {
        Alter.Table(tableName).AddColumn("visibility").AsInt32().NotNullable().WithDefaultValue(0);
    }

    public override void Down() => throw new NotImplementedException();
}
