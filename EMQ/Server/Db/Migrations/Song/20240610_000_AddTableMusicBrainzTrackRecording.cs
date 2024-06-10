using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20240610_000)]
public class AddTableMusicBrainzTrackRecording : Migration
{
    private string tableName = "musicbrainz_track_recording";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("track").AsGuid().NotNullable()
            .WithColumn("recording").AsGuid().NotNullable();

        Create.UniqueConstraint()
            .OnTable(tableName)
            .Columns("track", "recording");

        Create.Index().OnTable(tableName).InSchema("public").OnColumn("recording");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
