﻿using System.Data;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20221025_011)]
public class AddTableArtist_Music : Migration
{
    private string tableName = "artist_music";

    public override void Up()
    {
        Create.Table(tableName)
            // no cascade to require that artist is removed from all songs before deletion
            .WithColumn("artist_id").AsInt32().PrimaryKey().ForeignKey("artist", "id")
            .WithColumn("music_id").AsInt32().PrimaryKey().ForeignKey("music", "id").OnDelete(Rule.Cascade)
            .WithColumn("role").AsInt32().PrimaryKey()
            .WithColumn("artist_alias_id").AsInt32().NotNullable().ForeignKey("artist_alias", "id");

        Create.Index().OnTable(tableName).InSchema("public").OnColumn("music_id").Ascending().OnColumn("artist_id");
        Create.Index().OnTable(tableName).InSchema("public").OnColumn("artist_alias_id").Ascending().OnColumn("artist_id");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
