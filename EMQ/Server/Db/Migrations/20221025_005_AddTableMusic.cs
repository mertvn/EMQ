using FluentMigrator;

namespace EMQ.Server.Db.Migrations;

[Migration(20221025_005)]
public class AddTableMusic : Migration
{
    private string tableName = "music";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("type").AsInt32().NotNullable()
            .WithColumn("stat_correct").AsInt64().NotNullable().WithDefaultValue(0)
            .WithColumn("stat_played").AsInt64().NotNullable().WithDefaultValue(0)
            .WithColumn("stat_guessed").AsInt64().NotNullable().WithDefaultValue(0)
            .WithColumn("stat_totalguessms").AsInt64().NotNullable().WithDefaultValue(0);

        Execute.Sql(
            @"ALTER TABLE music ADD CONSTRAINT check_stats CHECK ( stat_correct <= stat_played );");

        // https://stackoverflow.com/a/17681467
        // https://stackoverflow.com/questions/75906508/generated-column-not-producing-expected-results#comment133886457_75906508
        Execute.Sql(
            @"ALTER TABLE music ADD stat_correctpercentage float4 GENERATED ALWAYS AS ((1.0 * stat_correct / COALESCE(NULLIF(stat_played, 0), 1)) * 100) STORED");

        Execute.Sql(
            @"ALTER TABLE music ADD stat_averageguessms int GENERATED ALWAYS AS ( stat_totalguessms / COALESCE(NULLIF(stat_guessed, 0), 1)) STORED");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
