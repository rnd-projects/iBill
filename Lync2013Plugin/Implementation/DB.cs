﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Reflection;
using LyncBillingBase.DataModels;
//using CCC.ORM.DataAttributes;

namespace Lync2013Plugin.Implementation
{
    public static class Db
    {
        //Define what attributes to be read from the class
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance;

        public static Func<IDataRecord, PhoneCall> PhoneCallsSelector = record => new PhoneCall
        {
            PhoneCallsTableName = "PhoneCalls2013",
            SessionIdTime = record.GetDateTime(record.GetOrdinal("SessionIdTime")),
            SessionIdSeq = record.GetInt32(record.GetOrdinal("SessionIdSeq")),
            ResponseTime =
                (record["ResponseTime"] == DBNull.Value)
                    ? DateTime.MinValue
                    : record.GetDateTime(record.GetOrdinal("ResponseTime")),
            SessionEndTime =
                (record["SessionEndTime"] == DBNull.Value)
                    ? DateTime.MinValue
                    : record.GetDateTime(record.GetOrdinal("SessionEndTime")),
            Duration =
                (record["Duration"] == DBNull.Value)
                    ? Convert.ToDecimal(0)
                    : record.GetDecimal(record.GetOrdinal("Duration")),
            SourceUserUri =
                (record["SourceUserUri"] == DBNull.Value)
                    ? string.Empty
                    : record.GetString(record.GetOrdinal("SourceUserUri")),
            DestinationUserUri =
                (record["DestinationUserUri"] == DBNull.Value)
                    ? string.Empty
                    : record.GetString(record.GetOrdinal("DestinationUserUri")),
            SourceNumberUri =
                (record["SourceNumberUri"] == DBNull.Value)
                    ? string.Empty
                    : record.GetString(record.GetOrdinal("SourceNumberUri")),
            DestinationNumberUri =
                (record["DestinationNumberUri"] == DBNull.Value)
                    ? string.Empty
                    : record.GetString(record.GetOrdinal("DestinationNumberUri")),
            FromMediationServer =
                (record["FromMediationServer"] == DBNull.Value)
                    ? string.Empty
                    : record.GetString(record.GetOrdinal("FromMediationServer")),
            ToMediationServer =
                (record["ToMediationServer"] == DBNull.Value)
                    ? string.Empty
                    : record.GetString(record.GetOrdinal("ToMediationServer")),
            FromGateway =
                (record["FromGateway"] == DBNull.Value)
                    ? string.Empty
                    : record.GetString(record.GetOrdinal("FromGateway")),
            ToGateway =
                (record["ToGateway"] == DBNull.Value) ? string.Empty : record.GetString(record.GetOrdinal("ToGateway")),
            SourceUserEdgeServer =
                (record["SourceUserEdgeServer"] == DBNull.Value)
                    ? string.Empty
                    : record.GetString(record.GetOrdinal("SourceUserEdgeServer")),
            DestinationUserEdgeServer =
                (record["DestinationUserEdgeServer"] == DBNull.Value)
                    ? string.Empty
                    : record.GetString(record.GetOrdinal("DestinationUserEdgeServer")),
            ServerFqdn =
                (record["ServerFQDN"] == DBNull.Value)
                    ? string.Empty
                    : record.GetString(record.GetOrdinal("ServerFQDN")),
            PoolFqdn =
                (record["PoolFQDN"] == DBNull.Value) ? string.Empty : record.GetString(record.GetOrdinal("PoolFQDN")),
            ReferredBy =
                (record["ReferredBy"] == DBNull.Value)
                    ? string.Empty
                    : record.GetString(record.GetOrdinal("ReferredBy")),
            OnBehalf =
                (record["OnBehalf"] == DBNull.Value) ? string.Empty : record.GetString(record.GetOrdinal("OnBehalf")),
            CalleeUri =
                (record["CalleeURI"] == DBNull.Value) ? string.Empty : record.GetString(record.GetOrdinal("CalleeURI"))
        };

        private static string NormalizeConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                return null;
            }
            return connectionString.Replace(@"Provider=SQLOLEDB.1;", "");
        }

        //public static void BulkInsert(this List<PhoneCall> source, string tableName, string databaseConnectionString)
        //{
        //    List<PropertyInfo> masterPropertyInfoFields = new List<PropertyInfo>();

        //    //SQL Bulk Copy only works with sql connection so we need to remove the provider from the connection string
        //    //using (var bcp = new SqlBulkCopy(DBLib.ConnectionString_Lync.Replace(@"Provider=SQLOLEDB.1;",""))) 
        //    using (var bcp = new SqlBulkCopy(NormalizeConnectionString(databaseConnectionString)))
        //    {
        //        var AllProperties =  source.GetType().GetGenericArguments().Single().GetProperties(flags).
        //            Where(property => property.GetCustomAttribute<DbColumnAttribute>() != null).
        //            Cast<PropertyInfo>().Select(item => item.GetCustomAttribute<DbColumnAttribute>().Name).ToArray();

        //        masterPropertyInfoFields = source.GetType().GetGenericArguments().Single().GetProperties(flags)
        //       .Where(property => 
        //            property.GetCustomAttribute<DbColumnAttribute>() != null &&  
        //            property.Name != "PhoneCallsTableName" && 
        //            property.Name != "PhoneBookName" && 
        //            property.Name != "PhoneCallsTable") 
        //       .Cast<PropertyInfo>()
        //       .ToList();

        //        using (var reader = ObjectReader.Create<PhoneCall>(source, AllProperties))
        //        {
        //            bcp.DestinationTableName = tableName;

        //            foreach (var item in masterPropertyInfoFields)
        //            {   
        //                bcp.ColumnMappings.Add(new SqlBulkCopyColumnMapping(item.Name, item.GetCustomAttribute<DbColumnAttribute>().Name));
        //            }

        //            bcp.WriteToServer(reader);
        //        }
        //    }
        //}


        public static void BulkInsert(this DataTable dt, string tableName, string databaseConnectionString)
        {
            try
            {
                using (var bcp = new SqlBulkCopy(NormalizeConnectionString(databaseConnectionString),
                    SqlBulkCopyOptions.TableLock | SqlBulkCopyOptions.KeepNulls | SqlBulkCopyOptions.KeepIdentity))
                {
                    foreach (DataColumn dc in dt.Columns)
                    {
                        bcp.ColumnMappings.Add(new SqlBulkCopyColumnMapping(dc.ColumnName, dc.ColumnName));
                    }

                    bcp.BulkCopyTimeout = 120;
                    bcp.BatchSize = 1000;
                    bcp.DestinationTableName = tableName;
                    bcp.WriteToServer(dt.Select());
                }
            }
            catch (Exception e)
            {
                throw e.InnerException;
            }
        }

        public static IEnumerable<T> ReadSqlData<T>(OleDbDataReader reader, Func<IDataRecord, T> selector)
        {
            while (reader.Read())
            {
                yield return selector(reader);
            }

            reader.CloseDataReader();
        }

        public static void CloseDataReader(this OleDbDataReader dataReader)
        {
            if (dataReader.IsClosed == false)
            {
                dataReader.Close();
                dataReader.Dispose();
            }
        }
    }
}