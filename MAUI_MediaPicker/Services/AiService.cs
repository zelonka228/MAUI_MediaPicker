using System.ClientModel;
using OpenAI;
using OpenAI.Chat;

namespace MAUI_MediaPicker.Services;

public class AiService
{
    private readonly ChatClient _chat = new OpenAIClient(
        new ApiKeyCredential(ApiKeys.AiKey),
        new OpenAIClientOptions { Endpoint = new Uri(ApiKeys.AiBaseUrl) })
        .GetChatClient(ApiKeys.AiModel);

    public async Task<string> SummarizeAsync(string transcript, string languageName)
    {
        ChatCompletion completion = await _chat.CompleteChatAsync(
            new SystemChatMessage(
                $"Випиши головні тези з розповіді користувача. Мова відповіді: {languageName}. " +
                "Кожна теза з нового рядка, починається з «• »."),
            new UserChatMessage(transcript));

        return completion.Content.Count > 0 ? completion.Content[0].Text : "";
    }
}
