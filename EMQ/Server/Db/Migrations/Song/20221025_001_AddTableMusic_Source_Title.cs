using FluentMigrator;
using FluentMigrator.Model;
using FluentMigrator.Postgres;

namespace EMQ.Server.Db.Migrations;

[Tags("SONG")]
[Migration(20221025_001)]
public class AddTableMusic_Source_Title : Migration
{
    private string tableName = "music_source_title";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("music_source_id").AsInt32().PrimaryKey().ForeignKey("music_source", "id")
            .WithColumn("latin_title").AsString().PrimaryKey()
            .WithColumn("non_latin_title").AsString().Nullable()
            .WithColumn("language").AsString().PrimaryKey()
            .WithColumn("is_main_title").AsBoolean().NotNullable();

        Execute.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm");
        Execute.Sql("CREATE EXTENSION IF NOT EXISTS btree_gin");

        Execute.Sql("CREATE INDEX trgm_idx_mst_latin_title ON music_source_title USING gin (latin_title gin_trgm_ops) WITH ( FASTUPDATE = ON );");
        Execute.Sql("CREATE INDEX trgm_idx_mst_non_latin_title ON music_source_title USING gin (non_latin_title gin_trgm_ops) WITH ( FASTUPDATE = ON );");

        // Create.Index().OnTable(tableName).InSchema("public").WithOptions().UsingGin().FastUpdate().NonClustered().OnColumn("latin_title");
        // Create.Index().OnTable(tableName).InSchema("public").WithOptions().UsingGin().FastUpdate().NonClustered().OnColumn("non_latin_title");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
