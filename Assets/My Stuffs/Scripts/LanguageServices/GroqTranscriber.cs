using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

public class GroqTranscriber : ITranscriber
{
    public Task<string> Transcribe(string audioPath)
        => GroqModel.Instance.TranscribeAudio(audioPath);
}

public class YorubaTranscriber : ITranscriber
{
    public Task<string> Transcribe(string audioPath)
        => YorubaModel.Instance.TranscribeAudio(audioPath);
}

public class YorubaToEnglishTranslator : ITranslator
{
    public Task<string> Translate(string text)
        => ChatgptModel.Instance.TranslateYorubaToEnglish(text);
}

public class EnglishGlossifier : IGlossifier
{
    public Task<JObject> TextToGloss(string text)
        => ChatgptModel.Instance.ConvertTextToGloss(text);
}