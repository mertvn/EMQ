SELECT id, name FROM tags 
WHERE searchable = true
ORDER BY SPLIT_PART(id::text, 'g', 2)::int -- fixes sorting for local copies where we don't have the vndbid type
