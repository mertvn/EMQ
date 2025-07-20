using System.Data;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20250719_002)]
public class AddTableCollectionUsers : Migration
{
    private string tableName = "collection_users";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("collection_id").AsInt32().PrimaryKey().ForeignKey("collection", "id").OnDelete(Rule.Cascade)
            .WithColumn("user_id").AsInt32().PrimaryKey() // todo FK
            .WithColumn("role").AsInt32().NotNullable();

        Create.Index().OnTable(tableName).InSchema("public").OnColumn("user_id");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
