using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Data.SqlServerCe;


namespace MappingExample
{
    /// <summary>
    /// a very crude and non-robust mapper/repository for Employee objects
    /// not exemplary - only done to illustrate some mapping issues!
    /// </summary>
    public class EmployeeMapper
    {
        private SqlCeConnection conn;

        private Dictionary<int, Employee> identityMap;

        public EmployeeMapper()
        {
            string connectionString = "data source='company.sdf'";
            conn = new SqlCeConnection(connectionString);

            identityMap = new Dictionary<int, Employee>();

        }

        /// <summary>
        /// gets specified employee with associated address and postcode objects
        /// </summary>
        /// <param name="id">the employee id</param>
        /// <returns>an employee object</returns>
        public Employee GetById(int id)
        {
            Employee result = null;

            // check whether target object is already loaded
            if (identityMap.ContainsKey(id))
            {
                result = identityMap[id];
            }
            else
            {
                StringBuilder sqlb = new StringBuilder(
                    "SELECT employeeid,name,username,phonenumber, discriminator, paygrade, propertyname, propertynumber, postcode ");
                sqlb.Append("FROM EMPLOYEES e, ADDRESSES a ");
                sqlb.Append("WHERE e.addressID = a.addressID ");
                sqlb.Append("AND employeeID = ");
                sqlb.Append(id.ToString());
                string sql = sqlb.ToString();

                SqlCeCommand comm = new SqlCeCommand(sql, conn);
                if(conn.State==ConnectionState.Closed) conn.Open();
                SqlCeDataReader reader = comm.ExecuteReader(CommandBehavior.CloseConnection);

                if (reader.Read())
                {
                    int employeeID = reader.GetInt32(0);
                    string name = reader.GetString(1);
                    string username = reader.GetString(2);
                    string phonenumber = reader.GetString(3);
                    string discriminator = reader.GetString(4);
                    int paygrade = 0;
                    if(!reader.IsDBNull(5))
                        paygrade = reader.GetInt32(5);
                    string propertyname = reader.GetString(6);
                    int propertynumber = reader.GetInt32(7);
                    string postcode = reader.GetString(8);

                    PostCode pc = new PostCode(postcode);
                    Address ad = new Address(propertyname, propertynumber, pc);

                    if (discriminator.Equals("S"))
                    {
                        // need to include pay grade in database schema and adjust query
                        result = new SalariedEmployee(employeeID, name, username, ad, phonenumber, paygrade);
                    }
                    else if (discriminator.Equals("H"))
                    {
                        result = new HourlyPaidEmployee(employeeID, name, username, ad, phonenumber);
                    }
                    else
                    {
                        throw new Exception("Invalid employee type");
                    }
                }

                //conn.Close();
                identityMap.Add(id, result);
            }
            return result;
        }

        /// <summary>
        /// gets all hourly paid employees
        /// with associated address, postcode and supervisor objects
        /// </summary>
        /// <returns>list of employees</returns>
        public List<Employee> GetAllHourlyPaid()
        {
            List<Employee> result = new List<Employee>();

            StringBuilder sqlb = new StringBuilder(
                    "SELECT employeeid,name,username,supervisor, phonenumber, propertyname, propertynumber, postcode ");
            sqlb.Append("FROM EMPLOYEES e, ADDRESSES a ");
            sqlb.Append("WHERE e.addressID = a.addressID ");
            sqlb.Append(@"AND discriminator = 'h'");
            string sql = sqlb.ToString();

            SqlCeCommand comm = new SqlCeCommand(sql, conn);
            if (conn.State == ConnectionState.Closed) conn.Open();
            SqlCeDataReader reader = comm.ExecuteReader(CommandBehavior.CloseConnection);

            while(reader.Read())
            {
                Employee emp = null;

                int employeeID = reader.GetInt32(0);
                string name = reader.GetString(1);
                string username = reader.GetString(2);
                int supervisor = -1;
                if(!reader.IsDBNull(3))
                    supervisor = reader.GetInt32(3);
                string phonenumber = reader.GetString(4);
                string propertyname = reader.GetString(5);
                int propertynumber = reader.GetInt32(6);
                string postcode = reader.GetString(7);

                if (identityMap.ContainsKey(employeeID))
                {
                    emp = identityMap[employeeID];
                }
                else
                {
                    PostCode pc = new PostCode(postcode);
                    Address ad = new Address(propertyname, propertynumber, pc);
                    emp = new HourlyPaidEmployee(employeeID, name, username, ad, phonenumber);
                    emp.Supervisor = GetById(supervisor);
                    identityMap.Add(employeeID, emp);
                }
                result.Add(emp);
            }

            //conn.Close();

            return result;
        }

        /// <summary>
        /// store an hourly paid employee incuding address/postcode and supervisor ID
        /// assumes address has not previously been stored
        /// </summary>
        /// <param name="emp"></param>
        /// <returns></returns>
        public int StoreHourlyPaid(HourlyPaidEmployee emp)
        {
            // store address first (assume we are sure address is not in db)
            StringBuilder sqlb = new StringBuilder(
                    "INSERT INTO Addresses(propertyname, propertynumber, postcode) ");
            sqlb.Append(@"VALUES('");
            sqlb.Append(emp.Address.PropertyName);
            sqlb.Append(@"',");
            sqlb.Append(emp.Address.PropertyNumber);
            sqlb.Append(@", '");
            sqlb.Append(emp.Address.PostCode.FullCode);
            sqlb.Append(@"')");
            string sql = sqlb.ToString();

            SqlCeCommand comm = new SqlCeCommand(sql, conn);
            if (conn.State == ConnectionState.Closed) conn.Open();
            comm.ExecuteNonQuery();

            sql = "SELECT Max(addressID) FROM Addresses";

            comm = new SqlCeCommand(sql, conn);
            if (conn.State == ConnectionState.Closed) conn.Open();
            int addressID = (int)comm.ExecuteScalar();

            sqlb = new StringBuilder(
                    "INSERT INTO Employees(name, phonenumber, username, discriminator, addressID, supervisor) ");
            sqlb.Append(@"VALUES('");
            sqlb.Append(emp.Name);
            sqlb.Append(@"', '");
            sqlb.Append(emp.PhoneNumber);
            sqlb.Append(@"', '");
            sqlb.Append(emp.Username);
            sqlb.Append(@"', 'H',");
            sqlb.Append(addressID);
            sqlb.Append(@", ");
            sqlb.Append(emp.Supervisor.EmployeeId);
            sqlb.Append(@")");
            sql = sqlb.ToString();

            comm = new SqlCeCommand(sql, conn);
            if (conn.State == ConnectionState.Closed) conn.Open();
            comm.ExecuteNonQuery();

            sql = "SELECT Max(employeeID) FROM Employees";

            comm = new SqlCeCommand(sql, conn);
            if (conn.State == ConnectionState.Closed) conn.Open();
            int employeeID = (int)comm.ExecuteScalar();

            conn.Close();
            return employeeID;
        }


    }
}
