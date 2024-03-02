using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20240301_000)]
public class AddTableRoom : Migration
{
    private string tableName = "room";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("initial_name").AsString().NotNullable()
            .WithColumn("created_by").AsInt32().NotNullable() // todo FK
            .WithColumn("created_at").AsDateTime().NotNullable();

        Create.Index().OnTable(tableName).InSchema("public").OnColumn("initial_name");
        Create.Index().OnTable(tableName).InSchema("public").OnColumn("created_by");
        Create.Index().OnTable(tableName).InSchema("public").OnColumn("created_at");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
