using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SpeedProcessing.BackEndService.Model
{

    #region TipsDrawing

    public class TipsPartDrawing
    {
        public string PartNo { get; set; }
        public string Minor { get; set; }
        public string NewPart { get; set; }
        public string FileName { get; set; }


    }

    #endregion

    #region RDDPartProcess
    public class SpeedProcessingRDDPart
    {
        public string tsdr { get; set; }

        public string productCode { get; set; }

        public List<SpeedProcessingRDDPartDetail> partDetailList { get; set; }

    }
    public class SpeedProcessingRDDPartDetail

    {
        public string partNumber { get; set; }
        public string partName { get; set; }
        public int qty { get; set; }
        public int partLevel { get; set; } = 1;
        public string kittingInstructions { get; set; } = "";
        public string storageLocation { get; set; }
        public string modifiedMaterial { get; set; } = "";

    }


        #endregion

    #region RDDSAPProcess

        public class SpeedProcessingSAPAddData
    {
        public int Id { get; set; }
        public string TSDR { get; set; }
        public string ProductCode { get; set; }
        public string PartNumber { get; set; }
        public string PartName { get; set; }
        public string Qty { get; set; }
        public string PartLevel { get; set; }
        public string KittingInstructions { get; set; }
        public string StorageLocation { get; set; }
        public string ModifiedMaterial { get; set; }
        public int IsSubmitted { get; set; }
        public string SzTimeLine { get; set; }
        public DateTime? TimeStamp { get; set; }
    }


    public class PartDetail

    {
        //PartNo
        //51601UC98671
        public string PartNo { get; set; }
        public string PartName { get; set; }

    }


    public class SpeedProcessingSAPDelData
    {
        public int Id { get; set; }
        public string TSDR { get; set; }
        public string PartNumber { get; set; }
        public string PartName { get; set; }
        public string Qty { get; set; }
        public string PartLevel { get; set; }
        public int IsSubmitted { get; set; }
        public DateTime? TimeStamp { get; set; }
    }
    #endregion



    //internal class AddEntity
    //{
    //    public string Name { get; set; }
    //}


    //internal class DelEntity
    //{
    //    public string Name { get; set; }
    //}

    internal class ProcessObjectEntity
    {
        public List<SPPartData> AddEntities { get; set; }

        public List<SPDelPartData> DelEntities { get; set; }

        public string tsdr { get; set; }

        public string productCode { get; set; }

    }
    public class SPPartData_new
    {
        public string tid { get; set; }
        public int isadd { get; set; }
        public string model { get; set; }
        public string groupNo { get; set; }
        public string compCode { get; set; }
        public string vari { get; set; }
        public string ser { get; set; }
        public string partNumber { get; set; }
        public string partName { get; set; }
        public string partLevel { get; set; }
        public string qty { get; set; }
        public string kjCode { get; set; }
        public string oriPartNumber { get; set; }
        public string itemcode { get; set; }
        public string contents { get; set; }
        public string @short { get; set; }
        public string route { get; set; }
        public string remFromitemconverision { get; set; }
        public string fromLocation { get; set; }
        public string kittingInstructions { get; set; }
        public string storageLocation { get; set; }
        public string modifiedMaterial { get; set; }
        public string vari2 { get; set; }
        public string caseType { get; set; }
        public string colorRemove { get; set; }
        public string color { get; set; }
        public string isactive { get; set; }
        public string timeStamp { get; set; }
        public int isMulitple { get; set; }
        public string instructionCode { get; set; }
        public int isEditable { get; set; }
        public string fcid { get; set; }
    }

    public class RDDSAPSQLResult

    {
        public string tsdr { get; set; }

        public string productCode { get; set; }

        public List<SpeedProcessingSAPAddData> addpartlist { get; set; }

        public List<SpeedProcessingSAPDelData> delpartlist { get; set; }


        // public List<RDDSAPData> partdellist { get; set; }


    }
    public class RDDSAPApiResult

    {
        public string message { get; set; }

        public List<RDDSAPApiPartData> components { get; set; }


    }
    public class RDDSAPApiPartData
    {
        // public bool iSADD { get; set; }
        public string part_number { get; set; }
        public int quanitity { get; set; }
    }

    public class SPPartData
    {
        public string TID { get; set; }
        public int ISADD { get; set; }
        public string Model { get; set; }
        public string GroupNo { get; set; }
        public string CompCode { get; set; }
        public string Vari { get; set; }
        public string Ser { get; set; }
        public string PartNumber { get; set; }
        public string PartName { get; set; }
        public string PartLevel { get; set; }
        public string Qty { get; set; }
        public string KJCode { get; set; }
        public string OriPartNumber { get; set; }
        public string Itemcode { get; set; }
        public string Contents { get; set; }
        public string Short { get; set; }
        public string Route { get; set; }
        public string RemFromitemconverision { get; set; }
        public string FromLocation { get; set; }
        public string KittingInstructions { get; set; } = "";
        public string StorageLocation { get; set; }
        public string ModifiedMaterial { get; set; }
        public string Vari2 { get; set; }
        public string CaseType { get; set; }
        public string ColorRemove { get; set; }
        public string Color { get; set; }
        public string Isactive { get; set; }
        public string TimeStamp { get; set; }
        public int IsMulitple { get; set; }
        public string InstructionCode { get; set; }
        public int IsEditable { get; set; } = 0;
        public string fcid { get; set; }

        public string SzTimeLine { get; set; }

    }
    public class test
    {
        public string ettpno { get; set; }

    }

    public class Querystring

    {

        public string querystring { get; set; }
    }
    public class SPDelPartData
    {
        public bool ischecked { get; set; }
        public string pagenumber { get; set; }
        public string partnumber { get; set; }
        public string partname { get; set; }
        public int partlevel { get; set; }
        public string qty { get; set; }
        public string comment { get; set; }
        public int fcitemid { get; set; }
        public string identification { get; set; }
    }
    public class SPUnprocessTask
    {
        //public Int64 id { get; set; }
        public string AddJson { get; set; }
        public string tsdr { get; set; }
        public string productCode { get; set; }

        //public string timestamp { get; set; }
        public string DelJson { get; set; }

        //public int isprocessed { get; set; }

        //  AddJson DelJson tsdr productCode

    }

}
