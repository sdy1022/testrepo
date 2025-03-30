using System.Data;
using System.Reflection;

namespace Common.Utility
{
    public static class AppUtility
    {
        public static string AS400RancherAPITUrl = $"https://linuxserviceas400.development.toyotatmh.io/AS400Api?servername=p&sql=";
        public static string AS400RancherAPIPUrl = $"https://linuxserviceas400.production.toyotatmh.io/AS400Api?servername=t&sql=";
      //  public static Dictionary<string, string> SftpDict { get; set; }
        public static Dictionary<string, string> RabbitMQDict { get; set; }


        // public static Dictionary<string, string> MongoDBDict { get; set; }

        public static int DEQUEUEMAXCOUNT { get; set; }

      
        private static object AssignNull(object? value)
        {
            return value == null ? DBNull.Value : value;
        }

        public static string GetTimeStampFileName(string orifilename)
        {
            DateTime now = DateTime.Now;
            // Format the timestamp
            string timestamp = now.ToString("yyMMddHHmmssff");
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(orifilename);
            string extension = Path.GetExtension(orifilename);
            return  $"{nameWithoutExtension}_{timestamp}{extension}";
          
        }
        public static DataTable ToDataTable<T>(List<T> items)
        {
            DataTable dataTable = new DataTable(typeof(T).Name);

            //Get all the properties
            PropertyInfo[] Props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo prop in Props)
            {
                //Defining type of data column gives proper data table 
                var type = (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>) ? Nullable.GetUnderlyingType(prop.PropertyType) : prop.PropertyType);
                //Setting column names as Property names
                dataTable.Columns.Add(prop.Name, type);
            }
            foreach (T item in items)
            {
                var values = new object[Props.Length];
                for (int i = 0; i < Props.Length; i++)
                {
                    //inserting property values to datatable rows
                    values[i] = Props[i].GetValue(item, null);
                }
                dataTable.Rows.Add(values);
            }
            //put a breakpoint here and check datatable
            return dataTable;
        }
    }

   
}
