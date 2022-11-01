Table music_source {
  id int [pk]
  air_date_start datetime
  air_date_end datetime // might be unnecessary
  original_language enum
  average_rating int
  type enum //OVA, Movie etc.
}

Table music_source_title {
  music_source_id int [ref: > music_source.id]
  latin_title varchar
  non_latin_title varchar
  lang enum
  is_main_title boolean
  Indexes {
    (music_source_id, latin_title, lang) [pk]
  }
}

Table music_source_external_link {
  music_source_id int [ref: > music_source.id]
  url varchar
  type enum // VNDB, AniDB etc. // might be unnecessary
    Indexes {
    (music_source_id, url) [pk]
  }
}

Table category {
  id int [pk]
  name varchar
  type enum // Tag, Genre etc.
}

Table music_source_category {
  music_source_id int [ref: <> music_source.id]
  category_id int [ref: <> category.id]
    Indexes {
    (music_source_id, category_id) [pk]
  }
}

Table music {
  id int [pk]
  length int
  type enum //Instrumental etc.
}

// Not sure if this is needed.
// Can a music really have more than one name/title?
Table music_title {
  music_id int [ref: > music.id]
  latin_title varchar
  non_latin_title varchar
  lang enum
  is_main_title boolean
  Indexes {
    (music_id, latin_title, lang) [pk]
  }
}

Table music_source_music {
  music_source_id int [ref: <> music_source.id]
  music_id int [ref: <> music.id]
  type enum // ED, OP, Insert etc.
    Indexes {
    (music_source_id, music_id) [pk]
  }
}

Table music_external_link {
  music_id int [ref: > music.id]
  url varchar
  type enum // catbox, openingsmoe etc. // might be unnecessary
  is_video boolean
    Indexes {
    (music_id, url) [pk]
  }
}

Table artist {
  id int [pk]
  primary_language enum
  sex enum //Female, Male, Unknown // might be unnecessary
  vndb_staff_id int 
}

Table artist_alias {
  id int [pk]
  artist_id int [ref: <> artist.id]
  latin_alias varchar
  non_latin_alias varchar
  is_main_name boolean
  Indexes {
    (artist_id, latin_alias) [unique]
  }
}

Table artist_music {
  artist_id int [ref: <> artist.id]
  music_id int [ref: <> music.id]
  artist_alias_id int [ref: <> artist_alias.id] 
  role enum // Singer, Composer etc.
    Indexes {
    (artist_id, music_id) [pk]
  }
}
