SELECT v.id AS "VNID", s.id AS "StaffID", vs.aid AS "ArtistAliasID", sa.name, vs.role, vs.note AS "MusicName",
  CASE WHEN bool_or(p.id IS NOT NULL) THEN json_agg(p.id ORDER BY p.id) ELSE '[]' END AS "ProducerIds"
FROM vn_staff vs
JOIN vn v ON v.id = vs.id
JOIN staff_alias sa ON sa.aid = vs.aid
JOIN staff s ON s.id = sa.id
JOIN releases_vn rv ON rv.vid = v.id
JOIN releases r ON r.id = rv.id
LEFT JOIN releases_producers rp ON rp.id = r.id
LEFT JOIN producers p ON p.id = rp.pid
WHERE vs.role::text ~* 'songs'
AND vs.note ~* '(")|(“)|(”)|('')'
AND vs.note !~* '[\x3040-\x309A]' --No Hiragana
AND vs.note !~* '[\x30A0-\x30DE\x30E0-\x30FA]' --No Katakana (except ミ)
AND vs.note !~* '[\xFF65-\xFF9D]' --No half-width kana
AND vs.note !~* '[\x4E00-\x5F60\x5F62-\x9FFF\x3400-\x4DBF\x20000-\x2A6DF\x2A700-\x2B73F\x2B740-\x2B81F\x2B820-\x2CEAF\x2CEB0-\x2EBEF]' --No CJK (except 彡)
AND v.id IN
(
SELECT v.id
FROM vn_staff vs
JOIN vn v ON v.id = vs.id
JOIN releases_vn rv ON rv.vid = v.id
JOIN releases r ON r.id = rv.id
JOIN staff_alias sa ON sa.aid = vs.aid
WHERE r.released != 99999999
GROUP BY v.id
)
AND r.official = true
group by v.id, s.id, vs.aid, sa.name, vs.role, vs.note
ORDER BY SPLIT_PART(v.id::text, 'v', 2)::int -- fixes sorting for local copies where we don't have the vndbid type
