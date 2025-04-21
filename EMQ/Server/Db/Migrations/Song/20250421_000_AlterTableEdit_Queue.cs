using System;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20250421000)]
public class AlterTableEdit_Queue : Migration
{
    public override void Up()
    {
        Execute.Sql(@"
ALTER TABLE edit_queue ADD COLUMN entity_id INTEGER NOT NULL DEFAULT 0;
UPDATE edit_queue SET entity_id = (SELECT (json_extract_path_text(entity_json::json, 'Id'))::integer);
ALTER TABLE edit_queue ALTER COLUMN entity_id DROP DEFAULT;
ALTER TABLE edit_queue ADD CHECK (entity_id > 0);
");
    }

    public override void Down() => throw new NotImplementedException();
}
