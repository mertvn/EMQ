using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20221025_009)]
public class AddTableArtist : Migration
{
    private string tableName = "artist";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("sex").AsInt32().Nullable()
            .WithColumn("primary_language").AsString().Nullable()
            .WithColumn("vndb_id").AsString().Nullable();

        Create.Index().OnTable(tableName).InSchema("public").OnColumn("vndb_id").Unique();
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
