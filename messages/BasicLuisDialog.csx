#load "QnAMakerResult.csx"

using System;
using System.Threading.Tasks;
using System.Net;
using Microsoft.Bot.Builder.Azure;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using Newtonsoft.Json;

// For more information about this template visit http://aka.ms/azurebots-csharp-luis
[Serializable]
public class BasicLuisDialog : LuisDialog<object>
{
    public BasicLuisDialog() : base(new LuisService(new LuisModelAttribute(Utils.GetAppSetting("LuisAppId"), Utils.GetAppSetting("LuisAPIKey"))))
    {
    }

    [LuisIntent("None")]
    public async Task NoneIntent(IDialogContext context, LuisResult result)
    {
        await context.PostAsync($"You have reached the none intent. You said: {result.Query}"); //
        context.Wait(MessageReceived);
    }

    [LuisIntent("Query")]
    public async Task QueryIntent(IDialogContext context, LuisResult result)
    {

        string responseString = string.Empty;
        var query = result.Query; //User Query
        var knowledgebaseId = Utils.GetAppSetting("QnAKnowledgeBaseId"); 
        var qnamakerSubscriptionKey = Utils.GetAppSetting("QnASubscriptionKey");

        //Build the URI
        Uri qnamakerUriBase = new Uri("https://westus.api.cognitive.microsoft.com/qnamaker/v1.0");
        var builder = new UriBuilder($"{qnamakerUriBase}/knowledgebases/{knowledgebaseId}/generateAnswer");

        //Add question as part of the body
        var postBody = $"{{\"question\": \"{query}\"}}";

        //Send the POST request to QnAMaker API
        using (WebClient client = new WebClient())
        {
            //Set the encoding to UTF8
            client.Encoding = System.Text.Encoding.UTF8;

            //Add the subscription key header
            client.Headers.Add("Ocp-Apim-Subscription-Key", qnamakerSubscriptionKey);
            client.Headers.Add("Content-Type", "application/json");
            responseString = client.UploadString(builder.Uri, postBody);
        }

        //De-serialize the response
        QnAMakerResult response;
        try
        {
            response = JsonConvert.DeserializeObject<QnAMakerResult>(responseString);
        }
        catch
        {
            throw new Exception("Unable to deserialize QnA Maker response string.");
        }

        await context.PostAsync($"{response.Answer}");
        context.Wait(MessageReceived);
    }

    // Go to https://luis.ai and create a new intent, then train/publish your luis app.
    // Finally replace "MyIntent" with the name of your newly created intent in the following handler
    [LuisIntent("MyIntent")]
    public async Task MyIntent(IDialogContext context, LuisResult result)
    {
        await context.PostAsync($"You have reached the MyIntent intent. You said: {result.Query}"); //
        context.Wait(MessageReceived);
    }
}