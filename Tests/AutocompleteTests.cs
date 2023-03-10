using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Client;
using EMQ.Shared.Core;
using NUnit.Framework;

namespace Tests;

public class AutocompleteTests
{
    [SetUp]
    public void Setup()
    {
    }

    private string[] Data { get; set; } = Array.Empty<string>();

    [Test, Explicit]
    public async Task Test_A()
    {
        Data = new string[] { "abc", "t", "tes", "test", "z test", "9 TEST", "", "testing" };
        string search = "test";

        List<string> expected = new List<string>()
        {
            "test",
            "9 TEST a",
            "z test a",
            "testing",
        };

        var actual = Autocomplete.SearchAutocompleteMst(Data, search);

        Assert.AreEqual(JsonSerializer.Serialize(expected, Utils.Jso), JsonSerializer.Serialize(actual, Utils.Jso));
    }
}
