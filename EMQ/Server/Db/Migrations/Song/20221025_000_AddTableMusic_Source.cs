using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
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
            .WithColumn("language_original").AsString().NotNullable()
            .WithColumn("rating_average").AsInt32().Nullable()
            .WithColumn("rating_bayesian").AsInt32().Nullable()
            // .WithColumn("popularity").AsInt32().Nullable()
            .WithColumn("votecount").AsInt32().Nullable()
            .WithColumn("type").AsInt32().NotNullable()
            .WithColumn("developers").AsString().Nullable();

        Execute.Sql("ALTER TABLE music_source ADD CHECK (ISFINITE(air_date_start) = 't');");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
