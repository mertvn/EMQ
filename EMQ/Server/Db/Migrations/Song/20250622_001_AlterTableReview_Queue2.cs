using System;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20250622_001)]
public class AlterTableReview_Queue2 : Migration
{
    private string tableName = "review_queue";

    public override void Up()
    {
        Alter.Table(tableName).AddColumn("attributes").AsInt32().NotNullable().WithDefaultValue(0);
        Alter.Table(tableName).AddColumn("lineage").AsInt32().NotNullable().WithDefaultValue(0);
        Alter.Table(tableName).AddColumn("comment").AsString(4096).NotNullable().WithDefaultValue("");
    }

    public override void Down() => throw new NotImplementedException();
}
