namespace Model
{
    using System;

    //public enum STTaskType
    //{
    //    //AUTOPACKAGE = 0,
    //    //QALABSCANNING = 1, 
    //    //RELEASEECI = 2,
    //    //STARTECI=3,
    //    //RDDWORKFLOW =4,
    //    //ECIPUBLISH =5

    //    AutoPackage = 0,
    //    QALabScanning = 1,
    //    ReleaseEci = 2,
    //    StartReleaseEci = 3,

    //    RDDWorkFlow = 4,
    //    EciPublish = 5,
    //    TSDWorkFlow=6,
    //    PackageRelease=7
    //   // ,ManualAssignPackage=8
        

    //}

    /// <summary>
    ///
    /// </summary>
    public enum STQueueMessageType
    {
        TaskCategory = 0,
        TaskDetail = 1
    }
;


    /// <summary>
    ///
    /// </summary>
    public class STQueueMessage
    {
        public string Type { get; set; }

        public string Runtime { get; set; }

        public STQueueMessageType MessageType { get; set; }

        public int TaskPriority { get; set; }

        public bool TaskInQueue { get; set; }

        public DateTime? LastQueueTime { get; set; }

        // Json data for task detail
        public string Data { get; set; }




    }

}
