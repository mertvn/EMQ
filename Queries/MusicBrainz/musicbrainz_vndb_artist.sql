with cte as (
select a.gid as mb, json_agg(replace(u.url,'https://vndb.org/','')) as vndbid from url u
join l_artist_url lau on lau.entity1 = u.id
join artist a on a.id = lau.entity0 
where url like '%https://vndb.org/s%'
group by a.gid
) select json_agg(row_to_json(cte.*)) from cte
