using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Serilog;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace SpeedProcessing.BackEndService
{

    public class LowercaseContractResolver : DefaultContractResolver
    {
        protected override string ResolvePropertyName(string propertyName)
        {
            return propertyName.ToLower();
        }
    }

    public class Utility
    {
        public static IConfiguration config;

        public static string AS400APIEndPint = string.Empty;
        public static string AS400APIUpdateEndPint = string.Empty;
        public static string AS400APIDeleteEndPint = string.Empty;
        public static string SAPAPIEndPoint = string.Empty;
        public static string SAPAPIKey = string.Empty;



        public static string FolderPath = string.Empty;

        public static JsonSerializerSettings jsonsettings = new JsonSerializerSettings
        {
            ContractResolver = new LowercaseContractResolver()
        };
        static Utility()
        {

            var builder = new ConfigurationBuilder();
            var environmentName = Environment.GetEnvironmentVariable("ENVIRONMENT");
            string appsettingsname = "";
            if(string.IsNullOrEmpty(environmentName))
            {
                appsettingsname = "appsettings.json";
            }
            else
            {
                appsettingsname = $"appsettings.{environmentName}.json";
            }
            builder.SetBasePath(Directory.GetCurrentDirectory())
                   .AddJsonFile($"{appsettingsname}", optional: true, reloadOnChange: true)
                   //.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                   .AddEnvironmentVariables();
            Utility.config = builder.Build();


            //config = new ConfigurationBuilder()
            //    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            //    .Build();

            AS400APIEndPint = config["AS400ApiEndPoint:BaseUrl"] + config["AS400ApiEndPoint:QueryEnv"];
            AS400APIUpdateEndPint = config["AS400ApiEndPoint:BaseUrlUpdate"] + config["AS400ApiEndPoint:UpdateQueryEnv"];
            AS400APIDeleteEndPint = AS400APIUpdateEndPint + "&adminpwd=tmhappdev";
            FolderPath = config["AppSettings:SAPFolderPath"];
            SAPAPIEndPoint = config["SAPApiEndPoint:SAPApiBaseUrl"];
            SAPAPIKey = config["SAPApiEndPoint:ApiKey"];
        }

        internal static void SendNotificationToUser(string tsdr, string prodcode, string subject, string message)
        {

            EmailSender.SendEmail(
                Utility.config["MailSettings:Smtpsender"],
                Utility.config["MailSettings:Smtpreceiver"],
                $"{subject} for {tsdr} : {prodcode} ", message);

        }
        #region AS400 API Operations
        public static string SendGetAS400ApiRe(string stdUrl)
        {
            try
            {
                //  WebRequest reqObject = WebRequest.Create(stdUrl);
                ////  reqObject.Timeout = Infinite;
                //  reqObject.Method = "GET";
                //  reqObject.ContentType = "application/json";
                //  var response = reqObject.GetResponse();
                //  var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
                //  var responseString = reader.ReadToEnd();
                //  reader.Close();
                //  return responseString;
                // stdUrl = "http://colwebprd01/as400/api/Data?server=t&statement=select%20*%20from%20LIBDF7.S032P%20where%20LIBDF7.S032P.BDODN%3D%270514140%27%20order%20by%20LIBDF7.S032P.BDDT%20desc%2CLIBDF7.S032P.BDTM%20desc%20fetch%20first%201%20row%20only";
                WebRequest reqObject = WebRequest.Create(stdUrl);
                //  ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                reqObject.Method = "GET";
                reqObject.ContentType = "application/json";
                var response = reqObject.GetResponse();
                var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
                var responseString = reader.ReadToEnd();
                reader.Close();
                return responseString;

            }
            catch (Exception err)
            {
                Log.Information("GetAS400ApiResult Error: " + err.Message);
                return "";
            }

        }

        //public  static string SendPostAS400ApiRe(string strUrl, string data)
        //{
        //    //http://localhost:54618/swagger/ui/index#!/Values/Values_Post
        //    strUrl = "http://localhost:54618/api/Data?server=t&processfilename=SpeedProBackEnd";

        //    WebRequest reqObject = WebRequest.Create(strUrl);
        //    reqObject.Method = "POST";
        //    reqObject.ContentType = "application/json";
        //    string postData = "{\"sql\": \"" + data + "\"}";
        //    string jsonsql = $"{{\"sql\": \"{data}\"}}";
        //    byte[] byteArray = Encoding.UTF8.GetBytes(postData);
        //    reqObject.ContentLength = byteArray.Length;
        //    Stream dataStream = reqObject.GetRequestStream();
        //    dataStream.Write(byteArray, 0, byteArray.Length);
        //    dataStream.Close();
        //    var response = reqObject.GetResponse();
        //    var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
        //    var message = reader.ReadToEnd();
        //    reader.Close();
        //    return message;

        //    //if (A797Pstatus == "\"Success;Success\"")
        //    //{
        //    //    return true;
        //    //}
        //    //else
        //    //{
        //    //    return false;
        //    //}


        //}

        /// <summary>
        /// http client
        /// </summary>
        /// <param name="url"></param>
        /// <param name="json"></param>
        /// <returns></returns>
        static async Task<string> PostJsonAsync(string url, string json)
        {
            using (HttpClient client = new HttpClient())
            {
                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(url, content);
                response.EnsureSuccessStatusCode(); // Throw an exception if the response is not successful
                string responseContent = await response.Content.ReadAsStringAsync();
                return responseContent;
            }
        }

        /// <summary>
        /// only len 11, 13 can have minor, minor is last character
        /// </summary>
        /// <param name="partnum"></param>
        /// <param name="minor"></param>
        /// <param name="inputpart"></param>      


        public static void GetPartMinorFromInput(out string partnum, out string minor, string inputpart)
        {

            int len = inputpart.Length;
            if (len == 11 || len == 13)
            {
                partnum = inputpart.Substring(0, len - 2);
                minor = inputpart[len - 1].ToString();
            }
            else
            {
                partnum = inputpart;
                minor = "";
            }

        }


        public static string RemoveExtraSpace(string apiurl)
        {
            string pattern = "\\s+";

            string replacement = " ";                       // replacement pattern

            Regex rx = new Regex(pattern);

            string result = rx.Replace(apiurl, replacement);
            return result;
        }


        private static string GetTypeByQueryScript(string statement)
        {
            if (statement.Contains("insert"))
            {
                return "ADD";
            }
            if (statement.Contains("update"))
            {
                return "UPDATE";
            }
            if (statement.Contains("delete"))
            {
                return "DELETE";
            }
            return "NotA/U";
        }
        /// <summary>
        ///  need detail 
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public static bool SendIssueNotification(string body)
        {
            
            bool send = false;
            // SMTP server configuration
            string smtpServer = "smtp.office365.com";
            int smtpPort = 587; // or use the appropriate port for your SMTP server

            string smtpUsername = "Dayang.Sun@toyotatmh.com";
            string smtpPassword = "Cummins1*2024";
            // Email details
            string fromAddress = "noreply@toyotatmh.com";
            string toAddress = "Dayang.Sun@toyotatmh.com,greg.mcnealy@toyotatmh.com";
            string subject = "Hello, World!";
            //string body = "This is the body of the email.";

            // Create a new SMTP client
            using (SmtpClient client = new SmtpClient(smtpServer, smtpPort))
            {
                //client.EnableSsl = true;
                client.Credentials = new NetworkCredential("noreply@toyotatmh.com", "wgXsNL4vD3RNwwtbUkya");

                // Create a new email message
                MailMessage message = new MailMessage(fromAddress, toAddress, subject, body);

                try
                {
                    // Send the email
                    client.Send(message);
                    Console.WriteLine("Email sent successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to send email: " + ex.Message);
                }
            }
            return send;
        }

        public static bool AppendTextToSAPFile(string fname, string linestr)
        {
            bool res = true;
            //   string folderPath = @"C:\sptest"; // Folder path
            string filePath = Path.Combine(Utility.FolderPath, fname); // Combine folder path and file name
            try
            {
                // Create the folder if it doesn't exist
                if (!Directory.Exists(Utility.FolderPath))
                {
                    Directory.CreateDirectory(Utility.FolderPath);
                }

                // Append lines to the file
                using (StreamWriter writer = File.AppendText(filePath))
                {
                    writer.WriteLine(linestr); // Append a new line
                                               // writer.WriteLine("Another line appended."); // Append another line
                }

                // Console.WriteLine("Lines appended successfully.");
            }
            catch (Exception ex)
            {
                res = false;
                Log.Information($"AppendTextToSAPFile {fname}  fail:{ex.Message}");

                Console.WriteLine($"AppendTextToSAPFile {fname}  fail:{ex.Message}");
            }

            return res;



        }

        internal static string CheckNull(string str)
        {
            return String.IsNullOrEmpty(str) == true ? "" : str;
        }
        #endregion


        // Rest of the part_number...

    }

    public static class EmailSender
    {
        private static System.Net.Mail.SmtpClient smtp = null;//= new System.Net.Mail.SmtpClient("webmail.us.toyota-industries.com");

        static EmailSender()
        {
            if (smtp == null)
            {

                smtp = //new System.Net.Mail.SmtpClient("smtp.ad.us.toyota-industries.com");
                new System.Net.Mail.SmtpClient(Utility.config["MailSettings:Smtpserver"]);
                smtp.Credentials = new System.Net.NetworkCredential("smarteam.service@tiem.toyota-industries.com", "smartiem#1");
            }

        }



        public static string SendEmail(string sender, string receiver, string subject, string content, string attfilename=null)
        {
           // subject = ApplicationSetting.ENV + subject;

            string result = string.Empty;
            Attachment temp = null;
            try
            {
                MailMessage message = new MailMessage(sender, receiver, subject, content);
                message.IsBodyHtml = true;
                if (!string.IsNullOrEmpty(attfilename))
                {
                    temp = new Attachment(attfilename);
                    message.Attachments.Add(temp);
                }
                smtp.Send(message);
                result = "Successs";
                if (temp != null)
                {
                    temp.Dispose();
                }
            }
            catch (Exception err)
            {
                result = string.Format("Error:{0}", err.Message);
            }

            return result;
        }

    }

}
