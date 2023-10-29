drop table if exists aaa_rids;
select distinct r.id as rid, rg.id as rgid, aaa.vgmdburl, aaa.vndbid
into table aaa_rids
from aaa
inner join url u on u.url = aaa.vgmdburl
inner join l_release_url l_ru on l_ru.entity1 = u.id
inner join release r on r.id = l_ru.entity0
inner join release_group rg on rg.id = r.release_group
--left join link l on l.id = l_ru.link
--left join link_type lt on lt.id = l.link_type
--where lt.id = 86 -- vgmdb
--where u.url in (select column1 from aaa)
--where vndbid = 'v11857'
order by r.id
