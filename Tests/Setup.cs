﻿using System;
using System.IO;
using System.Threading.Tasks;
using EMQ.Server;
using EMQ.Server.Db;
using NUnit.Framework;

namespace Tests;

[SetUpFixture]
public class Setup
{
    [OneTimeSetUp]
    public async Task RunBeforeTests()
    {
        // Console.WriteLine(Directory.GetCurrentDirectory());
        DotEnv.Load("../../../../.env");

        // todo important: don't run this if the db doesn't exist
        await DbManager.Init();
    }

    [OneTimeTearDown]
    public void RunAfterTests()
    {
    }
}
