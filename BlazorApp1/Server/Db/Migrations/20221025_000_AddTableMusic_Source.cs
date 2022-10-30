using FluentMigrator;

namespace BlazorApp1.Server.Db.Migrations;

[Migration(20221025_000)]
public class AddTableMusic_Source : Migration
{
    private string tableName = "music_source";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("air_date_start").AsDateTime().NotNullable()
            .WithColumn("air_date_end").AsDateTime().Nullable()
            .WithColumn("language_original").AsInt32().NotNullable()
            .WithColumn("rating_average").AsInt32().Nullable()
            .WithColumn("type").AsInt32().NotNullable()
            ;
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
