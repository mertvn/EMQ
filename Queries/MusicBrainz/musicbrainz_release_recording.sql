with cte as (
select distinct rec.gid as recording_gid, r.gid as release_gid
from aaa_rids
inner join release r on r.id = aaa_rids.rid
inner join medium m on m.release = r.id
inner join track t on t.medium = m.id
inner join release_group rg on rg.id = r.release_group 
inner join artist_credit ac on ac.id = t.artist_credit 
inner join recording rec on rec.id = t.recording 
inner join artist_credit_name acn on acn.artist_credit = ac.id
inner join artist a on a.id = acn.artist 
where acn.name is not null AND not t.is_data_track
and m.format != 43 -- data cd https://musicbrainz.org/release/404cf7ad-06a2-41dc-ac7a-e00ad1024d3f
and acn."name" != '[dialogue]' and acn."name" != '[data]'
and (r.status = 1 or r.status = 2 or r.status = 3) -- official, promotion, bootleg
and not exists(select id from aaa_rec_vocals where id = rec.id)
and not exists(select id from aaa_rec_lyricist where id = rec.id)
) select json_agg(row_to_json(cte.*)) from cte
