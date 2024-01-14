using System;
using System.IO;
using EMQ.Server;
using NUnit.Framework;

namespace Tests;

[SetUpFixture]
public class Setup
{
    [OneTimeSetUp]
    public void RunBeforeTests()
    {
        // Console.WriteLine(Directory.GetCurrentDirectory());
        DotEnv.Load("../../../../.env");
    }

    [OneTimeTearDown]
    public void RunAfterTests()
    {
    }
}
