using Dapper;
using Microsoft.Extensions.Configuration;
using Serilog;
using SpeedProcessing.BackEndService.Model;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;

namespace SpeedProcessing.BackEndService.DAL
{
    internal class SPDal
    {
        public System.Data.IDbConnection db = new SqlConnection(
           Utility.config.GetConnectionString("SpeedProcessingConnectionString")
            );

        public SPDal()
        {
            Dapper.SqlMapper.Settings.CommandTimeout = 0;
        }

        internal List<Querystring> GetETSACDailyList()
        {
            string sql = $@"
    SELECT CONCAT(
        'SELECT etpkg,etpdc, ettpno, etsac FROM libdf7.S002P WHERE etpdc=''', ProductCode, ''' AND etpkg = ''', tsdr, ''' AND etsac <> ''98'' AND ettpno IN (', 
        STRING_AGG(CONCAT('''', PartNumber, ''''), ','), 
        ')'
    ) AS querystring
    FROM [ETA].[dbo].[SpeedProcessing_ETSACSubmitLog]
    WHERE DATEDIFF(d, GETDATE(), TimeStamp) = 0
    GROUP BY tsdr, ProductCode";

            var orilist = db.Query<Querystring>(sql).ToList();

            return orilist;


        }

        internal string GetOrderByPackage(string tsdr)
        {
            string sql = $"SELECT top 1 [OrderNumber] FROM colsqlprd03.[ETA].[dbo].[OrderNumbers] where DesignNumber='{tsdr}'";
            //string orderNumber = null;
            dynamic result = db.Query(sql).FirstOrDefault();
            string orderNumber = result == null ? null : result.OrderNumber?.ToString();
            return orderNumber;

        }

        internal PartDetail GetPartNameByPartNumber(string partNumber)
        {
            partNumber = partNumber.Replace("-", "");
            string sql = $"exec [dbo].[GetPRDetailByPartNo]  '{partNumber}'";
            PartDetail res = db.Query<PartDetail>(sql).FirstOrDefault();

            return res;
        }

        internal List<ProcessObjectEntity> GetProcessObjList()
        {
            string sql = "exec ETA.[dbo].[GetSpeedProcessingBackEndTasks]";
            //$"exec [dbo].[Get_UnProcess_SpeedProcessingTaskList]";

            var orilist = db.Query<SPUnprocessTask>(sql).ToList();

            List<ProcessObjectEntity> reslist = new List<ProcessObjectEntity>();
            // get distinct tsdr+prod
            foreach (var item in orilist)
            {
                try
                {
                    ProcessObjectEntity res = new ProcessObjectEntity();
                    res.AddEntities = Newtonsoft.Json.JsonConvert.DeserializeObject<List<SPPartData>>(item.AddJson, Utility.jsonsettings);
                    res.DelEntities = Newtonsoft.Json.JsonConvert.DeserializeObject<List<SPDelPartData>>(item.DelJson, Utility.jsonsettings);

                    res.tsdr = item.tsdr;
                    res.productCode = item.productCode;
                    reslist.Add(res);
                }

                catch (Exception err)
                {
                    string notifymessage =
                        $"Json value of Task for {item.tsdr} , {item.productCode} are not valid, pleaes check . err message: {err.Message} ";
                    Log.Information(notifymessage);
                    Utility.SendNotificationToUser(item.tsdr, item.productCode, "Error: SpeedProcessing.BackendService AS400 Process ", notifymessage);

                }
            }

            return reslist;
        }

        internal string GetSPPartstorageLocation(string partNumber)
        {
            string query = @"
                SELECT TOP (1) StorageLocation
                FROM [ETA].[dbo].[SpeedProcessing_Part_Data]
                WHERE PartNumber = @PartNumber
                ORDER BY TimeStamp DESC";

            return db.QueryFirstOrDefault<string>(query, new { PartNumber = partNumber });

        }

        internal List<RDDSAPSQLResult> GetSPSAPAddDelData()
        {
            List<RDDSAPSQLResult> res = new List<RDDSAPSQLResult>();

            // Declare variables for add and delete results
            List<SpeedProcessingSAPAddData> addResult;
            List<SpeedProcessingSAPDelData> delResult;

            // Execute the stored procedure and get multiple results
            using (var multi = db.QueryMultiple("GetSPSAPAddDelData", commandType: CommandType.StoredProcedure))
            {
                // Read results for add and delete data
                addResult = multi.Read<SpeedProcessingSAPAddData>().ToList();
                delResult = multi.Read<SpeedProcessingSAPDelData>().ToList();
            }

            // Find distinct TSDR and ProductCode from addResult
            var tsdrList = addResult
                .GroupBy(x => new { x.TSDR, x.ProductCode })  // Group by TSDR and ProductCode
                .Select(g => g.Key)  // Select distinct TSDR and ProductCode pairs
                .ToList();  // Convert to a List of tuples

            // Iterate over each distinct TSDR and ProductCode combination
            foreach (var item in tsdrList)
            {
                var entry = new RDDSAPSQLResult
                {
                    tsdr = item.TSDR,
                    productCode = item.ProductCode,
                    addpartlist = addResult.Where(x => x.TSDR == item.TSDR && x.ProductCode == item.ProductCode).ToList(),
                    delpartlist = delResult.Where(x => x.TSDR == item.TSDR).ToList()
                };

                res.Add(entry);
            }

            return res;
        }

        internal List<TipsPartDrawing> GetTipPartDraws()
        {
            string sql = $"exec [dbo].[GetTipsAllVaultDrawings]";

            var res = db.Query<TipsPartDrawing>(sql).ToList();

            return res;

        }

        internal string RollBackSPSQL(string tsdr, string productCode)
        {
            try
            {
                string sql = $"exec ETA.[dbo].[DelSpeedProcessingPartsByTSDRProductcode] '{tsdr}','{productCode}'";
                db.Execute(sql);
                return "Success";
            }
            catch (Exception err)
            {
                return $"Error:{err.Message}";
            }

        }

        internal void SaveRDDPartToReview(SpeedProcessingRDDPartDetail partdetail, string tsdr, string productcode)
        {
            string query = @"INSERT INTO [dbo].[RDDPartReview]
                           ([partNumber]
                           ,[partName]
                           ,[qty]
                           ,[partLevel]
                           ,[kittingInstructions]
                           ,[storageLocation]
                           ,[modifiedMaterial]
                           ,[tsdr]
                           ,[productCode]
                           )
        VALUES (@partNumber, @partName, @qty, @partLevel, @kittingInstructions, @storageLocation, @modifiedMaterial, @tsdr, @productCode)";

            var parameters = new
            {
                partNumber = partdetail.partNumber,
                partName = partdetail.partName,
                qty = partdetail.qty,
                partLevel = partdetail.partLevel,
                kittingInstructions = partdetail.kittingInstructions,
                storageLocation = partdetail.storageLocation,
                modifiedMaterial = partdetail.modifiedMaterial ?? string.Empty, // Handle null
                tsdr = tsdr,
                productCode = productcode
            };

            db.Execute(query, parameters);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="addjsonlist"></param>
        /// <param name="deljsonlist"></param>
        /// <returns></returns>
        internal string SpeedProcessingBackEndTasks_Insert_Delete(string tsdr, string addjsonlist, string deljsonlist)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@tsdr", tsdr);
            parameters.Add("@AddJson", addjsonlist);
            parameters.Add("@DelJson", deljsonlist);
            parameters.Add("@Result", dbType: DbType.String, direction: ParameterDirection.Output, size: 50);

            db.Execute("SpeedProcessingBackEndTasks_Insert_Delete", parameters, commandType: CommandType.StoredProcedure);

            string result = parameters.Get<string>("@Result");

            Log.Information($"SpeedProcessingBackEndTasks_Insert_Delete process message:{result}");
            return result;
            /*
             select *  from [ETA].[dbo].[SpeedProcessing_Part_Data_t1]
            select * from Eta.dbo.SpeedProcessing_Delete_Unselected_t1
            select * from  [dbo].[SpeedProcessing_Delete_Staging_t1]

            DECLARE	@return_value int,
                    @Result nvarchar(50)

            EXEC	@return_value = [dbo].[SpeedProcessingBackEndTasks_Insert_Delete]
                    @tsdr = N'D7R0',
                    @AddJson = N'[{"TID":"MH7B;561;1901_56054UMH7B71_1_G865","ISADD":1,"Model":"MH7B","GroupNo":"561","CompCode":"19","Vari":"01","Ser":"","PartNumber":"56054UMH7B71","PartName":"WIRE; RR UPR; SUB; BLUE LIGHT","PartLevel":"1","Qty":"1","KJCode":"3","OriPartNumber":"","Itemcode":"","Contents":"","Short":"","Route":"","RemFromitemconverision":"","FromLocation":"","KittingInstructions":"V-MNA","StorageLocation":"MF01","ModifiedMaterial":"","Vari2":"","CaseType":"1","ColorRemove":null,"Color":"","Isactive":"1","TimeStamp":"01/12/2023 09:25:40","IsMulitple":0,"InstructionCode":"","IsEditable":0,"fcid":"2"},{"TID":"G865;565;5102_56530U136071_1_G865","ISADD":1,"Model":"G865","GroupNo":"565","CompCode":"51","Vari":"02","Ser":"","PartNumber":"56530U136071","PartName":"LAMP ASSY; BLUE","PartLevel":"1","Qty":"1","KJCode":"3","OriPartNumber":"","Itemcode":"","Contents":"","Short":"","Route":"","RemFromitemconverision":"","FromLocation":"","KittingInstructions":"","StorageLocation":"MF01","ModifiedMaterial":"","Vari2":"","CaseType":"1","ColorRemove":null,"Color":"","Isactive":"1","TimeStamp":"12/22/2022 14:30:51","IsMulitple":0,"InstructionCode":"","IsEditable":0,"fcid":"3"},{"TID":"G865;565;5102_56691U137071_1_G865","ISADD":1,"Model":"G865","GroupNo":"565","CompCode":"51","Vari":"02","Ser":"","PartNumber":"56691U137071","PartName":"BRACKET; BLUE LAMP","PartLevel":"1","Qty":"1","KJCode":"3","OriPartNumber":"","Itemcode":"","Contents":"","Short":"","Route":"","RemFromitemconverision":"","FromLocation":"","KittingInstructions":"","StorageLocation":"MF01","ModifiedMaterial":"","Vari2":"","CaseType":"1","ColorRemove":null,"Color":"","Isactive":"1","TimeStamp":"09/17/2015 08:24:10","IsMulitple":0,"InstructionCode":"","IsEditable":0,"fcid":"3"},{"TID":"G865;578;6902_57886U136071_1_G865","ISADD":1,"Model":"G865","GroupNo":"578","CompCode":"69","Vari":"02","Ser":"","PartNumber":"57886U136071","PartName":"INDICATOR; LAMP AIM","PartLevel":"1","Qty":"2","KJCode":"3","OriPartNumber":"","Itemcode":"","Contents":"","Short":"","Route":"","RemFromitemconverision":"","FromLocation":"","KittingInstructions":"","StorageLocation":"MF01","ModifiedMaterial":"","Vari2":"","CaseType":"1","ColorRemove":null,"Color":"","Isactive":"1","TimeStamp":"01/16/2023 07:26:24","IsMulitple":0,"InstructionCode":"","IsEditable":0,"fcid":"4"},{"TID":"G865;581;0203_461462300071A_1_G865","ISADD":1,"Model":"G865","GroupNo":"581","CompCode":"02","Vari":"03","Ser":"","PartNumber":"461462300071A","PartName":"WASHER; PLATE","PartLevel":"1","Qty":"4","KJCode":"3","OriPartNumber":"","Itemcode":"","Contents":"","Short":"","Route":"","RemFromitemconverision":"","FromLocation":"","KittingInstructions":"","StorageLocation":"MF01","ModifiedMaterial":"","Vari2":"","CaseType":"1","ColorRemove":null,"Color":"","Isactive":"1","TimeStamp":"01/07/2019 07:43:03","IsMulitple":0,"InstructionCode":"","IsEditable":0,"fcid":"5"},{"TID":"G865;581;0203_58120U361171_1_G865","ISADD":1,"Model":"G865","GroupNo":"581","CompCode":"02","Vari":"03","Ser":"","PartNumber":"58120U361171","PartName":"BUZZER ASSY; BACK","PartLevel":"1","Qty":"1","KJCode":"3","OriPartNumber":"","Itemcode":"","Contents":"","Short":"","Route":"","RemFromitemconverision":"","FromLocation":"","KittingInstructions":"","StorageLocation":"MF01","ModifiedMaterial":"","Vari2":"","CaseType":"1","ColorRemove":null,"Color":"","Isactive":"1","TimeStamp":"10/30/2019 06:59:10","IsMulitple":0,"InstructionCode":"","IsEditable":0,"fcid":"5"},{"TID":"G865;581;0203_901190805471_1_G865","ISADD":1,"Model":"G865","GroupNo":"581","CompCode":"02","Vari":"03","Ser":"","PartNumber":"901190805471","PartName":"","PartLevel":"1","Qty":"2","KJCode":"3","OriPartNumber":"","Itemcode":"","Contents":"","Short":"","Route":"","RemFromitemconverision":"","FromLocation":"","KittingInstructions":"","StorageLocation":"MF01","ModifiedMaterial":"","Vari2":"","CaseType":"1","ColorRemove":null,"Color":"","Isactive":"1","TimeStamp":"01/07/2019 07:43:03","IsMulitple":0,"InstructionCode":"","IsEditable":0,"fcid":"5"},{"TID":"G865;581;0203_901700800571_1_G865","ISADD":1,"Model":"G865","GroupNo":"581","CompCode":"02","Vari":"03","Ser":"","PartNumber":"901700800571","PartName":"NUT; HEXAGON","PartLevel":"1","Qty":"2","KJCode":"3","OriPartNumber":"","Itemcode":"","Contents":"","Short":"","Route":"","RemFromitemconverision":"","FromLocation":"","KittingInstructions":"","StorageLocation":"MF01","ModifiedMaterial":"","Vari2":"","CaseType":"1","ColorRemove":null,"Color":"","Isactive":"1","TimeStamp":"09/17/2015 08:24:10","IsMulitple":0,"InstructionCode":"","IsEditable":0,"fcid":"5"}]'
            ,
                    @DelJson = N'[{"ischecked":true,"pagenumber":"G865 5786901","partnumber":"57886U136071","partname":"INDICATOR, LAMP AIM","partlevel":1,"qty":1,"comment":"  ","fcitemid":4,"identification":"G8655786902G8655786901_57886U136071"},{"ischecked":true,"pagenumber":"G865 5810201","partnumber":"461462300071","partname":"WASHER, PLATE","partlevel":1,"qty":4,"comment":"  ","fcitemid":5,"identification":"G8655810203G8655810201_461462300071"},{"ischecked":true,"pagenumber":"G865 5810201","partnumber":"58116U136071","partname":"BRACKET, SMART ALARM","partlevel":1,"qty":1,"comment":"  ","fcitemid":5,"identification":"G8655810203G8655810201_58116U136071"},{"ischecked":false,"pagenumber":"G865 5810201","partnumber":"58120U361171","partname":"BUZZER ASSY, BACK","partlevel":1,"qty":1,"comment":"  ","fcitemid":5,"identification":"G8655810203G8655810201_58120U361171"},{"ischecked":false,"pagenumber":"G865 58120U361171","partnumber":"58110U109171","partname":"BUZZER ASSY, BACK","partlevel":2,"qty":1,"comment":"  ","fcitemid":5,"identification":"G8655810203G8655810201_58110U109171"},{"ischecked":true,"pagenumber":"G865 5810201","partnumber":"901190805471","partname":"BOLT, W/WASHER","partlevel":1,"qty":2,"comment":"  ","fcitemid":5,"identification":"G8655810203G8655810201_901190805471"},{"ischecked":false,"pagenumber":"G865 5810201","partnumber":"901700800571","partname":"NUT, HEXAGON","partlevel":1,"qty":2,"comment":"  ","fcitemid":5,"identification":"G8655810203G8655810201_901700800571"}]',


                    @Result = @Result OUTPUT

            SELECT	@Result as N'@Result'

            SELECT	'Return Value' = @return_value
             */
        }

        internal string UpdaeTaskStatus(ProcessObjectEntity item, int flagbit)
        {
            try
            {
                string sql = $"UPDATE [dbo].[SPJsonLogs] SET   [isprocessed] = {flagbit} WHERE  tsdr='{item.tsdr}' and productCode='{item.productCode}' ";
                db.Execute(sql);
                return "Success";
            }
            catch (Exception err)
            {
                return $"Error:{err.Message}";
            }



        }

        internal void UpdateSPSAPAddDelData(string tsdr, string productCode)
        {
            try
            {
                string sql = $"exec UpdateSPSAPAddDelData {tsdr}, {productCode}";
                db.Execute(sql);

            }
            catch (Exception err)
            {
                Log.Error($"Error:{err.Message}");
            }

        }
    }
}
