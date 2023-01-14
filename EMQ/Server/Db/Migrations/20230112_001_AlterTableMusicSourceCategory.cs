using FluentMigrator;

namespace EMQ.Server.Db.Migrations;

[Migration(20230112_001)]
public class AlterTableMusicSourceCategory : Migration
{
    private string tableName = "music_source_category";

    public override void Up()
    {
        Alter.Table(tableName)
            .AddColumn("rating").AsFloat().Nullable()
            .AddColumn("spoiler_level").AsInt32().Nullable();
    }

    public override void Down()
    {
        Delete.Column("rating").FromTable(tableName);
        Delete.Column("spoiler_level").FromTable(tableName);
    }
}
