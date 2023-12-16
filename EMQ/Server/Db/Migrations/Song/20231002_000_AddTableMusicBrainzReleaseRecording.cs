using FluentMigrator;

namespace EMQ.Server.Db.Migrations;

[Tags("SONG")]
[Migration(20231002_000)]
public class AddTableMusicBrainzReleaseRecording : Migration
{
    private string tableName = "musicbrainz_release_recording";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("release").AsGuid().NotNullable()
            .WithColumn("recording").AsGuid().NotNullable();

        Create.UniqueConstraint()
            .OnTable(tableName)
            .Columns("release", "recording");

        Create.Index().OnTable(tableName).InSchema("public").OnColumn("recording");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
