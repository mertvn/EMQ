SELECT *
FROM vn_titles vt
WHERE vt.id IN

(
SELECT v.id
FROM vn_staff vs
JOIN vn v ON v.id = vs.id
JOIN releases_vn rv ON rv.vid = v.id
JOIN releases r ON r.id = rv.id
JOIN staff_alias sa ON sa.aid = vs.aid 
WHERE vs.role::text ~* 'songs' 
AND vs.note ~* '(")|(“)|(”)|('')'
AND r.released != 99999999
GROUP BY v.id
)


ORDER BY SPLIT_PART(vt.id::text, 'v', 2)::int -- fixes sorting for local copies where we don't have the vndbid type
