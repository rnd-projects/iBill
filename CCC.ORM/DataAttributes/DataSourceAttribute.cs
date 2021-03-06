﻿using System;

namespace CCC.ORM.DataAttributes
{
    /// <summary>
    ///     This attribute is designed to tell the repository that the class or struct which is decorated with it is resembles
    ///     a Database Table and sets it's name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class DataSourceAttribute : Attribute
    {
        /// <summary>
        ///     This option specifies the data source name. It's value changes with respect to it's source-type and access-type.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     This option specifies the kind of the data source. It can be one of the following:
        ///     * DBTable: A database table. In this case, the Name option specifies the table name.
        ///     * WS: A webservice endpoint. In this case, the Name option speficies the address.
        /// </summary>
        public Globals.DataSource.Type Type { get; set; }

        /// <summary>
        ///     This specifies how the data is read and fetched. This option can be one of the following:
        ///     * SingleSource: Data is read from a single data source, such as: a table, a webservice endpoint...etc
        ///     * Distributed: Data is read from multiple sources, this data source acts as a lookup of the other sources, such as:
        ///     a lookup table, a lookup webservice endpoint...etc
        /// </summary>
        public Globals.DataSource.AccessMethod AccessMethod { get; set; }
    }
}