using System.Data;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20240825_000)]
public class AddTableErodle : Migration
{
    private string tableName = "erodle";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("date").AsDate().NotNullable()
            .WithColumn("kind").AsInt32().NotNullable()
            .WithColumn("correct_answer").AsString().NotNullable();

        Create.UniqueConstraint()
            .OnTable(tableName)
            .Columns("date", "kind");

        Create.UniqueConstraint()
            .OnTable(tableName)
            .Columns("kind", "correct_answer");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
