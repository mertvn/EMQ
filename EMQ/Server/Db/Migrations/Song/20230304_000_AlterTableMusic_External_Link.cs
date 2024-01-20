using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20230304_000)]
public class AlterTableMusic_External_Link : Migration
{
    private string tableName = "music_external_link";

    public override void Up()
    {
        Alter.Table(tableName)
            .AddColumn("duration").AsTime().NotNullable();
        Alter.Table(tableName)
            .AddColumn("submitted_by").AsString().Nullable();
        Alter.Table(tableName)
            .AddColumn("sha256").AsString().Nullable(); // todo not null

        Execute.Sql("CREATE INDEX trgm_idx_mel_submitted_by ON music_external_link USING gin (submitted_by gin_trgm_ops) WITH ( FASTUPDATE = ON );");
        Execute.Sql("CREATE INDEX idx_mel_sha256 ON music_external_link(sha256);");
    }

    public override void Down()
    {
        Delete.Column("duration").FromTable(tableName);
        Delete.Column("submitted_by").FromTable(tableName);
    }
}
