using Code.Models;
using Common.Utility;
using Microsoft.Extensions.Logging;
using Model;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

public interface IRabbitMQService
{
    void ProcessQueueTaskList(List<STQueueMessage> tasklist, bool ishighestpriority);

  void ProcessLogMessageTaskList(List<LogMessage> tasklist, bool ishighestpriority);

    bool PushMessageToQueue(bool ishighestpriority, IModel chanel, string message);

    string DequeMessages(int maxcount);

    List<string> DequeBatchMessages(int maxcount);
}

public class RabbitMQService : IRabbitMQService
{
    private readonly string queueName;

    private readonly ILogger<RabbitMQService> logger;

    /// <summary>
    /// QueueFactory.
    /// </summary>
    private static ConnectionFactory QueueFactory = new ConnectionFactory()
    {

        UserName =
        //"smartteamprioritytaskqueue",
       AppUtility.RabbitMQDict["UserName"],
        Password = //"75kUBqvp7ttZhWfQsLVq",
         AppUtility.RabbitMQDict["Password"],
        VirtualHost =  // "/",  // "smartteamprioritytaskqueue", 
         AppUtility.RabbitMQDict["VirtualHost"],
        Protocol = Protocols.DefaultProtocol,
        HostName = //"rabbitmq.toyotatmh.io",
         AppUtility.RabbitMQDict["HostName"]
             ,
        Port =//5672
        Int32.Parse(AppUtility.RabbitMQDict["QueuePort"])
        //5672,
    };

    public RabbitMQService(string queuename, ILogger<RabbitMQService> _logger)
    {
        this.logger = _logger;
        this.queueName = queuename;
    }




    /// <summary>
    /// 
    /// </summary>
    /// <param name="tasklist"></param>
    /// <param name="ishighestpriority"></param>
    public void ProcessQueueTaskList(List<STQueueMessage> tasklist, bool ishighestpriority = false)
    {
        using (var connection = QueueFactory.CreateConnection())
        {
           // logger.LogDebug("in PublishQueueTaskList without DI from applicationlog");
            using (var channel = connection.CreateModel())
            {

                IDictionary<String, Object> args1 = new Dictionary<String, Object>();

                foreach (STQueueMessage item in tasklist)
                {
                    string entmessage = JsonConvert.SerializeObject(item);
                    // check this item need to be inqueue or not by time
                  //  logger.LogDebug(item.Type + " at " + item.Runtime + " " + item.TaskInQueue.ToString() + " will be processed");

                    // will only add task to queue once during time range
                    this.PushMessageToQueue(ishighestpriority, channel, entmessage);



                }

            }

        }
    }

    /// <summary>
    /// Push Message To Queue.
    /// </summary>
    /// <param name="ishighestpriority"></param>
    /// <param name="channel"></param>
    /// <param name="entmessage"></param>
    public bool PushMessageToQueue(bool ishighestpriority, IModel channel, string entmessage)
    {
        try

        {
            var body = Encoding.UTF8.GetBytes(entmessage);
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            if (ishighestpriority)
            {
                properties.Priority = Convert.ToByte(AppUtility.RabbitMQDict["QueueMaxPriority"]);
            }
            else
            {
                properties.Priority = Convert.ToByte(1);
            }


            channel.ConfirmSelect();
            channel.BasicPublish("", this.queueName, properties, body);
            channel.WaitForConfirmsOrDie();
            return true;
        }
        catch (Exception err)
        {
            return false;
        }

    }



    /// <summary>
    /// 
    /// </summary>
    /// <param name="factory"></param>
    /// <param name="queuename"></param>
    /// <returns></returns>
    public bool PeekQueueMessage(IModel channel, string queuename)
    {
        bool hasmessage = true;
        QueueDeclareOk result = channel.QueueDeclarePassive(queuename);
        uint count = result != null ? result.MessageCount : 0;
        if (count == 0)
            hasmessage = false;
        return hasmessage;
    }

    public string DequeFirst()
    {
        string res = null;
        try
        {
            using (var connection = QueueFactory.CreateConnection())
            {
              //  logger.LogDebug("in PublishQueueTaskList without DI from applicationlog");
                using (var channel = connection.CreateModel())
                {
                    bool noAck = false;
                    BasicGetResult result = channel.BasicGet(this.queueName, noAck);
                    if (result == null)
                        res = "No Message in Queue";
                    else
                    {
                        // dequeu message
                        IBasicProperties props = result.BasicProperties;
                        byte[] body = result.Body;
                        var message = Encoding.UTF8.GetString(body);
                        channel.BasicAck(result.DeliveryTag, false);
                        res = "First Message Dequeued:" + message;
                    }
                }
            }
        }
        catch (Exception err)
        {
            res = "Error:" + err.Message;
        }
        return res;
    }

    public List<string> DequeBatchMessages(int maxcount)
    {
       // string res = null;
        var messagesList = new List<string>();

        try
        {
            using (var connection = QueueFactory.CreateConnection())
            {
               // logger.LogDebug($"Attempting to dequeue {maxcount} messages");
                using (var channel = connection.CreateModel())
                {
                    // Set prefetch count to match maxcount
                    channel.BasicQos(prefetchSize: 0, prefetchCount: (ushort)maxcount, global: false);

                    var messagesReceived = 0;
                    var consumer = new EventingBasicConsumer(channel);

                    consumer.Received += (model, ea) =>
                    {
                        if (messagesReceived < maxcount)
                        {
                            var body = ea.Body;
                            var message = Encoding.UTF8.GetString(body.ToArray());
                            messagesList.Add(message);
                            channel.BasicAck(ea.DeliveryTag, false);
                            Interlocked.Increment(ref messagesReceived);
                        }
                    };

                    string consumerTag = channel.BasicConsume(queue: this.queueName,
                                                            autoAck: false,
                                                            consumer: consumer);

                    // Wait until we get desired count or timeout
                    var timeout = TimeSpan.FromSeconds(5);
                    var startTime = DateTime.UtcNow;

                    while (messagesReceived < maxcount && DateTime.UtcNow - startTime < timeout)
                    {
                        Thread.Sleep(100);
                    }

                    // Cancel consumer after we're done
                    channel.BasicCancel(consumerTag);
                }
            }

            //if (messagesList.Count == 0)
            //    res = "";
            ////"No Messages in Queue";
            //else
            //    res  = JsonConvert.SerializeObject(messagesList);

            return messagesList;
            //$"Requested {maxcount} messages, dequeued {messagesList.Count} messages: {string.Join(", ", messagesList)}";
        }
        catch (Exception err)
        {
            return new List<string> {$"Error: {err.Message}" };
        }
        
    }
    public string DequeMessages(int maxcount)
    {
        string res = null;
        var messagesList = new List<string>();

        try
        {
            using (var connection = QueueFactory.CreateConnection())
            {
              //  logger.LogDebug($"Attempting to dequeue {maxcount} messages");
                using (var channel = connection.CreateModel())
                {
                    int count = 0;
                    while (count < maxcount)
                    {
                        BasicGetResult result = channel.BasicGet(this.queueName, false);
                        if (result == null)
                        {
                            break;
                        }

                        byte[] body = result.Body;
                        var message = Encoding.UTF8.GetString(body);
                        messagesList.Add(message);

                        channel.BasicAck(result.DeliveryTag, false);
                        count++;
                    }
                }
            }

            if (messagesList.Count == 0)
                res = "No Messages in Queue";
            else
                res = $"Dequeued {messagesList.Count} messages: {string.Join(", ", messagesList)}";
        }
        catch (Exception err)
        {
            res = "Error:" + err.Message;
        }
        return res;
    }
    public void ProcessLogMessageTaskList(List<LogMessage> tasklist, bool ishighestpriority)
    {
        using (var connection = QueueFactory.CreateConnection())
        {
          //  logger.LogDebug("in ProcessLogMessageTaskList without DI from applicationlog");
            using (var channel = connection.CreateModel())
            {

                IDictionary<String, Object> args1 = new Dictionary<String, Object>();

                foreach (var item in tasklist)
                {
                    string entmessage = JsonConvert.SerializeObject(item);

                    // check this item need to be inqueue or not by time
                   // logger.LogDebug(entmessage);
                    // will only add task to queue once during time range
                    this.PushMessageToQueue(ishighestpriority, channel, entmessage);



                }

            }

        }
    }
}
