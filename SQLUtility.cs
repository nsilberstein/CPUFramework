using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CPUFramework
{
    public static class SQLUtility
    {
        public enum ExecSQLTypeEnum { NoResultSet, SingleRecord, MultipleRecord}
        public static List<T> ExecuteGetListDapper<T>(string sprocname, DynamicParameters dynamparam) where T: new()
        {
           (T tobj, List<T> tlst) = DoExecuteSQLDapper<T>(sprocname, dynamparam, ExecSQLTypeEnum.MultipleRecord);
            return tlst;
        }
        public static T ExecuteGetSingleDapper<T>(string sprocname, DynamicParameters dynamparam) where T: new()
        {
            (T tobj, List<T> tlst) = DoExecuteSQLDapper<T>(sprocname, dynamparam, ExecSQLTypeEnum.SingleRecord);
            return tobj;
        }
        public static void ExecuteSQLDapper(string sprocname, DynamicParameters dynamparam)
        {
            DoExecuteSQLDapper<object>(sprocname, dynamparam);
        }
        private static (T, List<T>) DoExecuteSQLDapper<T>(string sprocname, DynamicParameters dynamparam, ExecSQLTypeEnum execsqltype = ExecSQLTypeEnum.NoResultSet) where T : new()
        {
            T tobj = new();
            List<T> tlst = new();
            dynamparam.Add("Message", "", direction: ParameterDirection.InputOutput);
            dynamparam.Add("return_value", "", direction: ParameterDirection.ReturnValue);
            using (SqlConnection conn = new SqlConnection(DataUtility.ConnectionString))
            {
                try
                {
                    switch (execsqltype)
                    {
                        case ExecSQLTypeEnum.SingleRecord:
                            tobj = conn.QueryFirstOrDefault<T>(sprocname, dynamparam, commandType: CommandType.StoredProcedure);
                            if (tobj == null)
                            {
                                tobj = new T();
                            }
                            break;
                        case ExecSQLTypeEnum.MultipleRecord:
                            tlst = conn.Query<T>(sprocname, dynamparam, commandType: CommandType.StoredProcedure).ToList();
                            break;
                        default:
                            conn.Execute(sprocname, dynamparam, commandType: CommandType.StoredProcedure);
                            break;
                    }
                }
                catch (SqlException ex) when (IsConstraintError(ex.Message))
                {
                    throw new CPUException(ex.Message);
                }
              
            }
            int ret = dynamparam.Get<int>("return_value");
            string msg = dynamparam.Get<string>("Message");
            if (ret == 1)
            {
                throw new CPUException(msg);
            }

           
            return (tobj, tlst);
        }

        //internal static object DoGet<T>(string SprocName, DynamicParameters dynmparam, ExecSQLTypeEnum execsqltype)
        //{
        //    object obj = null;
        //    using (SqlConnection conn = new SqlConnection(DataUtility.ConnectionString))
        //    {
                
        //        switch(execsqltype)
        //        {
        //            case ExecSQLTypeEnum.SingleRecord: 
        //            obj = conn.QueryFirstOrDefault<T>(SprocName, dynmparam, commandType: CommandType.StoredProcedure);
        //                break;
        //            case ExecSQLTypeEnum.MultipleRecord:
        //            obj = conn.Query<T>(SprocName, dynmparam, commandType: CommandType.StoredProcedure).ToList();
        //                break;
        //        }

        //        return obj;

        //    }
        //}
       
        private static DataTable ExecuteSQL(SqlCommand cmd, string connstringvalue)
        {
            Debug.Print(GetSQL(cmd));
            using (SqlConnection conn = new SqlConnection(connstringvalue))
            {
                cmd.Connection = conn;
                conn.Open();
                if (cmd.CommandType == CommandType.StoredProcedure && cmd.Parameters.Count == 0)
                {
                    SqlCommandBuilder.DeriveParameters(cmd);
                }
                DataTable dt = new DataTable();

                try
                {
                SqlDataReader dr = cmd.ExecuteReader();


                if (cmd.Parameters.Contains("@Return_value") && cmd.Parameters["@Return_value"].Value != null)
                {
                    int returnval = (int)cmd.Parameters["@Return_value"].Value;
                    if (returnval == 1)
                    {
                        string msg = cmd.Parameters["@Message"].Value.ToString();
                        throw new CPUException(msg, cmd.CommandText);

                    }
                }
                dt.Load(dr);
            }
                catch(SqlException ex) when (IsConstraintError(ex.Message))
                {
                    throw new CPUException(ex.Message, cmd.CommandText);
                }
                return dt;
            }
         
        }

        private static bool IsConstraintError(string message)
        {
            bool b = false;
            if (message.ToLower().Contains("f_") || message.ToLower().Contains("ck_")) ;
            {
                b = true;
            }
            return b;
        }
        public static SqlCommand GetSQLCommand(string connstringvalue, string sprocname)
        {
            using (SqlConnection conn = new SqlConnection(connstringvalue))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand(sprocname, conn);
                cmd.CommandType = CommandType.StoredProcedure;
                SqlCommandBuilder.DeriveParameters(cmd);
                return cmd;
            }
         
        }

        public static DataTable GetDataTable(SqlCommand cmd, string connstringvalue)
        {
            return ExecuteSQL(cmd, connstringvalue);
        }
        public static DataTable GetDataTable(string connstringvalue, string sqlstatement)
        {
            SqlCommand cmd = new SqlCommand(sqlstatement);
            cmd.CommandType = CommandType.Text;
            return ExecuteSQL(cmd, connstringvalue);
        }

        public static DataTable GetDataTableFromSproc(string connstringvalue, string procname)
        {
            SqlCommand cmd = new SqlCommand(procname) { CommandType = CommandType.StoredProcedure};

            return ExecuteSQL(cmd, connstringvalue);
        }

        private static string GetSQL(SqlCommand cmd) 
        {
            string val = "";
            if (cmd.Connection != null)
            {
                val += "********\n" + cmd.Connection.DataSource + "\nuse " + cmd.Connection.Database + "\ngo\n";
            }
            if (cmd.CommandType == CommandType.StoredProcedure)
            {
                val += "exec " + cmd.CommandText;
                foreach(SqlParameter p in cmd.Parameters)
                {
                   
                    if (p.ParameterName != "@RETURN_VALUE")
                    {
                        string paramval = "null";
                        if (p.Value != null)
                        {
                            paramval = p.Value.ToString();
                        }
                        val += "\n" + p.ParameterName + " = " + paramval;
                    }                 
                }
            }
            else
            {
                val += cmd.CommandText;
            }

            return val;
        }
    }
}
