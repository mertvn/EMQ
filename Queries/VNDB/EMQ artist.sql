SELECT s.*
FROM vn_staff vs
JOIN vn v ON v.id = vs.id
JOIN releases_vn rv ON rv.vid = v.id
JOIN releases r ON r.id = rv.id
JOIN staff_alias sa ON sa.aid = vs.aid 
JOIN staff s ON s.id = sa.id
where vs.role::text ~* 'songs' 
AND vs.note ~* '(")|(“)|(”)|('')'
AND r.released != 99999999
GROUP BY s.id
