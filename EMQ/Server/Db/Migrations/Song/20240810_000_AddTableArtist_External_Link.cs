using System.Data;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20240810_000)]
public class AddTableArtist_External_Link : Migration
{
    private string tableName = "artist_external_link";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("artist_id").AsInt32().PrimaryKey().ForeignKey("artist", "id").OnDelete(Rule.Cascade)
            .WithColumn("url").AsString().PrimaryKey()
            .WithColumn("type").AsInt32().NotNullable()
            .WithColumn("name").AsString().NotNullable();

        // let's play it safe for now, these restrictions can be relaxed later
        Create.UniqueConstraint().OnTable(tableName).Columns("url");
        Create.UniqueConstraint().OnTable(tableName).Columns("artist_id", "type");

        // migrate old links
        Execute.Sql(@"WITH cte AS (
        SELECT id, vndb_id,
            CASE
        WHEN vndb_id LIKE 's%' THEN 1
        ELSE 2
        END AS linktype
            FROM artist
            )
        INSERT INTO artist_external_link
        SELECT id,CASE WHEN linktype = 1 THEN 'https://vndb.org/'||vndb_id ELSE 'https://musicbrainz.org/artist/'||vndb_id END,linktype,''
        FROM cte");
        Delete.Column("vndb_id").FromTable("artist").InSchema("public");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
