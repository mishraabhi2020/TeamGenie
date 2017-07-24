using System;
using System.Threading.Tasks;
using System.Net;
using Microsoft.Bot.Builder.Azure;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using Newtonsoft.Json;

[Serializable]
public class NameDialog : IDialog<string>
{
    public async Task StartAsync(IDialogContext context)
    {
        context.Wait(this.MessageReceivedAsync);
    }

    private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
    {
        var activity = await result as Activity;
        /*
        if ((message.Text != null) && (message.Text.Trim().Length > 0))
        {
            context.Done(message.Text);
        }
        else
        {
            --attempts;
            if (attempts > 0)
            {
                await context.PostAsync("I'm sorry, I don't understand your reply. What is your name (e.g. 'Bill', 'Melinda')?");

                context.Wait(this.MessageReceivedAsync);
            }
            else
            {
                context.Fail(new TooManyAttemptsException("Message was not a string or was an empty string."));
            }
        }*/
        var connector = new ConnectorClient(new Uri(activity.ServiceUrl));
        HttpResponseMessage response;
        var query = result.Query; //User Query
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

    }
}