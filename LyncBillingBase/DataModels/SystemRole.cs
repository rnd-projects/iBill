﻿using CCC.ORM;
using CCC.ORM.DataAccess;
using CCC.ORM.DataAttributes;

namespace LyncBillingBase.DataModels
{
    [DataSource(Name = "Roles_System", Type = Globals.DataSource.Type.DbTable,
        AccessMethod = Globals.DataSource.AccessMethod.SingleSource)]
    public class SystemRole : DataModel
    {
        [IsIdField]
        [DbColumn("ID")]
        public int Id { set; get; }

        [DbColumn("SipAccount")]
        public string SipAccount { get; set; }

        [DbColumn("RoleID")]
        public int RoleId { get; set; }

        [IsForeignKey]
        [DbColumn("SiteID")]
        public int SiteId { get; set; }

        [AllowNull]
        [DbColumn("Description")]
        public string Description { get; set; }

        //
        // Relations
        [DataRelation(WithDataModel = typeof (Site), OnDataModelKey = "Id", ThisKey = "SiteId")]
        public Site Site { get; set; }

        [DataRelation(WithDataModel = typeof (User), OnDataModelKey = "SipAccount", ThisKey = "SipAccount")]
        public User User { get; set; }
    }
}