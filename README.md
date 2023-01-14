# Eroge Music Quiz

No, all-ages VNs aren't excluded. Eroge = Visual Novel. Name was chosen as such for different reasons (mainly browser
autocomplete). Inspired by [AMQ](https://animemusicquiz.com/).

Currently running on a free service at https://emq.up.railway.app, which only provides 500 execution hours or $5 worth
of resource usage per month, so it will be down after the 21st of every month or even earlier if people use it too much
w

## TODO

* Restart songs on results phase option
* Proper authentication/authorization
* CSS... (help!)
* auto-generate autocomplete list
* coop: ability to click teammate's answer to switch to it
* coop: team answers should be determined by count
* trim before autocomplete
* 6+ players everything stretching
* https://developer.mozilla.org/en-US/docs/Web/API/Window/beforeunload_event
* Japanese song titles (VGMdb, EGS)
* player -> (site)user + (quiz)player
* check if players are still active ^prerequisite
* song lengths (~~EGS~~ usually has the full-size lengths, maybe link scanning?)
* song link selection ^prerequisite
* ~~leaving a room in the middle of a quiz and then starting a new quiz causes double timer tick~~ applied band-aid

## Steps to import data

1. Create a new postgres database, set the DATABASE_URL environment variable to
   its [connection URI](https://www.postgresql.org/docs/current/libpq-connect.html#LIBPQ-CONNSTRING). Example:
   postgresql://postgres:postgres@localhost:5432/EMQ
2. Run the project (EMQ.Server) once in order for the database migrations to run. Terminate it after it's done.
3. Go to https://query.vndb.org and run all of the queries located in Queries/VNDB, download the results as json and put
   them where they belong (check EMQ/Server/DB/Imports/VndbImporter.cs for the correct filenames and where to put them)
4. Run test VNDBStaffNotesParserTests.Test_Batch() (~6 seconds)
5. Run test EntryPoints.ImportVndbData() (~5 minutes) in order to import everything but the song links
6. (Optional) Run test EntryPoints.ImportSongLite() (~4 seconds) in order to import the song links if you have a
   SongLite.json file from before. You may encounter exceptions on this step if VNDB data has been modified since you
   last imported data. Manually edit your SongLite.json file to fix any discrepancies.

## Credits

### Licenses

Bootstrap: MIT License.

Open-iconic: MIT License and SIL OPEN FONT LICENSE Version 1.1.

Blazorise: Apache License 2.0.

VNDB: Data obtained under Open Data Commons Open Database License (ODbL) v1.0.

### People

rampaa: Help with the database schema and VNDB queries

hslead: Avatar "Auu"

Burnal: Motivational support

Ryuu: Added a lot of song links
