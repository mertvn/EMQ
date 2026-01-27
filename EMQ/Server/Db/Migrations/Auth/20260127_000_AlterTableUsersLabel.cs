using System;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Auth;

[Tags("AUTH")]
[Migration(20260127_000)]
public class AlterTableUsersLabel : Migration
{
    public override void Up()
    {
        Execute.Sql(@"
ALTER TABLE users_label ADD COLUMN database_kind int4 NOT NULL DEFAULT 0;

CREATE EXTENSION IF NOT EXISTS btree_gist;

ALTER TABLE users_label
ADD CONSTRAINT exclude_different_vndb_uid_per_preset
EXCLUDE USING gist (
  user_id WITH =,
  preset_name WITH =,
  database_kind WITH =,
  vndb_uid WITH <>
);

DROP INDEX IF EXISTS uc_users_label_user_id_vndbuid_vndblabelid_presetname;

CREATE UNIQUE INDEX uc_users_label_composite ON users_label (user_id, database_kind, vndb_uid, vndb_label_id, preset_name);");
    }

    public override void Down()
    {
        throw new NotImplementedException();
    }
}
