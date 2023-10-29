with cte as (
select
row_to_json(aaa_rids.*) as aaa_rids,
row_to_json(r.*) as release,
row_to_json(m.*) as medium,
row_to_json(t.*) as track,
row_to_json(rg.*) as release_group,
row_to_json(ac.*) as artist_credit,
row_to_json(rec.*) as recording,
json_agg(distinct acn.*) as json_agg_acn,
json_agg(distinct a.*) as json_agg_a
--u.url
from aaa_rids
inner join release r on r.id = aaa_rids.rid
inner join medium m on m.release = r.id
inner join track t on t.medium = m.id
inner join release_group rg on rg.id = r.release_group 
inner join artist_credit ac on ac.id = t.artist_credit 
inner join recording rec on rec.id = t.recording 
inner join artist_credit_name acn on acn.artist_credit = ac.id
inner join artist a on a.id = acn.artist 
--left join l_artist_url lau on lau.entity0 = a.id
--left join url u on lau.entity1 = u.id
where acn.name is not null AND not t.is_data_track
and m.format != 43 -- data cd https://musicbrainz.org/release/404cf7ad-06a2-41dc-ac7a-e00ad1024d3f
and acn."name" != '[dialogue]' and acn."name" != '[data]'
and (r.status = 1 or r.status = 2 or r.status = 3) -- official, promotion, bootleg
and not exists(select id from aaa_rec_vocals where id = rec.id)
and not exists(select id from aaa_rec_lyricist where id = rec.id)
--and (url like '%https://vndb.org/s%' or url is null)
--and r.gid = '404cf7ad-06a2-41dc-ac7a-e00ad1024d3f'
--and r.id = '3337626'
--and rg.gid = '9ab0b960-93d4-4cf5-8ff2-e2c90248dcc9'
--and t.name like '見よ、我が剣%'
group by
aaa_rids.rid, aaa_rids.rgid, aaa_rids.vgmdburl, aaa_rids.vndbid, aaa_rids.*,
r.id, r.gid, r."name", r.artist_credit, r.release_group, r.status, r."language", r.script, r.barcode, r."comment", r.*,
m.id, m."release", m."position", m.format, m."name", m.track_count , m.*,
t.id, t.gid, t.recording, t.medium, t."position", t."number", t."name", t.artist_credit, t.length, t.*,
rg.id, rg.gid, rg."name", rg.artist_credit, rg."type", rg."comment",  rg.*,
ac.id, ac."name", ac.artist_count, ac.ref_count, ac.gid, ac.*,
rec.id, rec.gid, rec."name", rec.artist_credit, rec.length, rec."comment", rec.edits_pending, rec.last_updated, rec.video
--u.url
ORDER BY SPLIT_PART(aaa_rids.vndbid::text, 'v', 2)::int
) select json_agg(row_to_json(cte.*)) from cte
