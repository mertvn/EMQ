SELECT v.id, v.title, min(r.released) AS "air_date_start", v.original, v.alias, v.olang, v.l_wikidata, v.c_votecount, v.c_popularity, v.c_rating, v.c_average, v.l_renai 
FROM vn v
JOIN releases_vn rv ON rv.vid = v.id
JOIN releases r ON r.id = rv.id
WHERE r.released != 99999999 AND rv.rtype != 'trial' AND v.id IN

(
SELECT v.id
FROM vn_staff vs
JOIN vn v ON v.id = vs.id
JOIN releases_vn rv ON rv.vid = v.id
JOIN releases r ON r.id = rv.id
JOIN staff_alias sa ON sa.aid = vs.aid 
WHERE r.released != 99999999
AND rv.rtype != 'trial'
and vs.role::text ~* 'songs' 
AND vs.note ~* '(")|(“)|(”)|('')'
GROUP BY v.id
)

GROUP BY v.id
ORDER BY v.id
