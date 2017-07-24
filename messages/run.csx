#r "Newtonsoft.Json"
#load "BasicLuisDialog.csx"

using System;
using System.Net;
using System.Threading;
using Newtonsoft.Json;
using Microsoft.Bot.Builder.Azure;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;

public async Task QueryQnA(Activity activity)
{
    string responseString = string.Empty;
    var query = result.Query; //User Query
    var knowledgebaseId = Utils.GetAppSetting("QnAKnowledgeBaseId");
    var qnamakerSubscriptionKey = Utils.GetAppSetting("QnASubscriptionKey");
    var client = new ConnectorClient(new Uri(activity.ServiceUrl));

    //Build the URI
    Uri qnamakerUriBase = new Uri("https://westus.api.cognitive.microsoft.com/qnamaker/v1.0");
    var builder = new UriBuilder($"{qnamakerUriBase}/knowledgebases/{knowledgebaseId}/generateAnswer");

    //Add question as part of the body
    var postBody = $"{{\"question\": \"{query}\"}}";

    //Send the POST request to QnAMaker API
    using (WebClient webClient = new WebClient())
    {
        //Set the encoding to UTF8
        webClient.Encoding = System.Text.Encoding.UTF8;

        //Add the subscription key header
        webClient.Headers.Add("Ocp-Apim-Subscription-Key", qnamakerSubscriptionKey);
        webClient.Headers.Add("Content-Type", "application/json");
        responseString = webClient.UploadString(builder.Uri, postBody);
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

    //Retraining QnA Model
    var reply = activity.CreateReply("If this did not help you, you can train me by providing the right answer. Would you like to?");
    reply.Type = ActivityTypes.Message;
    reply.TextFormat = TextFormatTypes.Plain;

    reply.SuggestedActions = new SuggestedActions()
    {
        Actions = new List<CardAction>()
            {
                new CardAction(){ Title = "Yes", Type=ActionTypes.ImBack, Value="Yes" },
                new CardAction(){ Title = "No", Type=ActionTypes.ImBack, Value="No" }
            }
    };
    await client.Conversations.ReplyToActivityAsync(reply);
    context.Wait(MessageReceived);
    if (reply.Value.Equals("Yes"))
    {
        reply = activity.CreateReply("Your next message will be posted as answer to the question. Go ahead.");
        await client.Conversations.ReplyToActivityAsync(reply);
        context.Wait(MessageReceived);
        await context.PostAsync($"You provided an answer. You said: {reply.Value}");
    }
}
public static async Task<object> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info($"Webhook was triggered!");

    // Initialize the azure bot
    using (BotService.Initialize())
    {
        // Deserialize the incoming activity
        string jsonContent = await req.Content.ReadAsStringAsync();
        var activity = JsonConvert.DeserializeObject<Activity>(jsonContent);
        
        // authenticate incoming request and add activity.ServiceUrl to MicrosoftAppCredentials.TrustedHostNames
        // if request is authenticated
        if (!await BotService.Authenticator.TryAuthenticateAsync(req, new [] {activity}, CancellationToken.None))
        {
            return BotAuthenticator.GenerateUnauthorizedResponse(req);
        }
    
        if (activity != null)
        {
            // one of these will have an interface and process it
            switch (activity.GetActivityType())
            {
                case ActivityTypes.Message:
                    string bTask = await Conversation.SendAsync(activity, () => new BasicLuisDialog());
                    switch(bTask)
                    {
                        case "QnAQuery":
                            await QnAQueryAsync(activity);
                    }
                    break;
                case ActivityTypes.ConversationUpdate:
                    var client = new ConnectorClient(new Uri(activity.ServiceUrl));
                    IConversationUpdateActivity update = activity;
                    if (update.MembersAdded.Any())
                    {
                        var reply = activity.CreateReply();
                        var newMembers = update.MembersAdded?.Where(t => t.Id != activity.Recipient.Id);
                        foreach (var newMember in newMembers)
                        {
                            reply.Text = "Welcome";
                            if (!string.IsNullOrEmpty(newMember.Name))
                            {
                                reply.Text += $" {newMember.Name}";
                            }
                            reply.Text += "!";
                            await client.Conversations.ReplyToActivityAsync(reply);
                        }
                    }
                    break;
                case ActivityTypes.ContactRelationUpdate:
                case ActivityTypes.Typing:
                case ActivityTypes.DeleteUserData:
                case ActivityTypes.Ping:
                default:
                    log.Error($"Unknown activity type ignored: {activity.GetActivityType()}");
                    break;
            }
        }
        return req.CreateResponse(HttpStatusCode.Accepted);
    }    
}