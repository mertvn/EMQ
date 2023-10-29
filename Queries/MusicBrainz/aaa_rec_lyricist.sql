-- TODO: keep 'instrumental recording of' https://musicbrainz.org/recording/e4045d72-3881-470c-b4d0-193ffd63e51e
drop table if exists aaa_rec_lyricist;
select 
distinct rec.id
into table aaa_rec_lyricist
from aaa_rids
inner join release r on r.id = aaa_rids.rid
inner join medium m on m.release = r.id
inner join track t on t.medium = m.id
inner join release_group rg on rg.id = r.release_group 
inner join artist_credit ac on ac.id = t.artist_credit 
inner join recording rec on rec.id = t.recording 
inner join artist_credit_name acn on acn.artist_credit = ac.id
inner join artist a on a.id = acn.artist 
left join l_recording_work lrw on lrw.entity0 = rec.id
left join work w on w.id = lrw.entity1
left join l_artist_work law on law.entity1 = w.id
left join link la on la.id = law.link
left join link_type lta on lta.id = la.link_type
where 1=1
and (lta.id = 165) -- lyricist
