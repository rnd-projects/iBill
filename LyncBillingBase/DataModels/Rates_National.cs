﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ORM;
using ORM.DataAccess;
using ORM.DataAttributes;

namespace LyncBillingBase.DataModels
{
    [DataSource(Name = "GatewaysRates", Type = GLOBALS.DataSource.Type.DBTable, AccessMethod = GLOBALS.DataSource.AccessMethod.DistributedSource)]
    public class Rates_National : DataModel
    {
        [IsIDField]
        [DbColumn("Rate_ID")]
        public Int64 RateID { get; set; }

        [DbColumn("ISO3CountryCode")]
        public string CountryCode { get; set; }

        [DbColumn("CountryName")]
        public string CountryName { get; set; }

        [DbColumn("DialingCode")]
        public Int64 DialingCode { get; set; }

        [DbColumn("TypeOfService")]
        public string TypeOfService { get; set; }

        [DbColumn("Rate")]
        public decimal Rate { get; set; }
    }
}
