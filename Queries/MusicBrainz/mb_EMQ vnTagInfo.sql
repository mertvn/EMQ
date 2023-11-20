SELECT v.id AS "VNID",
json_agg(DISTINCT jsonb_build_object('t', tvi.tag, 'r', TRUNC(tvi.rating::numeric, 2), 's', tvi.spoiler)) AS "TVIs"
                                  
FROM vn_staff vs
JOIN vn v ON v.id = vs.id
JOIN releases_vn rv ON rv.vid = v.id
JOIN releases r ON r.id = rv.id
JOIN tags_vn_inherit tvi on tvi.vid = v.id
WHERE 1=1

AND v.id = ANY(@vid)

AND r.official = true
group by v.id
ORDER BY SPLIT_PART(v.id::text, 'v', 2)::int -- fixes sorting for local copies where we don't have the vndbid type
