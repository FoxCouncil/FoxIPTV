using System;
using System.Collections.Generic;
using System.Text;

namespace FoxIPTV.Library.Services
{
    public class ServiceField
    {
        public string Key { get; private set; }

        public Type Type { get; private set; }

        public string Header { get; private set; }

        public string Placeholder { get; private set; }

        public ServiceField(string key, Type baseType, string header = "", string placeholder = "")
        {
            Key = key;

            Type = baseType;

            Header = header;

            Placeholder = placeholder;
        }
    }
}
