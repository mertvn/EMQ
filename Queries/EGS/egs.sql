SELECT ml.id, ml.name, ml.furigana, ml.playtime,
 gm.category,
 gl.gamename, gl.vndb,
 s.*,
 c.id, c.name, c.furigana
FROM musiclist ml
inner join game_music gm on ml.id = gm.music
inner join gamelist gl on gl.id = gm.game
inner join singer s on s.music = ml.id
inner join createrlist c on c.id = s.creater
order by ml.id
