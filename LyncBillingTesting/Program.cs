﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Linq.Expressions;

using LyncBillingBase.DataAccess;
using LyncBillingBase.DataModels;
using LyncBillingBase.DataMappers;
using LyncBillingBase.Helpers;
using LyncBillingBase.Repository;



namespace LyncBillingTesting
{
    class Program
    {
        public  static string tolower(string text)
        {
            return text.ToLower();
        }

        public static void Main(string[] args)
        {
            //var _dbStorage = DataStorage.Instance;

            var AnnouncemenetsRepository = new AnnouncementsDataMapper();

            var announcementsForRole = AnnouncemenetsRepository.GetAnnouncementsForRole(2);
            var announcementsForSite = AnnouncemenetsRepository.GetAnnouncementsForSite(1);
        }
    }
}
