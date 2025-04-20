using System.Linq;
using System.Threading.Tasks;
using EMQ.Server;
using EMQ.Server.Db;
using EMQ.Shared.Core;
using NUnit.Framework;

namespace Tests;

[SetUpFixture]
public class Setup
{
    public static string[] BlacklistedCreaterNames { get; set; } =
    {
        "TOSHI", "CAS", "RIO", "Hiro", "maya", "YUINA", "AYA", "koro", "cittan*", "Ryo", "marina", "GORO", "rian",
        "MIU", "tria", "tria+", "Ne;on", "Ne;on Otonashi", "KILA", "rie kito", "A BONE", "satsuki", "Antistar",
        "anporin", "mio", "ちづ", "SAORI", "yui", "ゆい", "masa", "yuri", "SHIKI", "momo", "ayumu", "rin", "yuki",
        "sana", "ms", "yuuka", "mao", "kana", "mayumi", "rino", "yukari", "kei", "ari", "yun", "uma", "sae",
        "sakura", "YOSHIAKI",
    };

    [OneTimeSetUp]
    public async Task RunBeforeTests()
    {
        // Console.WriteLine(Directory.GetCurrentDirectory());
        DotEnv.Load("../../../../.env");

        // todo important: don't run this if the db doesn't exist
        await DbManager.Init();

        BlacklistedCreaterNames = BlacklistedCreaterNames.Select(x => x.NormalizeForAutocomplete()).ToArray();
    }

    [OneTimeTearDown]
    public void RunAfterTests()
    {
    }
}
