#load "QnAMakerResult.csx"

using System;
using System.Threading.Tasks;
using System.Net;
using Microsoft.Bot.Builder.Azure;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;

[Serializable]
public class LuisDialog : LuisDialog<object>
{
    public LuisDialog() : base(new LuisService(new LuisModelAttribute(Utils.GetAppSetting("LuisAppId"), Utils.GetAppSetting("LuisAPIKey"))))
    {
    }

    [LuisIntent("None")]
    public async Task NoneIntent(IDialogContext context, LuisResult result)
    {
        await context.PostAsync($"You have reached the none intent. You said: {result.Query}");
        context.Wait(MessageReceived);
    }

    [LuisIntent("Query")]
    public async Task QueryIntent(IDialogContext context, LuisResult result)
    {
        await context.Done(result);
    }
    
}