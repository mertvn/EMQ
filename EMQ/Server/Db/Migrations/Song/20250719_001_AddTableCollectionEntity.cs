using System.Data;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20250719_001)]
public class AddTableCollectionEntity : Migration
{
    private string tableName = "collection_entity";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("collection_id").AsInt32().PrimaryKey().ForeignKey("collection", "id").OnDelete(Rule.Cascade)
            .WithColumn("entity_id").AsInt32().PrimaryKey()
            .WithColumn("modified_at").AsDateTimeOffset().NotNullable()
            .WithColumn("modified_by").AsInt32().NotNullable(); // todo FK

        Create.Index().OnTable(tableName).InSchema("public").OnColumn("entity_id");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
