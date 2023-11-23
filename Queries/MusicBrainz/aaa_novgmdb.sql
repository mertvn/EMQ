insert into aaa_rids
select distinct r.id as rid, rg.id as rgid, null as vgmdburl, aaa_novgmdb.vndbid
from aaa_novgmdb
inner join release r on r.gid = aaa_novgmdb.releasegid::uuid
inner join release_group rg on rg.id = r.release_group
order by r.id
