﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Linq;
using System.Text;

namespace CCC.UTILS.Libs
{
    public class DbLib
    {
        //Load DLL Configs
        public static LoadConfigs Config = new LoadConfigs();

        public DbLib()
        {
            var connectionStringsSection = Config.DllConfig.ConnectionStrings;

            if (connectionStringsSection != null && connectionStringsSection.ConnectionStrings.Count > 0)
            {
                ConnectionString = connectionStringsSection.ConnectionStrings[0].ConnectionString;
            }
            else
            {
                throw new Exception("No connection string was found in the DLL Config File.");
            }
        }

        public DbLib(string connectionString)
        {
            ConnectionString = connectionString;
        }

        public static string ConnectionString { get; set; }

        private OleDbConnection DbInitializeConnection(string connectionString)
        {
            return new OleDbConnection(connectionString);
        }

        /// <summary>
        ///     Given a table name and a list of it's column names, return a list of column names in the following format:
        ///     TableName#ColumnName.
        ///     Example:
        ///     * TableName: "Users"
        ///     * Columns: ["UserName", "UserEmail"]
        ///     * Returned As: ["Users#UserName", "Users#UserEmail"]
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="columns"></param>
        /// <returns></returns>
        private static List<string> ConstructUniqueTableColumnsNames(string tableName, List<string> columns)
        {
            var separator = ".";
            var formattedColumnsNames = new List<string>();

            if (!string.IsNullOrEmpty(tableName) && columns != null && columns.Count() > 0)
            {
                formattedColumnsNames = columns
                    .Select(
                        column =>
                            String.Format("[{0}].[{1}] as '{2}{3}{4}'", tableName, column, tableName, separator, column)
                    ).ToList();
            }

            return formattedColumnsNames;
        }

        //SELECT with SQL query
        public DataTable Selectfromsql(string sqlQuery, string customConnectionString = null)
        {
            var dt = new DataTable();
            OleDbDataReader dr;
            OleDbConnection conn;

            //Initialize connections
            if (!string.IsNullOrEmpty(customConnectionString))
                conn = DbInitializeConnection(customConnectionString);
            else
                conn = DbInitializeConnection(ConnectionString);

            //Execute SQL Query
            var comm = new OleDbCommand(sqlQuery, conn);


            try
            {
                conn.Open();
                dr = comm.ExecuteReader();
                dt.Load(dr);
            }
            catch (Exception ex)
            {
                var argEx = new ArgumentException("Exception", "ex", ex);
                throw argEx;
            }
            finally
            {
                conn.Close();
            }

            return dt;
        }

        //SELECT FROM a table and JOIN with other tables
        public DataTable SELECT_WITH_JOIN(string tableName, List<string> masterTableColumns,
            Dictionary<string, object> whereConditions, List<SqlJoinRelation> tableRelationsMap, int limits)
        {
            var dt = new DataTable();

            OleDbDataReader dr;
            var finalSelectQuery = string.Empty;
            var truthOperator = "AND";

            var selectColumns = new StringBuilder("");
            var whereStatement = new StringBuilder("");
            var joinStatement = new StringBuilder("");
            var groupByFields = new StringBuilder("");
            var orderBy = new StringBuilder("");


            //Handle Order By
            if (tableName.Contains("Phonecalls"))
            {
                orderBy.Append("ORDER BY [SessionIdTime] DESC");
            }


            //Handle the JOIN statement
            if (tableRelationsMap != null && tableRelationsMap.Count > 0)
            {
                foreach (var relation in tableRelationsMap)
                {
                    //The two parts of the JOIN STATEMENT
                    var joinType = string.Empty;
                    var keysStatement = string.Empty;

                    //Decide the join type
                    if (Globals.DataRelation.Type.Intersection == relation.RelationType)
                    {
                        joinType = "INNER JOIN";
                    }
                    else
                    {
                        //if(relationType == Enums.DataRelationType.UNION.ToString())
                        joinType = "LEFT OUTER JOIN";
                    }

                    //Construct the JOIN KEYS statement 
                    keysStatement = String.Format(" AS {4} ON [{0}].[{1}] = [{4}].[{3}]", tableName,
                        relation.MasterTableKey, relation.JoinedTableName, relation.JoinedTableKey,
                        relation.RelationName);

                    foreach (var column in relation.JoinedTableColumns)
                    {
                        selectColumns.Append(string.Format("{0}.{1} AS '{0}.{1}',", relation.RelationName, column));
                    }

                    joinStatement.Append(String.Format("{0} {1} {2} ", joinType, relation.JoinedTableName, keysStatement));
                } //end-foreach
            } //end-outer-if


            //Concatenate the Master Table Columns with the local list
            if (masterTableColumns == null)
            {
                masterTableColumns = new List<string>();
            }
            masterTableColumns =
                masterTableColumns.Select(col => String.Format("[{0}].[{1}]", tableName, col, tableName, col)).ToList();
            //Columns = masterTableColumns.Concat(Columns).ToList();


            //Handle the fields collection
            if (masterTableColumns.Count > 0)
            {
                foreach (var field in masterTableColumns)
                {
                    //selectedfields.Append(fieldName + ",");
                    if (!string.IsNullOrEmpty(field))
                    {
                        selectColumns.Append(field + ",");
                    }
                }

                selectColumns.Remove(selectColumns.Length - 1, 1);
            }
            else
            {
                selectColumns.Append("*");
            }


            //Handle the whereClause collection
            if (whereConditions != null && whereConditions.Count != 0)
            {
                whereStatement.Append("WHERE ");

                foreach (var pair in whereConditions)
                {
                    var key = pair.Key;

                    //If the key doesn't contain the table separator ("."), then add the master table name and the table separator.
                    if (!key.Contains("."))
                    {
                        key = String.Format("[{0}].[{1}]", tableName, key);
                    }


                    if (pair.Value == null)
                    {
                        whereStatement.Append(String.Format("{0} IS NULL {1} ", key, truthOperator));
                    }

                    else if (pair.Value.ToString() == "!null")
                    {
                        whereStatement.Append(String.Format("{0} IS NOT NULL {1} ", key, truthOperator));
                    }

                    else if (pair.Value.ToString() == "!=0")
                    {
                        whereStatement.Append(String.Format("{0} <> 0 {1} ", key, truthOperator));
                    }

                    else if (pair.Value is string && pair.Value.ToString().ToLower().Contains("like"))
                    {
                        //key like value: key = "columnX", value = "like '%ABC%'"
                        whereStatement.Append(String.Format("{0} {1} {2} ", key, pair.Value, truthOperator));
                    }

                    else if (pair.Value is string && (pair.Value.ToString()).Contains("BETWEEN"))
                    {
                        //key like value: key = "columnX", value = "BETWEEN abc AND xyz"
                        whereStatement.Append(String.Format("{0} {1} {2} ", key, pair.Value, truthOperator));
                    }

                    else if (pair.Value is List<int>)
                    {
                        whereStatement.Append(key + " in ( ");

                        foreach (var item in (List<int>) pair.Value)
                        {
                            whereStatement.Append(item + ",");
                        }
                        //Remove last ','
                        whereStatement.Remove(whereStatement.Length - 1, 1);

                        whereStatement.Append(" ) " + truthOperator + " ");
                    }

                    else if (pair.Value is List<string>)
                    {
                        whereStatement.Append(key + " in ( ");

                        foreach (var item in (List<string>) pair.Value)
                        {
                            whereStatement.Append(item + ",");
                        }
                        //Remove last ','
                        whereStatement.Remove(whereStatement.Length - 1, 1);

                        whereStatement.Append(" ) " + truthOperator + " ");
                    }

                    else
                    {
                        var valueType = pair.Value.GetType();
                        if (valueType == typeof (int) || valueType == typeof (Double))
                        {
                            whereStatement.Append(String.Format("{0}={1} {2} ", key, pair.Value, truthOperator));
                        }
                        else
                        {
                            whereStatement.Append(String.Format("{0}='{1}' {2} ", key, pair.Value, truthOperator));
                        }
                    }
                }

                //Trim the whereStatement
                whereStatement.Remove(whereStatement.Length - 5, 5);
            }


            //Start constructing the FINAL SELECT QUERY
            //Start by formatting the select columns and limits
            if (limits == 0)
                finalSelectQuery = string.Format("SELECT {0}", selectColumns);
            else
                finalSelectQuery = string.Format("SELECT TOP({0}) {1}", limits, selectColumns);

            //Add the JOINED tables part
            if (joinStatement.ToString().Length > 0)
                finalSelectQuery = String.Format("{0} FROM [{1}] {2}", finalSelectQuery, tableName, joinStatement);
            else
                finalSelectQuery = String.Format("{0} FROM [{1}]", finalSelectQuery, tableName);

            //Add the where conditions to the FINAL SELECT QUERY
            if (whereStatement.ToString().Length > 0)
                finalSelectQuery = String.Format("{0} {1}", finalSelectQuery, whereStatement);

            //Add the order by part
            if (orderBy.ToString().Length > 0)
                finalSelectQuery = String.Format("{0} {1}", finalSelectQuery, orderBy);


            //Initialize the connection and command
            var conn = DbInitializeConnection(ConnectionString);
            var comm = new OleDbCommand(finalSelectQuery, conn);

            try
            {
                conn.Open();
                dr = comm.ExecuteReader();

                dt.Load(dr);
            }
            catch (Exception ex)
            {
                var argEx = new ArgumentException("Exception", "ex", ex);
                throw argEx;
            }
            finally
            {
                conn.Close();
            }

            return dt;
        }

        public DataTable SELECT(string tableName, string wherePart, string customConnectionString = null)
        {
            var dt = new DataTable();
            OleDbDataReader dr;
            OleDbConnection conn;

            //Initialize connections
            if (!string.IsNullOrEmpty(customConnectionString))
                conn = DbInitializeConnection(customConnectionString);
            else
                conn = DbInitializeConnection(ConnectionString);

            var sqlQuery = string.Format("SELECT * FROM {0} WHERE {1}", tableName, wherePart);

            //Execute SQL Query
            var comm = new OleDbCommand(sqlQuery, conn);


            try
            {
                conn.Open();
                dr = comm.ExecuteReader();
                dt.Load(dr);
            }
            catch (Exception ex)
            {
                var argEx = new ArgumentException("Exception", "ex", ex);
                throw argEx;
            }
            finally
            {
                conn.Close();
            }

            return dt;
        }

        /// <summary>
        ///     Construct Generic Select Statemnet
        /// </summary>
        /// <param name="tableName">DB Table Name</param>
        /// <param name="whereField">Where statemnet Field</param>
        /// <param name="whereValue">Where statemnet Value</param>
        /// <returns> DataTable Object</returns>
        /// Obsolete, use the function:
        /// ---> SELECT(string tableName, List
        /// <string> fieldsList, Dictionary<string, object> whereClause, int limits, bool setWhereStatementOperatorToOR = false)
        [Obsolete]
        public DataTable SELECT(string tableName, string whereField, object whereValue)
        {
            var dt = new DataTable();
            OleDbDataReader dr;
            var selectQuery = string.Empty;

            var selectedfields = new StringBuilder();

            if (whereValue.GetType().Equals(typeof (int)) ||
                whereValue.GetType().Equals(typeof (double)) ||
                whereValue.GetType().Equals(typeof (decimal)) ||
                whereValue.GetType().Equals(typeof (Int32)) ||
                whereValue.GetType().Equals(typeof (Int64)))
                selectQuery = string.Format("SELECT * FROM  [{0}] WHERE [{1}]={2}", tableName, whereField, whereValue);
            else
                selectQuery = string.Format("SELECT * FROM  [{0}] WHERE [{1}]='{2}'", tableName, whereField, whereValue);

            var conn = DbInitializeConnection(ConnectionString);
            var comm = new OleDbCommand(selectQuery, conn);

            try
            {
                conn.Open();
                dr = comm.ExecuteReader();
                dt.Load(dr);
            }
            catch (Exception ex)
            {
                var argEx = new ArgumentException("Exception", "ex", ex);
                throw argEx;
            }
            finally
            {
                conn.Close();
            }

            return dt;
        }

        public DataTable SELECT(string tableName)
        {
            var dt = new DataTable();
            OleDbDataReader dr;
            var selectQuery = string.Empty;

            var selectedfields = new StringBuilder();

            selectQuery = string.Format("SELECT * FROM  [{0}]", tableName);

            var conn = DbInitializeConnection(ConnectionString);
            var comm = new OleDbCommand(selectQuery, conn);

            try
            {
                conn.Open();
                dr = comm.ExecuteReader();
                dt.Load(dr);
            }
            catch (Exception ex)
            {
                var argEx = new ArgumentException("Exception", "ex", ex);
                throw argEx;
            }
            finally
            {
                conn.Close();
            }

            return dt;
        }

        /// <summary>
        ///     Construct Generic Select Statemnet
        /// </summary>
        /// <param name="tableName">DB Table Name</param>
        /// <param name="fields">List of Fields to be fetched from Database</param>
        /// <param name="whereClause">
        ///     A dictionary which holds the fields and its related values to be able to construct Where Statemnet
        ///     1. Null if there is no condition
        ///     2. Dictionary holds fields names and values if there is a condition
        /// </param>
        /// <param name="limits">
        ///     Holds how many rows to be fetched from the database table.
        ///     1. 0 if all Rows
        ///     2. Value for number of rows
        /// </param>
        /// <returns>DataTable Object</returns>
        public DataTable SELECT(string tableName, List<string> fields, Dictionary<string, object> whereClause,
            int limits)
        {
            var dt = new DataTable();
            string selectQuery;

            var selectedfields = new StringBuilder();
            var whereStatement = new StringBuilder();
            var orderBy = new StringBuilder();


            //table name
            if (tableName == "PhoneCalls")
            {
                orderBy.Append("ORDER BY [SessionIdTime] DESC");
            }
            else
            {
                orderBy.Append("");
            }


            //fields
            if (fields != null)
            {
                if (fields.Count != 0)
                {
                    foreach (var fieldName in fields)
                    {
                        selectedfields.Append(fieldName + ",");
                    }
                    selectedfields.Remove(selectedfields.Length - 1, 1);
                }
                else
                    selectedfields.Append("*");
            }
            else
            {
                selectedfields.Append("*");
            }


            //whereClause
            if (whereClause != null && whereClause.Count != 0)
            {
                whereStatement.Append("WHERE ");

                foreach (var pair in whereClause)
                {
                    if (pair.Value == null)
                    {
                        whereStatement.Append("[" + pair.Key + "] IS NULL" + " AND ");
                    }
                    else if (pair.Value.ToString() == "!null")
                    {
                        whereStatement.Append("[" + pair.Key + "] IS NOT NULL" + " AND ");
                    }
                    else if (pair.Value.ToString() == "!=0")
                    {
                        whereStatement.Append("[" + pair.Key + "] <> 0 " + " AND ");
                    }
                    else if (pair.Value is string && pair.Value.ToString().ToLower().Contains("like"))
                    {
                        whereStatement.Append("[" + pair.Key + "] " + pair.Value + " AND ");
                    }
                    else if (pair.Value is string && (pair.Value.ToString()).Contains("BETWEEN"))
                    {
                        whereStatement.Append("[" + pair.Key + "]  " + pair.Value);

                        whereStatement.Append(" AND ");
                    }
                    else
                    {
                        var valueType = pair.Value.GetType();

                        if (valueType == typeof(int) || valueType == typeof(Double))
                        {
                            whereStatement.Append("[" + pair.Key + "]=" + pair.Value + " AND ");
                        }
                        else
                        {
                            whereStatement.Append("[" + pair.Key + "]='" + pair.Value + "' COLLATE SQL_Latin1_General_CP1_CI_AS AND ");
                        }
                    }
                }

                whereStatement.Remove(whereStatement.Length - 5, 5);
            }


            if (limits == 0)
            {
                if (whereClause != null)
                    selectQuery = string.Format("SELECT {0} FROM  [{1}] {2} {3}", selectedfields, tableName,
                        whereStatement, orderBy);
                else
                    selectQuery = string.Format("SELECT {0} FROM  [{1}] {2}", selectedfields, tableName, orderBy);
            }
            else
            {
                if (whereClause != null)
                    selectQuery = string.Format("SELECT TOP({0}) {1} FROM [{2}] {3} {4}", limits, selectedfields, tableName, whereStatement, orderBy);
                else
                    selectQuery = string.Format("SELECT TOP({0}) {1} FROM [{2}] {3}", limits, selectedfields, tableName, orderBy);
            }

            var conn = DbInitializeConnection(ConnectionString);
            var comm = new OleDbCommand(selectQuery, conn);

            try
            {
                conn.Open();
                var dr = comm.ExecuteReader();

                dt.Load(dr);
            }
            catch (Exception ex)
            {
                var argEx = new ArgumentException("Exception", "ex", ex);
                throw argEx;
            }
            finally
            {
                conn.Close();
            }

            return dt;
        }

        /// <summary>
        ///     Construct Generic Select Statemnet
        /// </summary>
        /// <param name="tableName">DB Table Name</param>
        /// <param name="fieldsList">List of Fields to be fetched from Database</param>
        /// <param name="whereClause">
        ///     A dictionary which holds the fields and its related values to be able to construct Where Statemnet
        ///     1. Null if there is no condition
        ///     2. Dictionary holds fields names and values if there is a condition
        /// </param>
        /// <param name="limits">
        ///     Holds how many rows to be fetched from the database table.
        ///     1. 0 if all Rows
        ///     2. Value for number of rows
        /// </param>
        /// <param name="setWhereStatementOperatorToOr">
        ///     The default operator in the where statement is "AND", if you set this one
        ///     to true, the operator will be turned to "OR" in the where statement
        /// </param>
        /// <returns>DataTable Object</returns>
        public DataTable SELECT(string tableName, List<string> fieldsList, Dictionary<string, object> whereClause,
            int limits, bool setWhereStatementOperatorToOr = false)
        {
            var dt = new DataTable();
            OleDbDataReader dr;
            var selectQuery = string.Empty;
            var OPERATOR = setWhereStatementOperatorToOr ? " OR " : " AND ";

            var selectedfields = new StringBuilder();
            var whereStatement = new StringBuilder();
            var orderBy = new StringBuilder();


            //Handle tableName
            if (tableName.Contains("Phonecalls"))
            {
                orderBy.Append("ORDER BY [SessionIdTime] DESC");
            }
            else
            {
                orderBy.Append("");
            }


            //Handle the fields collection
            if (fieldsList != null)
            {
                if (fieldsList.Count != 0)
                {
                    foreach (var field in fieldsList)
                    {
                        //selectedfields.Append(fieldName + ",");
                        if (!string.IsNullOrEmpty(field))
                        {
                            if (field.Contains("COUNT") || field.Contains("SUM") || field.Contains("YEAR") ||
                                field.Contains("MONTH") || field.Contains("DISTINCT"))
                                selectedfields.Append(field + ",");
                            else
                                selectedfields.Append("[" + field + "],");
                        }
                    }
                    selectedfields.Remove(selectedfields.Length - 1, 1);
                }
                else
                    selectedfields.Append("*");
            }
            else
            {
                selectedfields.Append("*");
            }


            //Handle the whereClause collection
            if (whereClause != null && whereClause.Count != 0)
            {
                whereStatement.Append("WHERE ");

                foreach (var pair in whereClause)
                {
                    if (pair.Value == null)
                    {
                        whereStatement.Append("[" + pair.Key + "] IS NULL " + OPERATOR);
                    }

                    else if (pair.Value.ToString() == "!null")
                    {
                        whereStatement.Append("[" + pair.Key + "] IS NOT NULL " + OPERATOR);
                    }

                    else if (pair.Value.ToString() == "!=0")
                    {
                        whereStatement.Append("[" + pair.Key + "] <> 0 " + OPERATOR);
                    }

                    else if (pair.Value is string && pair.Value.ToString().ToLower().Contains("like"))
                    {
                        whereStatement.Append("[" + pair.Key + "] " + pair.Value + OPERATOR);
                    }

                    else if (pair.Value is string && (pair.Value.ToString()).Contains("BETWEEN"))
                    {
                        whereStatement.Append("[" + pair.Key + "]  " + pair.Value);

                        whereStatement.Append(" AND ");
                    }

                    else if (pair.Value is List<int>)
                    {
                        whereStatement.Append("[" + pair.Key + "] in ( ");

                        foreach (var item in (List<int>) pair.Value)
                        {
                            whereStatement.Append(item + ",");
                        }
                        //Remove last ','
                        whereStatement.Remove(whereStatement.Length - 1, 1);

                        whereStatement.Append(" ) " + OPERATOR);
                    }

                    else if (pair.Value is List<string>)
                    {
                        whereStatement.Append("[" + pair.Key + "] in ( ");

                        foreach (var item in (List<string>) pair.Value)
                        {
                            whereStatement.Append(item + ",");
                        }
                        //Remove last ','
                        whereStatement.Remove(whereStatement.Length - 1, 1);

                        whereStatement.Append(" ) " + OPERATOR);
                    }

                    else
                    {
                        var valueType = pair.Value.GetType();
                        if (valueType == typeof (int) || valueType == typeof (Double))
                        {
                            whereStatement.Append("[" + pair.Key + "]=" + pair.Value + OPERATOR);
                        }
                        else
                        {
                            whereStatement.Append("[" + pair.Key + "]='" + pair.Value + "'" + OPERATOR);
                        }
                    }
                }

                //Trim the whereStatement
                if (setWhereStatementOperatorToOr)
                    whereStatement.Remove(whereStatement.Length - 4, 4);
                else
                    whereStatement.Remove(whereStatement.Length - 5, 5);
            }

            if (limits == 0)
            {
                if (whereClause != null)
                    selectQuery = string.Format("SELECT {0} FROM  [{1}] {2} {3}", selectedfields, tableName,
                        whereStatement, orderBy);
                else
                    selectQuery = string.Format("SELECT {0} FROM  [{1}] {2}", selectedfields, tableName, orderBy);
            }
            else
            {
                if (whereClause != null)
                    selectQuery = string.Format("SELECT TOP({0}) {1} FROM [{2}] {3} {4}", limits, selectedfields,
                        tableName, whereStatement, orderBy);
                else
                    selectQuery = string.Format("SELECT TOP({0}) {1} FROM [{2}] {3}", limits, selectedfields, tableName,
                        orderBy);
            }

            var conn = DbInitializeConnection(ConnectionString);
            var comm = new OleDbCommand(selectQuery, conn);

            try
            {
                conn.Open();
                dr = comm.ExecuteReader();

                dt.Load(dr);
            }
            catch (Exception ex)
            {
                var argEx = new ArgumentException("Exception", "ex", ex);
                throw argEx;
            }
            finally
            {
                conn.Close();
            }

            return dt;
        }

        public DataTable SELECT_FROM_FUNCTION(string tableName, List<object> functionParams,
            Dictionary<string, object> whereClause)
        {
            var dt = new DataTable();

            OleDbDataReader dr;
            var selectQuery = string.Empty;

            var parameters = new StringBuilder();
            var whereStatement = new StringBuilder();

            if (functionParams != null && functionParams.Count != 0)
            {
                foreach (var obj in functionParams)
                {
                    var valueType = obj.GetType();

                    if (valueType == typeof (string) || valueType == typeof (DateTime))
                        parameters.Append("'" + obj + "',");
                    else
                        parameters.Append(obj + ",");
                }
                parameters.Remove(parameters.Length - 1, 1);
            }

            if (whereClause != null && whereClause.Count != 0)
            {
                whereStatement.Append("WHERE ");
                foreach (var pair in whereClause)
                {
                    var valueType = pair.Value.GetType();

                    if (valueType == typeof (int) || valueType == typeof (Double))
                        whereStatement.Append("[" + pair.Key + "]=" + pair.Value + " AND ");
                    else
                        whereStatement.Append("[" + pair.Key + "]='" + pair.Value + "' AND ");
                }
                whereStatement.Remove(whereStatement.Length - 5, 5);
            }

            if (whereClause != null && whereClause.Count != 0)
                selectQuery = string.Format("SELECT * FROM [{0}] ({1}) WHERE {2}", tableName, parameters, whereStatement);
            else
                selectQuery = string.Format("SELECT * FROM [{0}] ({1})", tableName, parameters);


            var conn = DbInitializeConnection(ConnectionString);
            var comm = new OleDbCommand(selectQuery, conn);

            try
            {
                conn.Open();
                dr = comm.ExecuteReader();

                dt.Load(dr);
            }
            catch (Exception ex)
            {
                var argEx = new ArgumentException("Exception", "ex", ex);
                throw argEx;
            }
            finally
            {
                conn.Close();
            }

            return dt;
        }

        public DataTable SELECT_FROM_FUNCTION(string databaseFunctionName, List<object> functionParams,
            Dictionary<string, object> whereClause, List<string> selectColumnsList = null,
            List<string> groupByColumnsList = null)
        {
            var dt = new DataTable();

            OleDbDataReader dr;
            var finalSelectQuery = string.Empty;

            var parameters = new StringBuilder();
            var whereStatement = new StringBuilder();
            var selectColumns = new StringBuilder();
            var groupByFields = new StringBuilder();

            if (functionParams != null && functionParams.Count != 0)
            {
                foreach (var obj in functionParams)
                {
                    var valueType = obj.GetType();

                    if (valueType == typeof (string) || valueType == typeof (DateTime))
                        parameters.Append("'" + obj + "',");
                    else
                        parameters.Append(obj + ",");
                }
                parameters.Remove(parameters.Length - 1, 1);
            }

            if (selectColumnsList != null && selectColumnsList.Count != 0)
            {
                foreach (var field in selectColumnsList)
                {
                    if (!string.IsNullOrEmpty(field))
                    {
                        if (field.Contains("COUNT") || field.Contains("SUM") || field.Contains("YEAR") ||
                            field.Contains("MONTH") || field.Contains("DISTINCT"))
                            selectColumns.Append(field + ",");
                        else
                            selectColumns.Append("[" + field + "],");
                    }
                }
                selectColumns.Remove(selectColumns.Length - 1, 1);
            }

            if (groupByColumnsList != null && groupByColumnsList.Count != 0)
            {
                foreach (var field in groupByColumnsList)
                {
                    if (!string.IsNullOrEmpty(field))
                    {
                        groupByFields.Append("[" + field + "],");
                    }
                }
                groupByFields.Remove(groupByFields.Length - 1, 1);
            }

            if (whereClause != null && whereClause.Count != 0)
            {
                foreach (var pair in whereClause)
                {
                    if (pair.Value == null)
                    {
                        whereStatement.Append("[" + pair.Key + "] IS NULL" + " AND ");
                    }

                    else if (pair.Value.ToString() == "!null")
                    {
                        whereStatement.Append("[" + pair.Key + "] IS NOT NULL" + " AND ");
                    }

                    else if (pair.Value.ToString() == "!=0")
                    {
                        whereStatement.Append("[" + pair.Key + "] <> 0" + " AND ");
                    }

                    else if (pair.Value is string && (pair.Value.ToString()).Contains("BETWEEN"))
                    {
                        whereStatement.Append("[" + pair.Key + "]  " + pair.Value);

                        whereStatement.Append(" AND ");
                    }

                    else if (pair.Value is List<int>)
                    {
                        whereStatement.Append("[" + pair.Key + "] in ( ");

                        foreach (var item in (List<int>) pair.Value)
                        {
                            whereStatement.Append(item + ",");
                        }
                        //Remove last ','
                        whereStatement.Remove(whereStatement.Length - 1, 1);

                        whereStatement.Append(" ) AND ");
                    }

                    else if (pair.Value is List<string>)
                    {
                        whereStatement.Append("[" + pair.Key + "] in ( ");

                        foreach (var item in (List<string>) pair.Value)
                        {
                            whereStatement.Append("'" + item + "',");
                        }
                        //Remove last ','
                        whereStatement.Remove(whereStatement.Length - 1, 1);

                        whereStatement.Append(" ) AND ");
                    }

                    else
                    {
                        var valueType = pair.Value.GetType();
                        if (valueType == typeof (int) || valueType == typeof (Double))
                        {
                            whereStatement.Append("[" + pair.Key + "]=" + pair.Value + " AND ");
                        }
                        else
                        {
                            whereStatement.Append("[" + pair.Key + "]='" + pair.Value + "' AND ");
                        }
                    }
                }
                whereStatement.Remove(whereStatement.Length - 5, 5);
            }

            if (selectColumnsList != null && selectColumnsList.Count > 0)
                finalSelectQuery = String.Format("SELECT {0} ", selectColumns);
            else
                finalSelectQuery = String.Format("SELECT * ");

            if (whereClause != null && whereClause.Count > 0)
                finalSelectQuery = string.Format("{0} FROM [{1}] ({2}) WHERE {3}", finalSelectQuery,
                    databaseFunctionName, parameters, whereStatement);
            else
                finalSelectQuery = string.Format("{0} FROM [{1}] ({2})", finalSelectQuery, databaseFunctionName,
                    parameters);

            if (groupByColumnsList != null && groupByColumnsList.Count > 0)
                finalSelectQuery = string.Format("{0} GROUP BY {1} ", finalSelectQuery, groupByFields);
            else
                finalSelectQuery = string.Format("{0}", finalSelectQuery);


            var conn = DbInitializeConnection(ConnectionString);
            var comm = new OleDbCommand(finalSelectQuery, conn);

            try
            {
                conn.Open();
                dr = comm.ExecuteReader();

                dt.Load(dr);
            }
            catch (Exception ex)
            {
                var argEx = new ArgumentException("Exception", "ex", ex);
                throw argEx;
            }
            finally
            {
                conn.Close();
            }

            return dt;
        }

        /// <summary>
        ///     Execute a store procedure with some parameters
        /// </summary>
        /// <param name="tableName">DB Table Name</param>
        /// <param name="columnsValues">Dictionary Holds Fields and Values to be inserted</param>
        /// <returns>Row ID </returns>
        public void EXECUTE_STORE_PROCEDURE(string storeProcedureName, Dictionary<string, object> parameters)
        {
            OleDbConnection conn;
            OleDbCommand comm;
            var numberOfRowsAffected = 0;
            var finalSelectQuery = string.Empty;
            var Parameters = new StringBuilder();


            if (parameters != null && parameters.Count > 0)
            {
                foreach (var pair in parameters)
                {
                    var keyValueString = string.Empty;

                    keyValueString = String.Format("@{0} = N'{1}', ", pair.Key, pair.Value);
                    Parameters.Append(keyValueString);
                }

                Parameters.Remove(Parameters.Length - 2, 2);
            }


            //Construct the EXEC FINAL QUERY
            //SET TRANSACTION ISOLATION LEVEL SNAPSHOT; EXEC ....
            if (Parameters.Length > 0)
                finalSelectQuery = String.Format("SET TRANSACTION ISOLATION LEVEL SNAPSHOT; EXEC [dbo].[{0}] {1}",
                    storeProcedureName, Parameters);
            else
                finalSelectQuery = String.Format("SET TRANSACTION ISOLATION LEVEL SNAPSHOT; EXEC [dbo].[{0}]",
                    storeProcedureName);


            //Establish DB connection and create a command.
            //Set the connection timeout to 20 minutes in case there is no timeout already defined.
            var localConnectionString = ConnectionString;
            if (!localConnectionString.Contains("ConnectionTimout"))
            {
                localConnectionString += ";ConnectionTimeout=1800";
            }

            conn = DbInitializeConnection(localConnectionString);
            comm = new OleDbCommand(finalSelectQuery, conn);

            try
            {
                //conn.Open();
                //transaction = conn.BeginTransaction(IsolationLevel.ReadUncommitted);
                //comm.Transaction = transaction;

                conn.Open();
                comm.CommandTimeout = (30*60); //30 minutes
                numberOfRowsAffected = comm.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                conn.Close();
            }

            //return numberOfRowsAffected;
        }

        /// <summary>
        ///     Insert by EXECUTING NATIVE SQL QUERY
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public int INSERT(string sql)
        {
            var conn = DbInitializeConnection(ConnectionString);
            var comm = new OleDbCommand(sql, conn);

            var recordId = 0;
            try
            {
                conn.Open();
                recordId = Convert.ToInt32(comm.ExecuteNonQuery());
            }
            catch (Exception ex)
            {
                var argEx = new ArgumentException("Exception", "ex", ex);
                throw argEx;
            }
            finally
            {
                conn.Close();
                conn.Dispose();
            }

            return recordId;
        }

        /// <summary>
        ///     Construct Generic INSERT Statement
        /// </summary>
        /// <param name="tableName">DB Table Name</param>
        /// <param name="columnsValues">Dictionary Holds Fields and Values to be inserted</param>
        /// <returns>Row ID </returns>
        public int INSERT(string tableName, Dictionary<string, object> columnsValues)
        {
            var fields = new StringBuilder();
            fields.Append("(");
            var values = new StringBuilder();
            values.Append("(");
            var whereStatement = new StringBuilder();


            //Fields and values
            foreach (var pair in columnsValues)
            {
                var valueType = pair.Value.GetType();

                if (valueType == typeof (DateTime) && (DateTime) pair.Value == DateTime.MinValue)
                {
                    continue;
                }
                if (
                    pair.Value == DBNull.Value ||
                    pair.Value.ToString() == string.Empty ||
                    pair.Value == null)
                {
                    continue;
                }
                fields.Append("[" + pair.Key + "],");

                if (valueType == typeof (int) || valueType == typeof (Double) || valueType == typeof (Decimal))
                {
                    values.Append(pair.Value + ",");
                }
                else if (valueType == typeof (DateTime) && (DateTime) pair.Value == DateTime.MinValue)
                {
                }
                else
                {
                    //Date should inclue Milliseconds
                    if (valueType == typeof (DateTime))
                    {
                        var value = ((DateTime) pair.Value).ToString("yyyy-MM-dd HH:mm:ss.fff");
                        values.Append("'" + value.Replace("'", "`") + "'" + ",");
                    }
                    else
                    {
                        values.Append("'" + pair.Value.ToString().Replace("'", "`") + "'" + ",");
                    }
                }
            }

            fields.Remove(fields.Length - 1, 1).Append(")");
            values.Remove(values.Length - 1, 1).Append(")");

            var insertQuery = string.Format("INSERT INTO [{0}] {1} VALUES {2}", tableName, fields, values);

            var conn = DbInitializeConnection(ConnectionString);
            var comm = new OleDbCommand(insertQuery, conn);

            var recordId = 0;
            try
            {
                conn.Open();
                recordId = Convert.ToInt32(comm.ExecuteNonQuery());
            }
            catch (Exception ex)
            {
                var argEx = new ArgumentException("Exception", "ex", ex);
                throw argEx;
            }
            finally
            {
                conn.Close();
                conn.Dispose();
            }

            return recordId;
        }

        /// <summary>
        ///     Construct Generic INSERT Statement
        /// </summary>
        /// <param name="tableName">DB Table Name</param>
        /// <param name="columnsValues">Dictionary Holds Fields and Values to be inserted</param>
        /// <returns>Row ID </returns>
        public int INSERT(string tableName, Dictionary<string, object> columnsValues, string idFieldName)
        {
            var fields = new StringBuilder();
            fields.Append("(");
            var values = new StringBuilder();
            values.Append("(");
            var whereStatement = new StringBuilder();

            //Fields and values
            foreach (var pair in columnsValues)
            {
                if (pair.Value == null)
                {
                    fields.Append("[" + pair.Key + "],");
                    values.Append("NULL" + ",");
                }
                else if (pair.Value is int || pair.Value is Double)
                {
                    fields.Append("[" + pair.Key + "],");
                    values.Append(pair.Value + ",");
                }
                else if (pair.Value is DateTime && (DateTime) pair.Value == DateTime.MinValue)
                {
                }
                else
                {
                    fields.Append("[" + pair.Key + "],");
                    values.Append("'" + pair.Value.ToString().Replace("'", "`") + "'" + ",");
                }
            }

            fields.Remove(fields.Length - 1, 1).Append(")");
            values.Remove(values.Length - 1, 1).Append(")");

            var insertQuery = string.Format("INSERT INTO [{0}] {1} OUTPUT INSERTED.{2}  VALUES {3}", tableName, fields,
                idFieldName, values);

            var conn = DbInitializeConnection(ConnectionString);
            var comm = new OleDbCommand(insertQuery, conn);

            var recordId = 0;
            try
            {
                conn.Open();
                recordId = Convert.ToInt32(comm.ExecuteScalar());
            }
            catch (Exception ex)
            {
                var argEx = new ArgumentException("Exception", "ex", ex);
                throw argEx;
            }
            finally
            {
                conn.Close();
            }

            return recordId;
        }

        /// <summary>
        ///     Update by EXECUTING NATIVE SQL QUERY
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public bool UPDATE(string sql)
        {
            var conn = DbInitializeConnection(ConnectionString);
            var comm = new OleDbCommand(sql, conn);
            comm.CommandTimeout = 360;

            try
            {
                conn.Open();
                comm.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                var argEx = new ArgumentException("Exception", "ex", ex);
                throw argEx;
            }
            finally
            {
                conn.Close();
            }
        }

        /// <summary>
        ///     Construct Generic UPDATE Statement
        /// </summary>
        /// <param name="tableName">DB Table Name</param>
        /// <param name="columnsValues">Dictionary Holds Fields and Values to be inserted</param>
        /// <param name="idFieldName">ID Field name </param>
        /// <param name="id">ID Value</param>
        /// <returns>Row ID</returns>
        public bool UPDATE(string tableName, Dictionary<string, object> columnsValues, string idFieldName, Int64 id)
        {
            var fieldsValues = new StringBuilder();

            foreach (var pair in columnsValues)
            {
                var valueType = pair.Value.GetType();

                if (valueType == typeof (int) || valueType == typeof (Double))
                    fieldsValues.Append("[" + pair.Key + "]=" + pair.Value + ",");
                else if (valueType == typeof (DateTime) && (DateTime) pair.Value == DateTime.MinValue)
                    continue;
                else
                    fieldsValues.Append("[" + pair.Key + "]=" + "'" + pair.Value + "'" + ",");
            }

            fieldsValues.Remove(fieldsValues.Length - 1, 1);

            var insertQuery = string.Format("UPDATE  [{0}] SET {1} WHERE [{2}]={3}", tableName, fieldsValues,
                idFieldName, id);

            var conn = DbInitializeConnection(ConnectionString);
            var comm = new OleDbCommand(insertQuery, conn);
            comm.CommandTimeout = 360;

            try
            {
                conn.Open();
                comm.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                var argEx = new ArgumentException("Exception", "ex", ex);
                throw argEx;
            }
            finally
            {
                conn.Close();
            }
        }

        /// <summary>
        ///     Construct Generic UPDATE Statement
        /// </summary>
        /// <param name="tableName">DB Table Name</param>
        /// <param name="columnsValues">Dictionary Holds Fields and Values to be inserted</param>
        /// <param name="wherePart">Dictionary Holds Fields and Values to be able to construct Where Statemnet</param>
        /// <returns></returns>
        public bool UPDATE(string tableName, Dictionary<string, object> columnsValues,
            Dictionary<string, object> wherePart)
        {
            var fieldsValues = new StringBuilder();
            var whereStatement = new StringBuilder();

            foreach (var columnValuePair in columnsValues)
            {
                if (columnValuePair.Value == null)
                {
                    fieldsValues.Append("[" + columnValuePair.Key + "]=null,");
                }
                else if (columnValuePair.Value is int || columnValuePair.Value is Double)
                {
                    fieldsValues.Append("[" + columnValuePair.Key + "]=" + columnValuePair.Value + ",");
                }
                else if (columnValuePair.Value is bool)
                {
                    if ((bool) columnValuePair.Value)
                        fieldsValues.Append("[" + columnValuePair.Key + "]=" + 1 + ",");
                    else
                        fieldsValues.Append("[" + columnValuePair.Key + "]=" + 0 + ",");
                }
                else if (columnValuePair.Value is DateTime && ((DateTime) columnValuePair.Value == DateTime.MinValue))
                {
                }
                else
                {
                    fieldsValues.Append("[" + columnValuePair.Key + "]=" + "'" +
                                        columnValuePair.Value.ToString().Replace("'", "`") + "'" + ",");
                }
            }

            fieldsValues.Remove(fieldsValues.Length - 1, 1);

            foreach (var wherePair in wherePart)
            {
                if (wherePair.Value is DateTime && ((DateTime) wherePair.Value == DateTime.MinValue))
                {
                }
                else if (wherePair.Value is int || wherePair.Value is Double)
                {
                    whereStatement.Append("[" + wherePair.Key + "]=" + wherePair.Value + " AND ");
                }
                else if (wherePair.Value is bool)
                {
                    if ((bool) wherePair.Value)
                        whereStatement.Append("[" + wherePair.Key + "]=" + 1 + " AND ");
                    else
                        whereStatement.Append("[" + wherePair.Key + "]=" + 0 + " AND ");
                }
                else
                {
                    whereStatement.Append("[" + wherePair.Key + "]='" + wherePair.Value.ToString().Replace("'", "`") +
                                          "' AND ");
                }
            }
            whereStatement.Remove(whereStatement.Length - 5, 5);

            var updateQuery = string.Format("UPDATE  [{0}] SET {1} WHERE {2}", tableName, fieldsValues, whereStatement);

            var conn = DbInitializeConnection(ConnectionString);
            var comm = new OleDbCommand(updateQuery, conn);
            comm.CommandTimeout = 360;

            try
            {
                conn.Open();
                comm.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                var argEx = new ArgumentException("Exception", "ex", ex);
                throw argEx;
            }
            finally
            {
                conn.Close();
            }
        }

        /// <summary>
        ///     Construct Generic UPDATE Statement
        /// </summary>
        /// <param name="tableName">DB Table Name</param>
        /// <param name="columnsValues">Dictionary Holds Fields and Values to be inserted</param>
        /// <param name="idFieldName">ID Field name </param>
        /// <param name="id">ID Value</param>
        /// <returns>Row ID</returns>
        public bool UPDATE(string tableName, Dictionary<string, object> columnsValues, string idFieldName, Int64 id,
            ref OleDbConnection conn)
        {
            var fieldsValues = new StringBuilder();

            foreach (var pair in columnsValues)
            {
                var valueType = pair.Value.GetType();

                if (valueType == typeof (int) || valueType == typeof (Double))
                    fieldsValues.Append("[" + pair.Key + "]=" + pair.Value + ",");
                else if (valueType == typeof (DateTime) && (DateTime) pair.Value == DateTime.MinValue)
                    continue;
                else
                    fieldsValues.Append("[" + pair.Key + "]=" + "'" + pair.Value + "'" + ",");
            }

            fieldsValues.Remove(fieldsValues.Length - 1, 1);

            var insertQuery = string.Format("UPDATE  [{0}] SET {1} WHERE [{2}]={3}", tableName, fieldsValues,
                idFieldName, id);

            var comm = new OleDbCommand(insertQuery, conn);

            try
            {
                //if (conn.State == ConnectionState.Closed)
                //    conn.Open();

                comm.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                var argEx = new ArgumentException("Exception", "ex", ex);
                throw argEx;
            }
        }

        /// <summary>
        ///     Construct Generic UPDATE Statement
        /// </summary>
        /// <param name="tableName">DB Table Name</param>
        /// <param name="columnsValues">Dictionary Holds Fields and Values to be inserted</param>
        /// <param name="wherePart">Dictionary Holds Fields and Values to be able to construct Where Statemnet</param>
        /// <returns></returns>
        public bool UPDATE(string tableName, Dictionary<string, object> columnsValues,
            Dictionary<string, object> wherePart, ref OleDbConnection conn)
        {
            var updateQuery = string.Empty;
            var fieldsValues = new StringBuilder();
            var whereStatement = new StringBuilder();

            if (columnsValues.Count > 0)
            {
                foreach (var pair in columnsValues)
                {
                    var valueType = pair.Value.GetType();

                    if (valueType == typeof (int) || valueType == typeof (Double))
                        fieldsValues.Append("[" + pair.Key + "]=" + pair.Value + ",");
                    else if (valueType == typeof (DateTime) && ((DateTime) pair.Value == DateTime.MinValue))
                        continue;
                    else
                        fieldsValues.Append("[" + pair.Key + "]=" + "'" + pair.Value.ToString().Replace("'", "`") + "'" +
                                            ",");
                }

                fieldsValues.Remove(fieldsValues.Length - 1, 1);
            }

            if (wherePart.Count > 0)
            {
                foreach (var pair in wherePart)
                {
                    var valueType = pair.Value.GetType();

                    if (valueType == typeof (int) || valueType == typeof (Double))
                        whereStatement.Append("[" + pair.Key + "]=" + pair.Value + " AND ");
                    else if (valueType == typeof (DateTime) && ((DateTime) pair.Value == DateTime.MinValue))
                        continue;
                    else
                        whereStatement.Append("[" + pair.Key + "]='" + pair.Value.ToString().Replace("'", "`") +
                                              "' AND ");
                }

                whereStatement.Remove(whereStatement.Length - 5, 5);
            }

            if (whereStatement.Length > 0)
                updateQuery = string.Format("UPDATE  [{0}] SET {1} WHERE {2}", tableName, fieldsValues, whereStatement);
            else
                updateQuery = string.Format("UPDATE  [{0}] SET {1}", tableName, fieldsValues);


            var comm = new OleDbCommand(updateQuery, conn);

            try
            {
                comm.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                var argEx = new ArgumentException("Exception", "ex", ex);
                throw argEx;
            }
        }

        /// <summary>
        ///     Construct Generic UPDATE Statement
        /// </summary>
        /// <param name="tableName">DB Table Name</param>
        /// <param name="columnsValues">Dictionary Holds Fields and Values to be inserted</param>
        /// <param name="wherePart">Dictionary Holds Fields and Values to be able to construct Where Statemnet</param>
        /// <returns></returns>
        public bool UPDATE(string tableName, Dictionary<string, object> columnsValues, ref OleDbConnection conn)
        {
            var fieldsValues = new StringBuilder();
            var whereStatement = new StringBuilder();

            if (columnsValues.Count < 0)
                return false;

            foreach (var pair in columnsValues)
            {
                var valueType = pair.Value.GetType();

                if (valueType == typeof (int) || valueType == typeof (Double))
                    fieldsValues.Append("[" + pair.Key + "]=" + pair.Value + ",");
                else if (valueType == typeof (DateTime) && ((DateTime) pair.Value == DateTime.MinValue))
                    continue;
                else
                    fieldsValues.Append("[" + pair.Key + "]=" + "'" + pair.Value.ToString().Replace("'", "`") + "'" +
                                        ",");
            }

            fieldsValues.Remove(fieldsValues.Length - 1, 1);


            whereStatement.Append(String.Format("SessionIdTime='{0}' AND SessionIdSeq={1}",
                columnsValues["SessionIdTime"], columnsValues["SessionIdSeq"]));


            var insertQuery = string.Format("UPDATE  [{0}] SET {1} WHERE {2}", tableName, fieldsValues, whereStatement);

            var comm = new OleDbCommand(insertQuery, conn);

            try
            {
                comm.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                var argEx = new ArgumentException("Exception", "ex", ex);
                throw argEx;
            }
        }

        /// <summary>
        ///     Delete by EXECUTING NATIVE SQL QUERY
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public bool DELETE(string sql)
        {
            var conn = DbInitializeConnection(ConnectionString);
            var comm = new OleDbCommand(sql, conn);

            try
            {
                conn.Open();
                comm.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                var argEx = new ArgumentException("Exception", "ex", ex);
                throw argEx;
            }
            finally
            {
                conn.Close();
            }
        }

        /// <summary>
        ///     Construct Generic DELETE Statement
        /// </summary>
        /// <param name="tableName">DB Table Name</param>
        /// <param name="idFieldName">ID Field Name</param>
        /// <param name="id">ID Field Value</param>
        /// <returns>True if Row has been deleted. </returns>
        public bool DELETE(string tableName, string idFieldName, int id)
        {
            var deleteQuery = string.Format("DELETE FROM [{0}] WHERE [{1}]={2}", tableName, idFieldName, id);

            var conn = DbInitializeConnection(ConnectionString);
            var comm = new OleDbCommand(deleteQuery, conn);

            try
            {
                conn.Open();
                comm.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                var argEx = new ArgumentException("Exception", "ex", ex);
                throw argEx;
            }
            finally
            {
                conn.Close();
            }
        }

        /// <summary>
        ///     Construct Generic DELETE Statement
        /// </summary>
        /// <param name="tableName">DB Table Name</param>
        /// <param name="idFieldName">ID Field Name</param>
        /// <param name="id">ID Field Value</param>
        /// <returns>True if Row has been deleted. </returns>
        public bool DELETE(string tableName, string idFieldName, long id)
        {
            var deleteQuery = string.Format("DELETE FROM [{0}] WHERE [{1}]={2}", tableName, idFieldName, id);

            var conn = DbInitializeConnection(ConnectionString);
            var comm = new OleDbCommand(deleteQuery, conn);

            try
            {
                conn.Open();
                comm.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                var argEx = new ArgumentException("Exception", "ex", ex);
                throw argEx;
            }
            finally
            {
                conn.Close();
            }
        }

        /// <summary>
        ///     Construct Generic DELETE Statement
        /// </summary>
        /// <param name="tableName">DB Table Name</param>
        /// <param name="wherePart">Dictionary Holds Fields and Values to be able to construct Where Statemnet</param>
        /// <returns>True if Row has been deleted.</returns>
        public bool DELETE(string tableName, Dictionary<string, object> wherePart)
        {
            var whereStatement = new StringBuilder();

            //
            // Parse the where conitions
            foreach (var pair in wherePart)
            {
                var valueType = pair.Value.GetType();

                if (valueType == typeof (int) || valueType == typeof (long) || valueType == typeof (Decimal) ||
                    valueType == typeof (Double))
                {
                    whereStatement.Append("[" + pair.Key + "]=" + pair.Value + " AND ");
                }
                else
                {
                    whereStatement.Append("[" + pair.Key + "]='" + pair.Value + "' AND ");
                }
            }

            whereStatement.Remove(whereStatement.Length - 5, 5);

            //
            // Final DELETE SQL Statement
            var deleteQuery = string.Format("DELETE FROM [{0}] WHERE {1}", tableName, whereStatement);

            var conn = DbInitializeConnection(ConnectionString);
            var comm = new OleDbCommand(deleteQuery, conn);

            try
            {
                conn.Open();
                comm.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                var argEx = new ArgumentException("Exception", "ex", ex);
                throw argEx;
            }
            finally
            {
                conn.Close();
            }
        }

        public bool CREATE_RATES_TABLE(string tablename)
        {
            var createTableQuery = string.Format(
                "CREATE TABLE [dbo].[{0}] " +
                "(" +
                " [country_code_dialing_prefix] [bigint] NOT NULL," +
                " [rate] [decimal](18, 4) NOT NULL," +
                " CONSTRAINT [{0}] PRIMARY KEY NONCLUSTERED " +
                " ([country_code_dialing_prefix] ASC )" +
                " WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]) ON [PRIMARY]",
                tablename);

            var conn = DbInitializeConnection(ConnectionString);
            var comm = new OleDbCommand(createTableQuery, conn);

            try
            {
                conn.Open();
                comm.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                var argEx = new ArgumentException("Exception", "ex", ex);
                throw argEx;
            }
            finally
            {
                conn.Close();
            }
        }

        public bool Create(string tableName, Dictionary<string, string> columns)
        {
            var createTableQuery = new StringBuilder();
            createTableQuery.Append("CREATE TABLE ");
            createTableQuery.Append(tableName);
            createTableQuery.Append(" ( ");

            foreach (var keyValue in columns)
            {
                createTableQuery.Append(keyValue.Key);
                createTableQuery.Append(" ");
                createTableQuery.Append(keyValue.Value);
                createTableQuery.Append(", ");
            }

            createTableQuery.Length -= 2; //Remove trailing ", "
            createTableQuery.Append(")");

            var conn = DbInitializeConnection(ConnectionString);
            var comm = new OleDbCommand(createTableQuery.ToString(), conn);

            try
            {
                conn.Open();
                comm.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                var argEx = new ArgumentException("Exception", "ex", ex);
                throw argEx;
            }
            finally
            {
                conn.Close();
            }
        }

        public OleDbDataReader Executereader(string sqlStatment, OleDbConnection connection)
        {
            OleDbCommand command;

            command = new OleDbCommand(sqlStatment, connection);
            command.CommandTimeout = 20000;

            return command.ExecuteReader();
        }

        private static string GetDateForDatabase(DateTime dt)
        {
            return dt.Year + "-" + dt.Month + "-" + dt.Day + " " + dt.Hour + ":" + dt.Minute + ":" + dt.Second + "." +
                   dt.Millisecond;
        }

        public void OpenConnection(ref OleDbConnection conn)
        {
            conn.Open();
        }

        public void CloseConnection(ref OleDbConnection conn)
        {
            conn.Close();
        }
    }


    public class SqlJoinRelation
    {
        public Globals.DataRelation.Type RelationType { get; set; }
        public string MasterTableName { get; set; }
        public string MasterTableKey { get; set; }
        public string JoinedTableName { get; set; }
        public string JoinedTableKey { get; set; }
        public List<string> JoinedTableColumns { get; set; }
        public string RelationName { get; set; }
    }
}