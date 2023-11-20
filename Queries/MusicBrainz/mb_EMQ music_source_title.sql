SELECT *
FROM vn_titles vt

WHERE vt.id = ANY(@vid)

ORDER BY SPLIT_PART(vt.id::text, 'v', 2)::int -- fixes sorting for local copies where we don't have the vndbid type
