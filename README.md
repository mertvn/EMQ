# Eroge Music Quiz

No, all-ages VNs aren't excluded. Eroge = Visual Novel. Name was chosen as such for different reasons (mainly browser
autocomplete). Inspired by [AMQ](https://animemusicquiz.com/).

Currently running on a free service at https://emq.up.railway.app, which only provides 500 execution hours per month, so it will be down after the 21st of every month.

## Steps to import data

1. Create a new postgres database, set the DATABASE_URL environment variable to
   its [connection URI](https://www.postgresql.org/docs/current/libpq-connect.html#LIBPQ-CONNSTRING). Example:
   postgresql://postgres:postgres@localhost:5432/EMQ
2. Run the project (EMQ.Server) once in order for the database migrations to run. Terminate it after it's done.
3. Go to https://query.vndb.org and run all of the queries located in Queries/VNDB, download the results as json and put
   them where they belong (check EMQ/Server/DB/Imports/VNDB/VndbImporter.cs for the correct filenames and where to put them).
4. Run test VNDBStaffNotesParserTests.Test_Batch() (~8 seconds).
5. Run test EntryPoints.ImportVndbData() (~7 minutes) in order to import everything but the song links.
6. (Optional) Run test EntryPoints.ImportSongLite() (~15 seconds) in order to import the song links if you have a
   SongLite.json file from before. You may encounter exceptions on this step if VNDB data has been modified since you
   last imported data. Manually edit your SongLite.json file to fix any discrepancies.
7. (Optional) Run the EGS query and run test EntryPoints.ImportEgsData() (~2 minutes) in order to import Japanese song titles.

## Credits

rampaa: Help with the database schema and VNDB queries

hslead: Avatar "Auu" and favicon "Love"

Burnal: Motivational support

Ryuu: Added a lot of song links
