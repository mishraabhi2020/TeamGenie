using Newtonsoft.Json;
using Microsoft.Bot.Connector;

public class QnAMakerUpdate : Activity
{
    public string Answer { get; set; }

    public string Question { get; set; }


}