using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JurisUtilityBase
{
    public class ErrorLog
    {
        public ErrorLog()
        {
            client = "";
            matter = "";
            message = "";

        }
        public string client { get; set; }
        public string matter { get; set; }

        public string message { get; set; }

    }
}
