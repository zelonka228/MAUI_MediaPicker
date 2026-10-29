using System.ClientModel;
using OpenAI;
using OpenAI.Audio;

namespace MAUI_MediaPicker.Services;

public class SpeechToTextService
{
    private readonly AudioClient _audio = new OpenAIClient(
        new ApiKeyCredential(string.IsNullOrEmpty(ApiKeys.SttKey) ? "not-set" : ApiKeys.SttKey),
        new OpenAIClientOptions { Endpoint = new Uri(ApiKeys.SttBaseUrl) })
        .GetAudioClient(ApiKeys.SttModel);

    public async Task<string> TranscribeAsync(string filePath, string language)
    {
        if (string.IsNullOrEmpty(ApiKeys.SttKey))
            throw new InvalidOperationException("Не вказано ключ розпізнавання мови (SttKey у Services/ApiKeys.cs).");

        await using var audioStream = File.OpenRead(filePath);

        AudioTranscription transcription = await _audio.TranscribeAudioAsync(
            audioStream,
            Path.GetFileName(filePath),
            new AudioTranscriptionOptions { Language = language });

        return transcription.Text;
    }
}
