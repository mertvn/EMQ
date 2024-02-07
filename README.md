# Eroge Music Quiz

EMQ is an [AMQ](https://animemusicquiz.com/)-like quiz game for eroge/visual novel songs.

Currently running at https://erogemusicquiz.com.
## Steps to import data
### Vocals
1. Create a new postgres database, set the DATABASE_URL environment variable to
   its [connection URI](https://www.postgresql.org/docs/current/libpq-connect.html#LIBPQ-CONNSTRING). Example:
   postgresql://postgres:postgres@localhost:5432/EMQ
2. Run the project (EMQ.Server) once in order for the database migrations to run. Terminate it after it's done.
3. Go to https://query.vndb.org and run all of the queries located in Queries/VNDB, download the results as json and put
   them where they belong (check EMQ/Server/DB/Imports/VNDB/VndbImporter.cs for the correct filenames and where to put them).
4. Run test VNDBStaffNotesParserTests.Test_Batch() (~2 seconds).
5. Run test EntryPoints.ImportVndbData() (~3.5 minutes) in order to import everything but the song links.
6. (Optional) Run test EntryPoints.ImportSongLite() (~30 seconds) in order to import the song links if you have a
   SongLite.json file from before. You may encounter exceptions on this step if VNDB data has been modified since you
   last imported data. Manually edit your SongLite.json file to fix any discrepancies.
7. (Optional) Run the EGS query and run test EntryPoints.ImportEgsData() (~2 minutes) in order to import Japanese song titles.

Also see EntryPoints.FreshSetup() for a method that automates most of these steps.
Using this method requires a local postgres server running, along with some other things.
Also it's likely that you'll encounter an error somewhere along the way when running it for the first time, so you should familiarize yourself with all of the steps first.
### BGM
Requires VNDB data to be already imported.

1. TODO (it's like 17 steps right now :/)

## Credits
rampaa: Help with the database schema and VNDB queries

hslead: Avatar "Auu" and favicon "Love"

Burnal: Motivational support

Thanks to anyone that uploads new song links.

Thanks to VNDB, EGS, and MusicBrainz administration and editors for providing the necessary data.
