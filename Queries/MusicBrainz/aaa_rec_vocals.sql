drop table if exists aaa_rec_vocals;
select distinct rec.id
into table aaa_rec_vocals
from aaa_rids
inner join release r on r.id = aaa_rids.rid
inner join medium m on m.release = r.id
inner join track t on t.medium = m.id
inner join release_group rg on rg.id = r.release_group 
inner join artist_credit ac on ac.id = t.artist_credit 
inner join recording rec on rec.id = t.recording 
inner join artist_credit_name acn on acn.artist_credit = ac.id
inner join artist a on a.id = acn.artist 
left join l_artist_recording lrw on lrw.entity1 = rec.id
left join link lr on lr.id = lrw.link 
left join link_type ltr on ltr.id = lr.link_type
where 1=1
and (ltr.id = 149) -- vocal
