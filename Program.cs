
// wait for user input then send to openai and get voice back to user. 
// loop to the begining for more questions

using System;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using OpenAI_API;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Globalization;

namespace ConsoleApp1
{
    class Program
    {
        static string _cui = "en-US";  // https://docs.microsoft.com/en-us/dotnet/api/system.globalization.cultureinfo?view=net-5.0#CultureNames
        static string _stKey = "xxxxxxxxxxxxxxxxxxxxxxxxxx";  //https://azure.microsoft.com/en-us/services/cognitive-services/speech-to-text/
        static string _stRegion = "eastus";
        static string _oiKey = "xxxxxxxxxxxxxxxxxxxxxxxxxx"; // https://beta.openai.com/examples
        static string _oiPrompt = "HUMAN: What have you been up to?\nCOMPUTER: Watching old movies.\nHUMAN:";
        static async Task Main()
        {
            // start here
            CultureInfo myCIintl = new CultureInfo(_cui, false);
            await ConversationStart();
        }
         
        async static Task ConversationStart()
        {
           Console.Clear(); 
           var speechConfig = SpeechConfig.FromSubscription(_stKey, _stRegion); 
           string myRequest = await FromMic(speechConfig); 
            if (myRequest!="")
            { 
                 // initiate openai 
                 OpenAIAPI api = new OpenAIAPI(new APIAuthentication(_oiKey)); 
                 var result = await api.Completions.CreateCompletionAsync(prompt: _oiPrompt +" "+ myRequest,  temperature:0.9, top_p:1, frequencyPenalty:0, presencePenalty:0.6);
                 var resultString = Regex.Replace(result.ToString(), @"^\s+$[\r\n]*", string.Empty, RegexOptions.Multiline); 
                  // openai response
                 Console.WriteLine($"{resultString}" ); 
                 await SynthesisToSpeakerAsync(resultString); 
            }
            // loop back 
            await ConversationStart();  
        }

        // Creates a speech synthesizer using the default speaker as audio output.
        public static async Task SynthesisToSpeakerAsync(string text)
        { 
            var config = SpeechConfig.FromSubscription(_stKey, _stRegion); 
            using (var synthesizer = new SpeechSynthesizer(config))
            { 
                using (var result = await synthesizer.SpeakTextAsync(text))
                 {
                    if (result.Reason == ResultReason.SynthesizingAudioCompleted)
                    {
                      //  Console.WriteLine($"Speech synthesized to speaker for text [{text}]");
                    } 
                 } 
            }
        }

        // Speech 2 text using azure
        async static Task<string> FromMic(SpeechConfig speechConfig)  
        {
           var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
           var recognizer = new SpeechRecognizer(speechConfig, audioConfig);
            Console.WriteLine("Speak into your microphone.");
            var result = await recognizer.RecognizeOnceAsync();
            string strResult = result.Text;
            Console.WriteLine($"HUMAN: {strResult}");
            return strResult; 
        }
    }
}
