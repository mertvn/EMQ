using FluentMigrator;

namespace EMQ.Server.Db.Migrations;

[Tags("SONG")]
[Migration(20230112_000)]
public class AlterTableCategory : Migration
{
    private string tableName = "category";

    public override void Up()
    {
        Alter.Table(tableName)
            .AddColumn("vndb_id").AsString().Nullable();

         Create.UniqueConstraint()
            .OnTable(tableName)
            .Columns("vndb_id", "type");
    }

    public override void Down()
    {
        Delete.Column("vndb_id").FromTable(tableName);

        // todo delete unique constraint?
    }
}
