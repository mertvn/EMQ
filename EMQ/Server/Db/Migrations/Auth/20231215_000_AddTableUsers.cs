using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Auth;

[Tags("AUTH")]
[Migration(20231215_000)]
public class AddTableUsers : Migration
{
    private string tableName = "users";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("username").AsString().NotNullable()
            .WithColumn("roles").AsInt32().NotNullable()
            .WithColumn("created_at").AsDateTime().NotNullable();

        // todo enable after implementing registration
        // todo? lower()
        // Create.UniqueConstraint()
        //     .OnTable(tableName)
        //     .Columns("username");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
