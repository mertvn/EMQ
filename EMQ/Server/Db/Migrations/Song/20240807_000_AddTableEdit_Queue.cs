using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20240807_000)]
public class AddTableEdit_Queue : Migration
{
    private string tableName = "edit_queue";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("submitted_by").AsString().NotNullable() // todo FK
            .WithColumn("submitted_on").AsDateTimeOffset().NotNullable()
            .WithColumn("status").AsInt32().NotNullable()
            .WithColumn("entity_kind").AsInt32().NotNullable()
            .WithColumn("entity_version").AsInt32().NotNullable()
            .WithColumn("entity_json").AsString().NotNullable()
            .WithColumn("old_entity_json").AsString().Nullable()
            .WithColumn("note_user").AsString().Nullable()
            .WithColumn("note_mod").AsString().Nullable();

        Alter.Table("music").AddColumn("data_source").AsInt32().NotNullable().WithDefaultValue(0);
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
