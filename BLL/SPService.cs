using Code.Models;
using Common.Utility;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog;
using SpeedProcessing.BackEndService.DAL;
using SpeedProcessing.BackEndService.Model;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace SpeedProcessing.BackEndService.BLL
{

    public interface IService
    {
        void Process();

    }
    internal class SPService : IService
    {

        private SPDal dal = new SPDal();
        private HttpClient httpClient = new HttpClient();

        private readonly IRabbitMQService _rabbitMQService;

        private readonly ILogger<SPService> _logger;
        public SPService(IRabbitMQService rabbitMQService

           , ILogger<SPService> logger)
        {
            _logger = logger;
            _rabbitMQService = rabbitMQService;

        }
        #region Other
        internal List<ProcessObjectEntity> GetProcessObjList()
        {
            Log.Information("in getprocessobjlist");
            return dal.GetProcessObjList();
        }
        /// <summary>
        /// main method
        /// </summary>
        /// 

        internal void SPBackendProcessSPList()
        {


            //  var ssd=Utility.config["AS400ApiEndPoint:BaseUrl"]; 
            bool res = false;
            List<ProcessObjectEntity> processlist = GetProcessObjList();
            foreach (ProcessObjectEntity item in processlist)
            {
                // AS400 process
                res = ProcessSPAS400(item);
                if (!res)
                {   // will robllback AS400 Change                   
                    RollBackSPAS400(item);
                    Log.Error("AS400 process for item: " + item + " Failed. Will send notfication to Customer and IT ");
                    Utility.SendNotificationToUser(item.tsdr, item.productCode,
                        "Error: SpeedProcessing.BackendService AS400 Process ", $"{item.tsdr} , {item.productCode}  Error: {res}");
                    continue;
                }
                Log.Information("AS400 process for item: " + item + " Done");

                //SAP
                var resstr = ProcessSPSAP(item);
                if (resstr != "Success")
                {
                    // will robllback AS400,SQL,SAP Change                    
                    RollBackSPAS400(item);
                    RollBackSAP(item);
                    Log.Information("SAP process for item: " + item + " Failed");
                    Utility.SendNotificationToUser(item.tsdr, item.productCode,
                       "Error: SpeedProcessing.BackendService SAP Process ", $"{item.tsdr} , {item.productCode}  Error: {res}");
                    continue;
                }
                Log.Information("SAP process for item: " + item + " Done");
                // SQL
                resstr = "Success";
                //ProcessSPSQL(item);
                if (resstr != "Success")
                {   // might not need this roll back 
                    // will robllback AS400,QL Change
                    RollBackSPAS400(item);
                    RollBackSAP(item);
                    Log.Information("SQL process for item: " + item + " Failed");
                    Utility.SendNotificationToUser(item.tsdr, item.productCode,
                       "Error: SpeedProcessing.BackendService SQL Process ", $"{item.tsdr} , {item.productCode}  Error: {res}");

                    continue;
                }
                Log.Information("SQL process for item: " + item + " Done");

                // call AS400 to move data from temp table to prod table
                MoveAS400StagingDataToProd(item.tsdr, item.productCode);

                // will update flag for current message need to test;
                dal.UpdaeTaskStatus(item, 1);
                Utility.SendNotificationToUser(item.tsdr, item.productCode, "SpeedProcessing.BackendService Process ", $"{item.tsdr} , {item.productCode} have been processed successfully");

            }//end for
            // return res;

        }

        private string RollBackSAP(ProcessObjectEntity item)
        {// will delete -A and -D file from target folder
            string addfile = $"{item.tsdr}-A.txt";
            string delfile = $"{item.tsdr}-D.txt";
            string filePathAdd = Path.Combine(Utility.FolderPath, addfile);
            string filePathDel = Path.Combine(Utility.FolderPath, delfile);
            try
            {
                // Check if the file exists
                DeleteSAPFiles(filePathAdd, filePathDel);
                return "Success";
            }

            catch (Exception err)
            {
                return $"SAP File Rollback error: {err.Message}";

            }


        }

        private static void DeleteSAPFiles(string filePathAdd, string filePathDel)
        {
            if (File.Exists(filePathAdd))
            {
                // Delete the file
                File.Delete(filePathAdd);
                Console.WriteLine($"{filePathAdd} deleted successfully.");
            }

            if (File.Exists(filePathDel))
            {
                // Delete the file
                File.Delete(filePathDel);
                Console.WriteLine($"{filePathDel} deleted successfully.");
            }
        }

        public string ProcessSPSAP(ProcessObjectEntity item)
        {
            //return "Success";
            var additems = item.AddEntities;
            var delitems = item.DelEntities;
            string szFiller = new string(' ', 40);
            try
            {
                // create -A file
                foreach (var entry in additems)
                {
                    string szPID = item.tsdr;
                    /*
CREATE TABLE dbo.SpeedProcessing_SAPAddData (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TSDR NVARCHAR(20) NOT NULL, -- szPID_SAP
    ProductCode NVARCHAR(20) NOT NULL, -- szProdCode
    PartNumber NVARCHAR(50), -- szNUM_SAP
    PartName NVARCHAR(50), -- szNAM_SAP
    Qty NVARCHAR(50), -- szQTY_SAP (Consider changing to INT or DECIMAL if numeric)
    PartLevel NVARCHAR(50), -- szLev_SAP
    KittingInstructions NVARCHAR(50), -- szKITL
    StorageLocation NVARCHAR(50) , -- szSTRL
    ModifiedMaterial NVARCHAR(50)  -- szMM
);
                    */
                    string szPID_SAP = (szPID + szFiller).Substring(0, 18);

                    string szNUM = String.Empty;
                    string szMIN = String.Empty;
                    Utility.GetPartMinorFromInput(out szNUM, out szMIN, entry.PartNumber);

                    string szNUM_O = Utility.CheckNull(szNUM).Replace("-", "").Trim();
                    string szNUM_SAP = (szNUM_O + szMIN + szFiller).Substring(0, 18);

                    string szNAM_O = Utility.CheckNull(entry.PartName).Trim();// -- Name
                    szNAM_O = szNAM_O.Replace("\r", "").Replace("\n", "");
                    string szNAM_SAP = (szNAM_O + szFiller).Substring(0, 40);


                    string szQTY_O = entry.Qty.ToString();
                    string szQTY_SAP = (szQTY_O + szFiller).Substring(0, 13);
                    string szLev_SAP = entry.PartLevel;
                    szLev_SAP = (szLev_SAP + szFiller).Substring(0, 2);
                    string szKITL = Utility.CheckNull(entry.KittingInstructions).Trim();
                    string szSTRL = Utility.CheckNull(entry.StorageLocation).Trim();
                    string szMM = Utility.CheckNull(entry.ModifiedMaterial).Trim();
                    string szProdCode = item.productCode;
                    string szColor = "";


                    string line = szPID_SAP + szNUM_SAP + szNAM_SAP + szQTY_SAP +
                        szLev_SAP + (szKITL + szFiller).Substring(0, 30) + (szSTRL + szFiller).Substring(0, 4) +
                        (szMM + szFiller).Substring(0, 18) + (szProdCode + szFiller).Substring(0, 40) +
                        (szColor + szFiller).Substring(0, 10) + "A";
                    Utility.AppendTextToSAPFile(item.tsdr + "-A.txt", line);
                }

                Log.Information(item.tsdr + "-A.txt processed done  ");

                // Process -D file
                if (delitems.Count == 0)
                {
                    Utility.AppendTextToSAPFile(item.tsdr + "-D.txt", "");

                }
                else
                {
                    /*
                    CREATE TABLE dbo.SpeedProcessing_SAPAddData (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TSDR NVARCHAR(20) NOT NULL, -- szPID_SAP
    ProductCode NVARCHAR(20) NOT NULL, -- szProdCode
    PartNumber NVARCHAR(50), -- szNUM_SAP
    PartName NVARCHAR(50), -- szNAM_SAP
    Qty NVARCHAR(50), -- szQTY_SAP (Consider changing to INT or DECIMAL if numeric)
    PartLevel NVARCHAR(50), -- szLev_SAP
    KittingInstructions NVARCHAR(50), -- szKITL
    StorageLocation NVARCHAR(50) , -- szSTRL
    ModifiedMaterial NVARCHAR(50)  -- szMM
);


                    */
                    foreach (var entry in delitems)
                    {
                        string szPID = item.tsdr;

                        string szPID_SAP = (szPID + szFiller).Substring(0, 18);

                        string szNUM = String.Empty;
                        string szMIN = String.Empty;
                        Utility.GetPartMinorFromInput(out szNUM, out szMIN, entry.partnumber);

                        string szNUM_O = Utility.CheckNull(szNUM).Replace("-", "").Trim();
                        string szNUM_SAP = (szNUM_O + szMIN + szFiller).Substring(0, 18);

                        string szNAM_O = Utility.CheckNull(entry.partname).Trim();// -- Name
                        szNAM_O = szNAM_O.Replace("\r", "").Replace("\n", "");
                        string szNAM_SAP = (szNAM_O + szFiller).Substring(0, 40);


                        string szQTY_O = entry.qty.ToString();
                        string szQTY_SAP = (szQTY_O + szFiller).Substring(0, 13);
                        string szLev_SAP = entry.partlevel.ToString();
                        szLev_SAP = (szLev_SAP + szFiller).Substring(0, 2);

                        //filetxt.Writeline(szPID_SAP & szNUM_SAP & szNAM_SAP & szQTY_SAP & SzLev_SAP & Left((SzFiller), 30) & Left((SzFiller), 4) & Left((SzFiller), 18) & Left((SzFiller), 40) & "D")
                        //
                        //string line = szPID_SAP + szNUM_SAP + szNAM_SAP + szQTY_SAP +
                        //    szLev_SAP + (szKITL + szFiller).Substring(0, 30) + (szSTRL + szFiller).Substring(0, 4) +
                        //    (szMM + szFiller).Substring(0, 18) + (szProdCode + szFiller).Substring(0, 40) +
                        //    (szColor + szFiller).Substring(0, 10) + "D";
                        string line = szPID_SAP + szNUM_SAP + szNAM_SAP + szQTY_SAP + szLev_SAP + szFiller.Substring(0, 30) + szFiller.Substring(0, 4) + szFiller.Substring(0, 18) + szFiller.Substring(0, 40) + "D";
                        Utility.AppendTextToSAPFile(item.tsdr + "-D.txt", line);
                    }
                }
                // create sap  del file               
                Log.Information(item.tsdr + "-D.txt processed done ");

                return "Success";


            }
            catch (Exception err)
            {
                string res = $"SAP Process Error:{item.tsdr} :{err.Message}";
                Log.Information(res);
                return res;

            }


        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private string ProcessSPSQL(ProcessObjectEntity item)
        {
            var addentitys = item.AddEntities;
            var delentitys = item.DelEntities;

            var addjsonstr = Newtonsoft.Json.JsonConvert.SerializeObject(addentitys, Utility.jsonsettings);
            var deljsonstr = Newtonsoft.Json.JsonConvert.SerializeObject(delentitys, Utility.jsonsettings);
            return dal.SpeedProcessingBackEndTasks_Insert_Delete(item.tsdr, addjsonstr, deljsonstr);
        }

        private string RollBackSPAS400(ProcessObjectEntity item)

        {
            string res = "Success";
            string deladdsql = $"delete  from libdf7.S002PS where ETPKG='{item.tsdr}' and ETPDC='{item.productCode}'";
            try
            {

                // call delete api to run this script : delete  from libdf7.S002PS where ETPKG='DH1T' and ETPDC='G855'
                var deladdresult = SendPostAS400ApiRequest(Utility.AS400APIDeleteEndPint, deladdsql);
                if (deladdresult.Contains("Success;Success"))
                {
                    return res;
                }
                else
                {
                    return deladdresult;
                }


            }
            catch (Exception err)
            {
                return $"RollBackSPAS400 Error. TSDR:{item.tsdr}. Prodcode:{item.productCode} : {err.Message}";
            }

        }
        /// <summary>
        /// for each item , 
        /// 1. create json str list to insert
        /// 2. call as400 post api witht json str to insert
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private bool ProcessSPAS400(ProcessObjectEntity item)
        {

            // return true;
            /*
            INSERT INTO LIBDF7.S002P_staging 
            (ETPKG,ETTPNO,ETPNO,ETTMIN,ETMIN,ETKJ,ETQTY,ETSAC,ETCPYF,ETSEC,ETPDC,ETACD,ETSIZE,ETCHAD,ETABB,ETITM,ETINS) Values
            ('D7R0','56054UMH7B71','','','','3','1','19','','','G865','561','','','','','' )

             INSERT INTO LIBDF7.S002P_staging (ETPKG,ETTPNO,ETPNO,ETTMIN,ETMIN,ETKJ,ETQTY,ETSAC,ETCPYF,ETSEC,ETPDC,ETACD,ETSIZE,ETCHAD,ETABB,ETITM,ETINS) Values
            ('D7R0','56530U136071','','','','3','1','51','','','G865','565','','','','','' )

             INSERT INTO LIBDF7.S002P_staging (ETPKG,ETTPNO,ETPNO,ETTMIN,ETMIN,ETKJ,ETQTY,ETSAC,ETCPYF,ETSEC,ETPDC,ETACD,ETSIZE,ETCHAD,ETABB,ETITM,ETINS) Values
            ('D7R0','56691U137071','','','','3','1','51','','','G865','565','','','','','' )
            
             INSERT INTO LIBDF7.S002P_staging (ETPKG,ETTPNO,ETPNO,ETTMIN,ETMIN,ETKJ,ETQTY,ETSAC,ETCPYF,ETSEC,ETPDC,ETACD,ETSIZE,ETCHAD,ETABB,ETITM,ETINS) Values
            ('D7R0','57886U136071','','','','3','2','69','','','G865','578','','','','','' )

             INSERT INTO LIBDF7.S002P_staging (ETPKG,ETTPNO,ETPNO,ETTMIN,ETMIN,ETKJ,ETQTY,ETSAC,ETCPYF,ETSEC,ETPDC,ETACD,ETSIZE,ETCHAD,ETABB,ETITM,ETINS) Values
            ('D7R0','461462300071','','A','','3','4','02','','','G865','581','','','','','' )

             INSERT INTO LIBDF7.S002P_staging (ETPKG,ETTPNO,ETPNO,ETTMIN,ETMIN,ETKJ,ETQTY,ETSAC,ETCPYF,ETSEC,ETPDC,ETACD,ETSIZE,ETCHAD,ETABB,ETITM,ETINS) Values
            ('D7R0','58120U361171','','','','3','1','02','','','G865','581','','','','','' )
             
             INSERT INTO LIBDF7.S002P_staging (ETPKG,ETTPNO,ETPNO,ETTMIN,ETMIN,ETKJ,ETQTY,ETSAC,ETCPYF,ETSEC,ETPDC,ETACD,ETSIZE,ETCHAD,ETABB,ETITM,ETINS) Values
            ('D7R0','57886U136071','','','','3','2','69','','','G865','578','','','','','' )

             INSERT INTO LIBDF7.S002P_staging 
            (ETPKG,ETTPNO,ETPNO,ETTMIN,ETMIN,ETKJ,ETQTY,ETSAC,ETCPYF,ETSEC,ETPDC,ETACD,ETSIZE,ETCHAD,ETABB,ETITM,ETINS) Values
            ('D7R0','901700800571','','','','3','2','02','','','G865','581','','','','','' )
             */

            // get additem and deleitem entity
            // var sd= Utility.AS400APIEndPint;
            var additems = item.AddEntities;
            var delitems = item.DelEntities;
            var jsonaddsql = CreateAddEntitiesAS400SQL(item, additems);
            Log.Information($"{item.tsdr},{item.productCode} AS400 Add SqL: {jsonaddsql}");
            var addresult = SendPostAS400ApiRequest(Utility.AS400APIUpdateEndPint, jsonaddsql);
            Log.Information($"{item.tsdr},{item.productCode} SendPostAS400ApiRequest Result: {addresult}");
            if (addresult.Contains("Success;Success"))
            {

                //call liblm7.sj0002ps('DH1T', 'G855')
                //// continue jsondelsql
                //var jsondelsql = CreateDekEntitiesAS400SQL(item, delitems);
                //var delresult = SendPostAS400ApiRequest(Utility.AS400APIUpdateEndPint, jsondelsql);
                //if (delresult.Contains ("Success;Success"))
                //    return true;
                //else
                //{
                //    // call rollback for add , del
                //    RollBackSPAS400(item, true);

                //    return false;
                //}

                // will call as400 prodcure to move staging table soo2ps to soo2p 
                // need to by tsdr and product part_number 
                return true;
            }
            else
            {
                // call rollback for add 
                Log.Information($"{item.tsdr},{item.productCode} rollback AS400 insertion");
                RollBackSPAS400(item);

                return false;
            }

        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <param name="additems"></param>
        /// <returns></returns>
        private string CreateAddEntitiesAS400SQL(ProcessObjectEntity item, List<SPPartData> additems)
        {
            if (additems.Count >= 0)
            {
                //var sql = "INSERT INTO LIBDF7.S002PS(ETPKG,ETTPNO,ETPNO,ETTMIN,ETMIN,ETKJ,ETQTY,ETSAC,ETCPYF,ETSEC,ETPDC,ETACD,ETSIZE,ETCHAD,ETABB,ETITM,ETINS) Values";

                //foreach (var entry in additems)
                //{
                //    if (isfirst)
                //    {
                //        isfirst = false;
                //        // ('DH1T','44112U313071','','B','','21','2','01','','','SD','440','','','','','' )
                //        sql += $"('{item.tsdr}','{entry.PartNumber}')";
                //    }
                //    else
                //    {
                //        //, ('DH1T','44112U313071','','B','','21','2','01','','','SD','440','','','','','' )

                //        sql += $",('{item.tsdr}','{entry.PartNumber}')";

                //    }
                //}
                bool isfirst = true;
                var sqlBuilder = new StringBuilder();
                sqlBuilder.Append("INSERT INTO LIBDF7.S002PS (ETPKG,ETTPNO,ETPNO,ETTMIN,ETMIN,ETKJ,ETQTY,ETSAC,ETCPYF,ETSEC,ETPDC,ETACD,ETSIZE,ETCHAD,ETABB,ETITM,ETINS) Values");
                foreach (var entry in additems)
                {
                    //        INSERT INTO LIBDF7.S002P_staging
                    //        (ETPKG, ETTPNO, ETPNO, ETTMIN, ETMIN, ETKJ, ETQTY, ETSAC, ETCPYF, ETSEC, ETPDC, ETACD, ETSIZE, ETCHAD, ETABB, ETITM, ETINS) Values
                    //('D7R0', '56054UMH7B71', '', '', '',          '3', '1', '19', '', '', 'G865', '561', '', '', '', '', '')

                    /*
                     declare @str nvarchar(max)='[{"tid":"MH7B;561;1901_56054UMH7B71_1_G865",
                    "isadd":1,"model":"MH7B","groupNo":"561","compCode":"19","vari":"01","ser":"",
                    "partNumber":"56054UMH7B71","partName":"WIRE; RR UPR; SUB; BLUE LIGHT","partLevel":"1","qty":"1","kjCode":"3",
                    "oriPartNumber":"","itemcode":"","contents":"","short":"","route":"","remFromitemconverision":"",
                    "fromLocation":"","kittingInstructions":"V-MNA","storageLocation":"MF01","modifiedMaterial":"",
                    "vari2":"","caseType":"1","colorRemove":null,"color":"","isactive":"1","timeStamp":"01/12/2023 09:25:40",
                    "isMulitple":0,"instructionCode":"","isEditable":0,"fcid":"2"}
                    ,{"tid":"G865;565;5102_56530U136071_1_G865","isadd":1,"model":"G865","groupNo":"565","compCode":"51","vari":"02","ser":"","partNumber":"56530U136071","partName":"LAMP ASSY; BLUE","partLevel":"1","qty":"1","kjCode":"3","oriPartNumber":"","itemcode":"","contents":"","short":"","route":"","remFromitemconverision":"","fromLocation":"","kittingInstructions":"","storageLocation":"MF01","modifiedMaterial":"","vari2":"","caseType":"1","colorRemove":null,"color":"","isactive":"1","timeStamp":"12/22/2022 14:30:51","isMulitple":0,"instructionCode":"","isEditable":0,"fcid":"3"},{"tid":"G865;565;5102_56691U137071_1_G865","isadd":1,"model":"G865","groupNo":"565","compCode":"51","vari":"02","ser":"","partNumber":"56691U137071","partName":"BRACKET; BLUE LAMP","partLevel":"1","qty":"1","kjCode":"3","oriPartNumber":"","itemcode":"","contents":"","short":"","route":"","remFromitemconverision":"","fromLocation":"","kittingInstructions":"","storageLocation":"MF01","modifiedMaterial":"","vari2":"","caseType":"1","colorRemove":null,"color":"","isactive":"1","timeStamp":"09/17/2015 08:24:10","isMulitple":0,"instructionCode":"","isEditable":0,"fcid":"3"},{"tid":"G865;578;6902_57886U136071_1_G865","isadd":1,"model":"G865","groupNo":"578","compCode":"69","vari":"02","ser":"","partNumber":"57886U136071","partName":"INDICATOR; LAMP AIM","partLevel":"1","qty":"2","kjCode":"3","oriPartNumber":"","itemcode":"","contents":"","short":"","route":"","remFromitemconverision":"","fromLocation":"","kittingInstructions":"","storageLocation":"MF01","modifiedMaterial":"","vari2":"","caseType":"1","colorRemove":null,"color":"","isactive":"1","timeStamp":"01/16/2023 07:26:24","isMulitple":0,"instructionCode":"","isEditable":0,"fcid":"4"},
                     {"tid":"G865;581;0203_461462300071A_1_G865","isadd":1,"model":"G865","groupNo":"581","compCode":"02","vari":"03","ser":"","partNumber":"461462300071A","partName":"WASHER; PLATE","partLevel":"1","qty":"4","kjCode":"3","oriPartNumber":"","itemcode":"","contents":"","short":"","route":"","remFromitemconverision":"","fromLocation":"","kittingInstructions":"","storageLocation":"MF01","modifiedMaterial":"","vari2":"","caseType":"1","colorRemove":null,"color":"","isactive":"1","timeStamp":"01/07/2019 07:43:03","isMulitple":0,"instructionCode":"","isEditable":0,"fcid":"5"},
                     {"tid":"G865;581;0203_58120U361171_1_G865","isadd":1,"model":"G865","groupNo":"581","compCode":"02","vari":"03","ser":"","partNumber":"58120U361171","partName":"BUZZER ASSY; BACK","partLevel":"1","qty":"1","kjCode":"3","oriPartNumber":"","itemcode":"","contents":"","short":"","route":"","remFromitemconverision":"","fromLocation":"","kittingInstructions":"","storageLocation":"MF01","modifiedMaterial":"","vari2":"","caseType":"1","colorRemove":null,"color":"","isactive":"1","timeStamp":"10/30/2019 06:59:10","isMulitple":0,"instructionCode":"","isEditable":0,"fcid":"5"},{"tid":"G865;581;0203_901190805471_1_G865","isadd":1,"model":"G865","groupNo":"581","compCode":"02","vari":"03","ser":"","partNumber":"901190805471","partName":"","partLevel":"1","qty":"2","kjCode":"3","oriPartNumber":"","itemcode":"","contents":"","short":"","route":"","remFromitemconverision":"","fromLocation":"","kittingInstructions":"","storageLocation":"MF01","modifiedMaterial":"","vari2":"","caseType":"1","colorRemove":null,"color":"","isactive":"1","timeStamp":"01/07/2019 07:43:03","isMulitple":0,"instructionCode":"","isEditable":0,"fcid":"5"},{"tid":"G865;581;0203_901700800571_1_G865","isadd":1,"model":"G865","groupNo":"581","compCode":"02","vari":"03","ser":"","partNumber":"901700800571","partName":"NUT; HEXAGON","partLevel":"1","qty":"2","kjCode":"3","oriPartNumber":"","itemcode":"","contents":"","short":"","route":"","remFromitemconverision":"","fromLocation":"","kittingInstructions":"","storageLocation":"MF01","modifiedMaterial":"","vari2":"","caseType":"1","colorRemove":null,"color":"","isactive":"1","timeStamp":"09/17/2015 08:24:10","isMulitple":0,"instructionCode":"","isEditable":0,"fcid":"5"}]'


                    SELECT *
                    FROM OPENJSON(@str)
                    WITH (
    
                        tid NVARCHAR(50) '$.tid',
	                    partnumber NVARCHAR(50) '$.partNumber',
	                    isadd int '$.isadd',
	                    model NVARCHAR(50) '$.model',
	                    groupNo NVARCHAR(50) '$.groupNo'
                      );


                    */
                    // need to remove minor value from partnumber , eg .461462300071A need to remove A
                    // ETPKG, ETTPNO, ETPNO, ETTMIN, ETMIN, ETKJ, ETQTY, ETSAC, ETCPYF,
                    // ETSEC, ETPDC, ETACD, ETSIZE, ETCHAD, ETABB, ETITM, ETINS

                    // todo list : 1. insert for as400 add; 2. insert for as400 delete 3. rollback as400 add; rollback as400 delete

                    // ETPKG:item.tsdr,ETTPNO:entry.PartNumber withour minor ;
                    string partnumonly = null; //ETTPNO   // ETPNO:entry.OriPartNumber ,
                    string minor = null;//ETTMIN // ETTMIN: minor value from entry.PartNumber;    
                                        //ETMIN :''  no assigment at all , so should always be empty
                                        //ETKJ : kjCode                     entry.KJCode
                                        //ETQTY : qty                     entry.Qty
                                        //ETSAC:                      entry.Vari2
                                        //ETCPYF: ''
                                        //ETSEC:''
                                        //ETPDC: item.productCode
                                        //ETACD :                     entry.CompCode
                                        //ETSIZE:                   entry.Contents

                    //ETCHAD: ''
                    //ETABB :                     entry.Short
                    //ETITM: itemcode ; entry.Itemcode
                    //ETINS: instructionCode : entry.InstructionCode // length 1 

                    //INSERT INTO LIBDF7.S002PS(ETPKG, ETTPNO, ETPNO, ETTMIN, ETMIN, ETKJ, ETQTY, ETSAC, ETCPYF, ETSEC, ETPDC, ETACD, ETSIZE, ETCHAD, ETABB, ETITM, ETINS) Values
                    //('D7R0', '56054UMH7B71', 'or1', 'A', '', '21', '1', '19', 'v2', '', 'G865', '561', 'c1', '', 's1', 'i1', 'L')



                    /*
                     final AS400 SQL old single Insert: INSERT INTO S002P (ETPKG,ETTPNO,ETPNO,ETTMIN,ETMIN,ETKJ,ETQTY,ETSAC,ETCPYF,ETSEC,ETPDC,ETACD,ETSIZE,ETCHAD,ETABB,ETITM,ETINS) Values
('D7R0','02565UMH7B71','ORIPARTVALUE','A','','21','1','var2vlaue','','','G865','010','content1','','shortvalue','item1','insvalue' 

SQL DB SQL : EXECUTE [dbo].[InsertSpeedProcessing_Part_Data] 1,'MH7B','010','var2vlaue','01','','02565UMH7B71A','IN; REV ACT BLUE SPOT LAMP','1_G865','1','21','','','content1','shortvalue','routvalue1','removevalue','fromvalue','kitvalue','MF01','mmvalue','var2vlaue','colorvlaue' ,'D7R0','item1','insvalue',0
                     */

                    Utility.GetPartMinorFromInput(out partnumonly, out minor, entry.PartNumber);
                    if (isfirst)
                    {
                        isfirst = false;
                        sqlBuilder.Append($"('{item.tsdr}','{partnumonly}','{entry.OriPartNumber}','{minor}','','{entry.KJCode}','{entry.Qty}','{entry.Vari2}'" +
                          $",'','','{item.productCode}','{entry.CompCode}','{entry.Contents}','','{entry.Short}','{entry.Itemcode}','{entry.InstructionCode}')");
                    }
                    else
                    {
                        sqlBuilder.Append($",('{item.tsdr}','{partnumonly}','{entry.OriPartNumber}','{minor}','','{entry.KJCode}','{entry.Qty}','{entry.Vari2}'" +
                           $",'','','{item.productCode}','{entry.CompCode}','{entry.Contents}','','{entry.Short}','{entry.Itemcode}','{entry.InstructionCode}')");
                        // sqlBuilder.Append($",('{item.tsdr}','{entry.PartNumber}')");
                    }
                }

                var sql = sqlBuilder.ToString();
                if (sql.Length > 0)
                {
                    sql = Utility.RemoveExtraSpace(sql);
                }
                Log.Information(sql);
                //string jsonsql = "{\"sql\": \"" + sql + "\"}";
                //sql = $"{{\"sql\": \"{sql}\"}}";                
                return sql;
            }
            else
            {
                return null;
            }
        }

        public string SendPostAS400ApiRequest(string strUrl, string sql)
        {

            //strUrl = "http://localhost:54618/api/Data?server=t&processfilename=test";
            //sql = "INSERT INTO LIBDF7.S002PS(ETPKG,ETTPNO,ETPNO,ETTMIN,ETMIN,ETKJ,ETQTY,ETSAC,ETCPYF,ETSEC,ETPDC,ETACD,ETSIZE,ETCHAD,ETABB,ETITM,ETINS) Values  ('DH1T','44112U313071','','B','','21','2','01','','','SD','440','','','','','' )";
            //sql += ",('DH1T','94112U313071','','B','','21','2','01','','','SD','440','','','','','' )";
            //sql += ",('D7R0','57886U136071','','B','','3','2','69','','','G865','578','','','','','' )";
            WebRequest reqObject = WebRequest.Create(strUrl);
            reqObject.Method = "POST";
            reqObject.ContentType = "application/json";
            string postData = "{\"sql\": \"" + sql + "\"}";
            byte[] byteArray = Encoding.UTF8.GetBytes(postData);
            reqObject.ContentLength = byteArray.Length;
            Stream dataStream = reqObject.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();
            var response = reqObject.GetResponse();
            var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
            var S002Pstatus = reader.ReadToEnd();
            reader.Close();
            return S002Pstatus; ;
        }

        public string MoveAS400StagingDataToProd(string tsdr, string prodcode)
        {
            //https://linuxserviceas400.production.toyotatmh.io/AS400Api?servername=t&sql=CALL%20LIBLM7.SPS002LOAD%20%28%27DsdH1T%27%2C%27G855%27%29
            // SendGetAS400ApiRequest($"https://linuxserviceas400.production.toyotatmh.io/AS400Api?servername=t&sql=CALL LIBLM7.SPS002LOAD ('DH1T','G855')");
            //https://colwebdev01.toyotatmh.io/as400readwolog/api/Data?server=t&statement=
            // Utility.AS400APIEndPint
            //  return true;
            ///  string tsdr = "DH1T";
            ///  string prodcode = "G855";
            var strUrl = $"{Utility.AS400APIEndPint}CALL LIBLM7.SPS002LOAD ('{tsdr}','{prodcode}')";
            WebRequest reqObject = WebRequest.Create(strUrl);

            reqObject.Method = "GET";

            var response = reqObject.GetResponse();

            var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);

            var apiResponse = reader.ReadToEnd();

            reader.Close();

            return apiResponse;

        }

        internal string GetProductCode(string tsdr)
        {   // need to get production part_number of this package
            // 1. get order of this package
            //SELECT top 1 [OrderNumber] FROM [ETA].[dbo].[OrderNumbers] where DesignNumber='EL2T'
            //2. get product part_number
            //select adpdc from libdf7.A004P where adodn='1219213' fetch first row only
            string res = null;

            string order = dal.GetOrderByPackage(tsdr);
            if (!string.IsNullOrEmpty(order))
            {
                string sql = $"select adpdc from libdf7.A004P where adodn='{order}' fetch first row only";
                var strUrl = $"https://linuxserviceas400.production.toyotatmh.io/AS400Api?servername=p&sql=select%20adpdc%20from%20libdf7.A004P%20where%20adodn%3D%27{order}%27%20fetch%20first%20row%20only";
                //$"{Utility.AS400APIEndPint}{sql}";
                WebRequest reqObject = WebRequest.Create(strUrl);

                reqObject.Method = "GET";

                var response = reqObject.GetResponse();

                var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);

                var apiResponse = reader.ReadToEnd();

                var items = System.Text.Json.JsonSerializer.Deserialize<dynamic[]>(apiResponse);
                if (items.Length > 0)
                {
                    res = items[0].GetProperty("adpdc").ToString();
                }

            }

            return res;
        }
        /// <summary>
         /*
         1. get all unprocessed TSDR SAP ADD and Delete list from SQL Server SpeedProcesssing Tables with IsSubmitted falag is 0: 
            [dbo].[SpeedProcessing_SAPAddData](Add table), [dbo].[SpeedProcessing_SAPDelData](Delete Table)
         2. Create result pair  of Addlist and dellist by TSDR, Productcode

         3. foreach pair of result list from second step , Call SAP Api to modify dellist
        
         4. foreach pair of rueslt  list from second step , create package-A, package-D files and saved to EDIProcessed Folder

         5. Update SQL Server SpeedProcesssing Tables  IsSubmitted falag to 1 
         */
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        internal async Task RDDSAPProcessAsync()
        {
            //1. get all unprocessed TSDR SAP ADD and Delete list from SQL Server SpeedProcesssing Tables with IsSubmitted falag is 0: 
            //[dbo].[SpeedProcessing_SAPAddData](Add table), [dbo].[SpeedProcessing_SAPDelData](Delete Table)
            //2.Create result pair  of Addlist and dellist by TSDR, Productcode
            List<RDDSAPSQLResult> rDDSAPResults = dal.GetSPSAPAddDelData();
            string jsonString = JsonConvert.SerializeObject(rDDSAPResults);
            Log.Information($"SPSAPAddDelData Size: {rDDSAPResults.Count}");
            // Log.Information($"SPSAPAddDelData: {jsonString}");
            // 3. foreach pair of result list from second step , Call SAP Api to modify dellis
            foreach (var item in rDDSAPResults)
            {
                await Task.Delay(1500);

                // call sap api to modify 
                RDDSAPApiResult apires = GetSAPAPIResult(item.tsdr);

                if (apires == null || apires.message.Contains("Error"))
                {
                    // will notify group for this item
                    string subject = $"SpeedProcessing.RDDSAP Process {item.tsdr};{item.productCode} Failed";
                    Utility.SendNotificationToUser(item.tsdr, item.productCode, subject, $" Please check. No SAP file created .  Detail:{apires.message}");
                    continue;
                }

                Log.Information($"{item.tsdr} SAP API call 1.5 seconds delayed : {apires.message} . Count : {apires.components.Count}");

                //4. foreach pair of rueslt  list from second step , create package-A, package-D files and saved to EDIProcessed Folder
                foreach (var delpart in apires.components)
                {
                    SpeedProcessingSAPDelData entry = new SpeedProcessingSAPDelData();
                    entry.TSDR = item.tsdr;
                    entry.PartNumber = delpart.part_number;
                    var partdetail = GetPartNameByPartNumber(entry.PartNumber);
                    if (partdetail == null || string.IsNullOrEmpty(partdetail.PartName))
                    {
                        entry.PartName = "";
                        Log.Information($"{entry.PartNumber} name is null");
                    }
                    else
                    {

                        entry.PartName = partdetail.PartName;
                    }

                    entry.Qty = delpart.quanitity.ToString();
                    item.delpartlist.Add(entry);


                }

            }
            List<ProcessObjectEntity> finallist = ConvertSAPListToSAPFileList(rDDSAPResults);

            foreach (var finalitem in finallist)
            {
                /// ProcessSPSAP(testentity) // create -A,-D file
                if (ProcessSPSAP(finalitem).Contains("Error"))

                {
                    // error handling ;
                }
                else
                {
                    // Update SQL Server SpeedProcesssing Tables IsSubmitted falag to 1
                    Log.Information($"Update SQL Server SpeedProcesssing Tables IsSubmitted falag to 1 : {finalitem.tsdr} ; {finalitem.productCode}");

                    dal.UpdateSPSAPAddDelData(finalitem.tsdr, finalitem.productCode);
                }

            }



        }

        private PartDetail GetPartNameByPartNumber(string partNumber)
        {
            return dal.GetPartNameByPartNumber(partNumber);
        }

        private List<ProcessObjectEntity> ConvertSAPListToSAPFileList(List<RDDSAPSQLResult> rDDSAPResults)
        {
            List<ProcessObjectEntity> res = new List<ProcessObjectEntity>();

            foreach (var item in rDDSAPResults)
            {
                ProcessObjectEntity entity = new ProcessObjectEntity();

                entity.tsdr = item.tsdr;
                entity.productCode = item.productCode;
                entity.AddEntities = new List<SPPartData>();
                entity.DelEntities = new List<SPDelPartData>();


                //public List<SPPartData> AddEntities ; public List<SpeedProcessingSAPAddData> addpartlist
                // entity.AddEntities = item.addpartlist;
                foreach (var addpart in item.addpartlist)
                {
                    SPPartData spadd = new SPPartData();
                    spadd.PartNumber = addpart.PartNumber;
                    spadd.PartName = addpart.PartName;
                    spadd.Qty = addpart.Qty;

                    entity.AddEntities.Add(spadd);
                }

                foreach (var delpart in item.delpartlist)
                {
                    SPDelPartData spdel = new SPDelPartData();
                    spdel.partnumber = delpart.PartNumber;
                    spdel.partname = delpart.PartName;
                    spdel.qty = delpart.Qty;

                    entity.DelEntities.Add(spdel);
                }

                res.Add(entity);
            }

            return res;
        }

        public RDDSAPApiResult GetSAPAPIResult(string tsdr)
        {
            RDDSAPApiResult result;


            try
            {
                // /*
                using var httpClient = new HttpClient();

                // Add the API key to the request headers (commonly in "Authorization" or "x-api-key")
                httpClient.DefaultRequestHeaders.Add("apikey", Utility.SAPAPIKey);

                // Make the GET request
                var response = httpClient.GetAsync($"{Utility.SAPAPIEndPoint}{tsdr}").Result;
                // Ensure the response is successful
                response.EnsureSuccessStatusCode();

                // Read and print the response content
                var responseData = response.Content.ReadAsStringAsync().Result;



                result = JsonConvert.DeserializeObject<RDDSAPApiResult>(responseData);

                //  result.message = "200";
                Console.WriteLine("Response data:");
                Console.WriteLine(responseData);
            }

            catch (Exception ex)
            {
                result = new RDDSAPApiResult();
                result.message = $"Error:{ex.Message}";
                result.components = new List<RDDSAPApiPartData>();
                Console.WriteLine($"Error: {ex.Message}");
                Log.Information($"An error occurred: {ex.Message}");
            }

            return result;
        }

        internal List<Querystring> GetETSACDailyList()
        {
            // Querystring res = new Querystring();
            // res.querystring = " SELECT ettpno as querystring FROM libdf7.S002P fetch first 2 rows only;";
            //List<Querystring> reslist =new List<Querystring>();
            // reslist.Add(res);
            // return reslist;

            return dal.GetETSACDailyList();
        }

        internal List<TipsPartDrawing> GetTipPartDraws()
        {
            return dal.GetTipPartDraws();
        }

        #endregion
        /// <summary>
        /*
                            {
  "tsdr": "TS-12345",
  "productCode": "PC-67890",
  "partDetailList": [
    {
      "partNumber": "51607U116171A",
      "partName": "Gear Assembly",
      "qty": 10
     
    },
    {
      "partNumber": "PN-1002",
      "partName": "Bolt Set",
      "qty": 5
     
    }
  ]
}
         */
        /// </summary>
        /// 

        public void Process()
        {
            try
            {
                var sw = new Stopwatch();
                sw.Start();
                List<string> result = _rabbitMQService.DequeBatchMessages(AppUtility.DEQUEUEMAXCOUNT);
                List<ProcessObjectEntity> res = new List<ProcessObjectEntity>();
                if (result.Count > 0 && !result[0].Contains("Error"))
                {
                    List<SpeedProcessingRDDPart> messagelist = result.Select(jsonString =>
                        JsonConvert.DeserializeObject<SpeedProcessingRDDPart>(jsonString)).ToList();
                    if (messagelist.Count == 0)

                        return;



                    _logger.LogInformation("get process tsdr/product code list");

                    // Step 1: Assign storagelocation value
                    foreach (var item in messagelist)
                    {
                        ProcessObjectEntity entity = new ProcessObjectEntity();
                        entity.AddEntities = new List<SPPartData>();
                        entity.DelEntities = new List<SPDelPartData>();
                        entity.tsdr = messagelist[0].tsdr;
                        entity.productCode = messagelist[0].productCode;

                        SPPartData spadd = new SPPartData();

                        foreach (var partdetail in item.partDetailList)
                        {


                            //string partnumber = partdetail.partNumber;
                            // 
                            string storelocationvalue = dal.GetSPPartstorageLocation(partdetail.partNumber);

                            if (string.IsNullOrEmpty(storelocationvalue))
                            {
                                partdetail.storageLocation = "MF01"; // default value
                                // save this entry to db table
                                dal.SaveRDDPartToReview(partdetail, item.tsdr, item.productCode);
                            }
                            else
                            {
                                partdetail.storageLocation = storelocationvalue;
                            }
                            spadd.PartNumber = partdetail.partNumber;

                            spadd.PartName = partdetail.partName;
                            spadd.Qty = partdetail.qty.ToString();
                            spadd.PartLevel = partdetail.partLevel.ToString();
                            spadd.KittingInstructions = partdetail.kittingInstructions;
                            spadd.StorageLocation = partdetail.storageLocation;
                            spadd.ModifiedMaterial = partdetail.modifiedMaterial;
                            //string szTimeStarted = DateTime.Now.ToString("yyyyMMddHHmmss");
                            //string szTimeStopped = DateTime.Now.ToString("yyyyMMddHHmmss");
                            //= szTimeStarted + szTimeStopped;
                            spadd.SzTimeLine = $"{DateTime.Now.ToString("yyyyMMddHHmmss")}{DateTime.Now.ToString("yyyyMMddHHmmss")}";
                            entity.AddEntities.Add(spadd);

                        }

                        res.Add(entity);

                    }
                    _logger.LogInformation("get process tsdr/product code list done");

                    // Step2 . 
                    // Create Create SP -A -D file with res; 

                    _logger.LogInformation("create -A -D file for each tsdr/productcode in res");

                    foreach (var finalitem in res)
                    {
                        /// ProcessSPSAP(testentity) // create -A,-D file
                        string returnmessage = ProcessSPSAP(finalitem);
                        if (returnmessage.Contains("Error"))

                        {
                            // error handling ;
                            // notification

                            EmailSender.SendEmail("noreply@toyotatmh.com",
                   "Dayang.Sun@toyotatmh.com,Navaneeth.Ponnam@toyotatmh.com",
                   $"SpeedProcessing RDD Part Process Failed : {finalitem.tsdr};{finalitem.productCode}   @" + DateTime.Now.ToString(),
                   returnmessage
                    );
                        }


                    }


                    sw.Stop();

                    string output =
                        $"Batch dequeue of {messagelist.Count} messages took {sw.ElapsedMilliseconds / 1000.0:F3}s. Result: {result}";
                    _logger.LogInformation(output);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing log message");
                // Rethrow the exception to be handled by the caller if needed
            }


        }
    }
}

