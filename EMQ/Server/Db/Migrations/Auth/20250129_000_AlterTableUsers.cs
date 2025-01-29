using System;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Auth;

[Tags("AUTH")]
[Migration(20250129_000)]
public class AlterTableUsers : Migration
{
    private string tableName = "users";

    public override void Up()
    {
        Alter.Table(tableName)
            .AddColumn("inc_perm").AsCustom("integer[]").Nullable()
            .AddColumn("exc_perm").AsCustom("integer[]").Nullable();
    }

    public override void Down()
    {
        throw new NotImplementedException();
    }
}
