SELECT sa.id, sa.aid, name, latin
FROM staff s
JOIN staff_alias sa ON sa.id = s.id
WHERE s.id IN

(
SELECT s.id
FROM vn_staff vs
JOIN vn v ON v.id = vs.id
JOIN releases_vn rv ON rv.vid = v.id
JOIN releases r ON r.id = rv.id
JOIN staff_alias sa ON sa.aid = vs.aid 
WHERE r.released != 99999999
and vs.role::text ~* 'songs' 
AND vs.note ~* '(")|(“)|(”)|('')'
GROUP BY s.id
)


ORDER BY SPLIT_PART(s.id::text, 's', 2)::int -- fixes sorting for local copies where we don't have the vndbid type
