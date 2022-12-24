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
* less clunkier Auto-complete (might need to use JS libraries or write something custom)
* player left room event
* restore state on page refresh
* auto-generate autocomplete list
* coop: ability to click teammate's answer to switch to it
* coop: team answers should be determined by count
* QuizLog
* trim before autocomplete
* 6+ players everything stretching
* rejoining player double losing lives?
* quiz continuing after lives = 0 rarely (ghost player with >0 lives?)
* https://developer.mozilla.org/en-US/docs/Web/API/Window/beforeunload_event
* previous song playing instead of the new song when the phase change message to the results phase is not received due
  to a disconnect
* player -> (site)user + (quiz)player
* Japanese song titles (VGMdb, EGS)
* song lengths (EGS, maybe link scanning?)
* leaving a room in the middle of a quiz and then starting a new quiz causes double timer tick etc.

## Steps to import data

1. Create a new postgres database, set the DATABASE_URL environment variable to
   its [connection URI](https://www.postgresql.org/docs/current/libpq-connect.html#LIBPQ-CONNSTRING). Example:
   postgresql://postgres:postgres@localhost:5432/EMQ
2. Run the project (EMQ.Server) once in order for the database migrations to run. Terminate it after it's done.
3. Go to https://query.vndb.org and run all of the queries located in Queries/VNDB, download the results as json and put
   them where they belong (check EMQ/Server/DB/Imports/VndbImporter.cs for the correct filenames and where to put them)
4. Run test VNDBStaffNotesParserTests.Test_Batch() (~6 seconds)
5. Run test DbTests.ImportVndbData() (~5 minutes) in order to import everything but the song links
6. (Optional) Run test DbTests.ImportSongLite() (~4 seconds) in order to import the song links if you have a
   SongLite.json file from before. You may encounter exceptions on this step if VNDB data has been modified since you
   last imported data. Manually edit your SongLite.json file to fix any discrepancies.

## Credits

### Licenses

Bootstrap: MIT License.

Open-iconic: MIT License and SIL OPEN FONT LICENSE Version 1.1.

VNDB: Data obtained under Open Data Commons Open Database License (ODbL) v1.0.

### People

rampaa: Help with the database schema and VNDB queries

hslead: Avatar "Auu"

Burnal: Motivational support

Ryuu: Added a lot of song links
