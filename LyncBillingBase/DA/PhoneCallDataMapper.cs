﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LyncBillingBase.DAL;

namespace LyncBillingBase.DA
{
    public class PhoneCallDataMapper
    {
        DistributedDataAccess<PhoneCall> PhoneCallsAccessor = new DistributedDataAccess<PhoneCall>();

        public override List<string> GetListOfTables()
        {
            List<string> tables = new List<string>();

            tables.Add("PhoneCalls2010");
            tables.Add("PhoneCalls2013");
            tables.Add("PhoneCalls2015");

            return tables;
        }
    }
}
