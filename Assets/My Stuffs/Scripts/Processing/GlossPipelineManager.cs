using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;

public class GlossPipelineManager : MonoBehaviour
{
    public enum Language { English, Yoruba }

    [SerializeField] private Language selectedLanguage;
    
    private LanguageService languageService;
    
    public TextMeshProUGUI resultTranscription; 

    void Awake()
    {
        switch (selectedLanguage)
        {
            case Language.English:
                languageService = LanguageServiceFactory.CreateEnglishService();
                break;
            case Language.Yoruba:
                languageService = LanguageServiceFactory.CreateYorubaService();
                break;
            default:
                Debug.LogError("Unsupported language selected.");
                break;
        }
    }

    public async void Audio(string audioPath)
    {
        print("Audio to Gloss");

        var (nativeText, gloss) = await languageService.AudioToGlossWithTranscript(audioPath);

        if (resultTranscription != null)
            resultTranscription.text = nativeText;
    }

    public async void Text(string inputText)
    {
        JObject gloss = await languageService.TextToGloss(inputText);
    }
}