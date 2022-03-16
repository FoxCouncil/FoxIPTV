// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Library
{
    using Newtonsoft.Json.Linq;
    using System;
    using System.Linq;

    public class Account
    {
        public Guid Id { get; set; }

        public Guid ProviderId { get; set; }

        public JObject Data { get; set; }

        public Account(Guid providerId, JObject data)
        {
            Id = Guid.NewGuid();

            ProviderId = providerId;
            Data = data;
        }

        public Result CheckAuthentication()
        {
            var service = Core.Services.FirstOrDefault(x => x.Id == ProviderId);

            if (service == null)
            {
                return new Result(false, $"No provider defined with GUID {ProviderId}");
            }

            return Result.Success();
        }
    }
}
