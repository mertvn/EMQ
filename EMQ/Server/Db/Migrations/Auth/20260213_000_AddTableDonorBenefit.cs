using EMQ.Shared.Core.SharedDbEntities;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Auth;

[Tags("AUTH")]
[Migration(20260213_000)]
public class AddTableDonorBenefit : Migration
{
    private string tableName = "donor_benefit";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("user_id").AsInt32().PrimaryKey().ForeignKey("users", "id")
            .WithColumn("show_donor_badge").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("username_color").AsString().NotNullable().WithDefaultValue("")
            .WithColumn("username_animation").AsInt32().NotNullable().WithDefaultValue((int)UsernameAnimationKind.none);
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
