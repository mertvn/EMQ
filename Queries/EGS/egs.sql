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


SELECT ml.id, ml.name, ml.furigana, ml.playtime,
 gm.category,
 gl.gamename, gl.vndb,
 s.*,
 c.id, c.name, c.furigana,
 json_agg(comp.creater),
 json_agg(arr.creater),
 json_agg(lyr.creater)
FROM musiclist ml
inner join game_music gm on ml.id = gm.music
inner join gamelist gl on gl.id = gm.game
inner join singer s on s.music = ml.id
left join composition comp on comp.music = ml.id
left join arrangement arr on arr.music = ml.id
left join lyrics lyr on lyr.music = ml.id
inner join createrlist c on c.id = s.creater
--where s.music= 1602
group by ml.id,gm.category,gl.gamename,gl.vndb,s.music,s.creater,c.id,c.name,c.furigana
order by ml.id
--limit 10


-- todo createrlist query that i forgot to save
