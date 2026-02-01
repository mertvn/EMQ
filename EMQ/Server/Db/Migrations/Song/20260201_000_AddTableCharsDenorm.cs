using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20260201_002)]
public class AddTableCharsDenorm : Migration
{
    private string tableName = "chars_denorm";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("vid").AsString().PrimaryKey()
            .WithColumn("cid").AsString().PrimaryKey()
            .WithColumn("image").AsString().NotNullable()
            .WithColumn("latin").AsString().Nullable()
            .WithColumn("name").AsString().NotNullable()
            .WithColumn("role").AsInt32().Nullable();

        Create.Index().OnTable(tableName).InSchema("public").OnColumn("cid");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
