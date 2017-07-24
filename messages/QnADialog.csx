using System;
using System.Threading.Tasks;
using System.Net;
using Microsoft.Bot.Builder.Azure;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using System.Text;

[Serializable]
public class QnADialog : IDialog<string>
{
    public async Task StartAsync(IDialogContext context, LuisResult result)
    {
        context.Wait(this.MessageReceivedAsync(result));
    }

    private async Task CheckTrainingRequired(IDialogContext context, IAwaitable<object> result)
    {
        var activity = await result as Activity;
        if (activity.Text.Equals("Yes"))
        {
            var reply = activity.CreateReply("Your next message will be posted as answer to the question. Go ahead.");
            await client.Conversations.SendToConversationAsync(reply);
            context.Wait(RetrainQnAModelAsync);
        }
        await context.Done(userResponse);
    }
    private async Task RetrainQnAModelAsync(IDialogContext context, IAwaitable<object> result)
    {
        var activity = await result as Activity;
        var knowledgebaseId = Utils.GetAppSetting("QnAKnowledgeBaseId");
        var qnamakerSubscriptionKey = Utils.GetAppSetting("QnASubscriptionKey");

        //Build the URI for updating knowledge base
        var qnamakerUriBase = new Uri("https://westus.api.cognitive.microsoft.com/qnamaker/v2.0");
        var builder = new UriBuilder($"{qnamakerUriBase}/knowledgebases/{knowledgebaseId}");

        //Add question and answer as part of the body
        var postBody = "{{\"add\":{\"qnaPairs\":[{\"answer\": \"" + reply.Value + "\",\"question\": \"" + result.Query + "\"}],\"urls\":[]}}}";
        var byteData = Encoding.UTF8.GetBytes(postBody);
        var content = new ByteArrayContent(byteData);

        //Send API request to QnAMaker API
        using (HttpClient httpClient = new HttpClient())
        {
            //Add request headers
            httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", qnamakerSubscriptionKey);
            httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");

            //Update Knowledgebase
            HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("PATCH"), builder.Uri)
            {
                Content = content
            };
            await httpClient.SendAsync(request);

            //Train the Model
            builder = new UriBuilder($"{qnamakerUriBase}/knowledgebases/{knowledgebaseId}/train");
            postBody = "{}";
            byteData = Encoding.UTF8.GetBytes(postBody);
            content = new ByteArrayContent(byteData);

            request = new HttpRequestMessage(new HttpMethod("PATCH"), builder.Uri)
            {
                Content = content
            };
            await httpClient.SendAsync(request);

            //Publish trained model
            request = new HttpRequestMessage(new HttpMethod("PUT"), builder.Uri)
            {
                Content = content
            };
            await httpClient.SendAsync(request);

            Activity newMessage = activity.CreateReply($"You contributed an answer. Thanks!");
            await activity.CreateReply(newMessage);
            context.Done(newMessage);
        }
    }
    private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result, LuisResult lResult)
    {
        var activity = await result as Activity;

        HttpResponseMessage response;
        var query = lResult.Query; //User Query
        var knowledgebaseId = Utils.GetAppSetting("QnAKnowledgeBaseId");
        var qnamakerSubscriptionKey = Utils.GetAppSetting("QnASubscriptionKey");
        var client = new ConnectorClient(new Uri(activity.ServiceUrl));

        //Build the URI
        Uri qnamakerUriBase = new Uri("https://westus.api.cognitive.microsoft.com/qnamaker/v2.0");
        var builder = new UriBuilder($"{qnamakerUriBase}/knowledgebases/{knowledgebaseId}/generateAnswer");

        //Add question as part of the body
        var postBody = $"{{\"question\": \"{query}\"}}";
        byte[] byteData = Encoding.UTF8.GetBytes(postBody);

        //Send the POST request to QnAMaker API
        using (HttpClient httpClient = new HttpClient())
        {
            var content = new ByteArrayContent(byteData);
            //Add the subscription key header
            httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", qnamakerSubscriptionKey);
            httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");
            HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("POST"), builder.Uri)
            {
                Content = content
            };
            response = await httpClient.SendAsync(request);
        }

        //De-serialize the response
        QnAMakerResult QnAResponse;
        try
        {
            QnAResponse = JsonConvert.DeserializeObject<QnAMakerResult>(response.Content.ToString());
        }
        catch
        {
            throw new Exception("Unable to deserialize QnA Maker response string.");
        }
        Activity newMessage = activity.CreateReply($"{QnAResponse.Answer}");
        await connector.Conversations.SendToConversationAsync(newMessage);

        //Retraining QnA Model
        newMessage = activity.CreateReply("If this did not help you, you can train me by providing the right answer. Would you like to?");
        newMessage.Type = ActivityTypes.Message;
        newMessage.TextFormat = TextFormatTypes.Plain;

        newMessage.SuggestedActions = new SuggestedActions()
        {
            Actions = new List<CardAction>()
                {
                    new CardAction(){ Title = "Yes", Type=ActionTypes.ImBack, Value="Yes" },
                    new CardAction(){ Title = "No", Type=ActionTypes.ImBack, Value="No" }
                }
        };
        await client.Conversations.SendToConversationAsync(newMessage);
        context.Wait(CheckTrainingRequired);

    }
}