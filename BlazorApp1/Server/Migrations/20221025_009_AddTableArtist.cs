using FluentMigrator;

namespace BlazorApp1.Server.Migrations;

[Migration(20221025_009)]
public class AddTableArtist : Migration
{
    private string tableName = "artist";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("name").AsString().NotNullable()
            .WithColumn("sex").AsInt32().Nullable()
            ;
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
