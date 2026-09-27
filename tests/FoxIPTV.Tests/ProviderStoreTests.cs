// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Tests
{
    using System;
    using FoxIPTV.Classes;
    using Newtonsoft.Json.Linq;

    public class ProviderStoreTests
    {
        [Fact]
        public void SaveLoadDelete_RoundTrips()
        {
            var id = "unit-test-" + Guid.NewGuid().ToString("N");

            try
            {
                Assert.Null(ProviderStore.Load(id));

                ProviderStore.Save(id, new JObject { ["Username"] = "fox", ["Password"] = "hunter2" });

                var loaded = ProviderStore.Load(id);

                Assert.Equal("fox", loaded["Username"]?.ToString());
                Assert.Equal("hunter2", loaded["Password"]?.ToString());
            }
            finally
            {
                ProviderStore.Delete(id);
            }

            Assert.Null(ProviderStore.Load(id));
        }
    }
}
