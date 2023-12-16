using FluentMigrator;

namespace EMQ.Server.Db.Migrations;

[Tags("SONG")]
[Migration(20221025_010)]
public class AddTableArtist_Alias : Migration
{
    private string tableName = "artist_alias";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("artist_id").AsInt32().ForeignKey("artist", "id")
            .WithColumn("latin_alias").AsString().NotNullable()
            .WithColumn("non_latin_alias").AsString().Nullable()
            .WithColumn("is_main_name").AsBoolean().NotNullable();

        Create.UniqueConstraint()
            .OnTable(tableName)
            .Columns("artist_id", "latin_alias");

        Execute.Sql("CREATE INDEX idx_artist_alias_latin_alias_lower ON artist_alias(lower(latin_alias))");
        // Create.Index().OnTable(tableName).InSchema("public").OnColumn("lower(latin_alias)");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
