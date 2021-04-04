// OpenReplAI, if you always wanted that someone smart will respond to your         //
// emails so you can focus on your real work instead and be even more productive.   // 
// Built by Marius Dima using Azure Cognitive Services & OpenAI                     //
// March 2021                                                                       //
// https://github.com/dasdata/openreplai                                            //

using System;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Azure;
using Azure.AI.TextAnalytics;
using OpenAI_API;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ConsoleApp1
{
    class Program
    { 
        // Azure Speech 2 Text 
        static string _stKey = "xxxxxxxxxxxxxxxxxxxxx";  //https://azure.microsoft.com/en-us/services/cognitive-services/speech-to-text/
        static string _stRegion = "eastus";
        // Azure Text Analytics
        private static readonly AzureKeyCredential credentials = new AzureKeyCredential("xxxxxxxxxxxxxxxxxxxxxxx");
        private static readonly Uri endpoint = new Uri("https://...");
        // Default region 
        static string _cui = "en-US";  // https://docs.microsoft.com/en-us/dotnet/api/system.globalization.cultureinfo?view=net-5.0#CultureNames 
        // Open AI 
        static string _oiKey = "xxxxxxxxxxxxxxxxxxxxx"; // https://beta.openai.com/examples
        static string _oiPrompt = "";
       
        static string _userMails = "";
        static string strKeyPhrases = "";
        static string _overallSentiment = "";
        static async Task Main()
        {
            // start here
            await getEmailFilesAsync();  
            //  await ConversationStart(); 
            Console.ReadLine();
        }

        // takes txt emails found in the email folder
        static async Task getEmailFilesAsync() {
            var txtFiles = Directory.EnumerateFiles("emails", "*.txt");
            foreach (string currentFile in txtFiles)
            { 
                _userMails = File.ReadAllText(currentFile);
                Console.WriteLine("\t" + _userMails); 
                var client = new TextAnalyticsClient(endpoint, credentials); 
               // LanguageDetection(client, _userMails); // detect language !todo implement translation
                SentimentAnalysis(client, _userMails);  // extract mood from your email 
                KeyPhraseExtraction(client, _userMails); // extract key phrases 
                await cmdGetSummaryAsync(_userMails); // build a summary 
            }           
        }

        // build a overall email sentiment, some scoring could be done also 
        static void SentimentAnalysis(TextAnalyticsClient client,  string textSource)
        { 
            DocumentSentiment documentSentiment = client.AnalyzeSentiment(textSource);
            Console.WriteLine($"\n\tDocument sentiment: {documentSentiment.Sentiment}\n");
            _overallSentiment = documentSentiment.Sentiment.ToString();

            foreach (var sentence in documentSentiment.Sentences)
            {
                double _positive = sentence.ConfidenceScores.Positive;
                double _negative = sentence.ConfidenceScores.Negative;
                double _neutral = sentence.ConfidenceScores.Neutral;
                double[] sentiments = new double[] { _positive, _negative, _neutral };
                double maxValue = sentiments.Max();
                double maxIndex = sentiments.ToList().IndexOf(maxValue); 
                Console.Write($"\tResult: \"{maxIndex + " "+ maxValue}\" ");
            }
            Console.WriteLine();
        }

        // language detection
        static void LanguageDetection(TextAnalyticsClient client, string textSource)
        {
            DetectedLanguage detectedLanguage = client.DetectLanguage(textSource);
            // Console.WriteLine("Language:");
              // detectedLanguage.Name + " " + detectedLanguage.Iso6391Name;
            Console.WriteLine($"\t{detectedLanguage.Name},\tISO-6391: {detectedLanguage.Iso6391Name}\n");
            CultureInfo myCIintl = new CultureInfo(detectedLanguage.Iso6391Name, false);
        }


        //  keyPhraseExtraction  
        static void KeyPhraseExtraction(TextAnalyticsClient client, string textSource)
        { 
            var response = client.ExtractKeyPhrases(textSource);

            foreach (string keyphrase in response.Value)
            {
                Console.Write($"\t{keyphrase}"); 
                strKeyPhrases += keyphrase + ","; //string.Join(",", keyphrase); 
                strKeyPhrases.TrimEnd(',');
            }
            Console.WriteLine();
        }

        // get summary and prepare generated reply to sender with confirmation 
        static async Task cmdGetSummaryAsync(string textSource) {
            //  tl; dr:
            // initiate openai 
            OpenAIAPI api = new OpenAIAPI(new APIAuthentication(_oiKey));
            var result = await api.Completions.CreateCompletionAsync(prompt: textSource + " tl;dr: ", temperature: 0.3, top_p: 1, frequencyPenalty: 0, presencePenalty: 0) ;
            var resultString = Regex.Replace(result.ToString(), @"^\s+$[\r\n]*", string.Empty, RegexOptions.Multiline);
            // openai response
          //  Console.WriteLine($"\t{resultString}");
            string prepReplyPrompt = "You got a " + _overallSentiment + " mail: " + resultString + ". Preparing response..";

            await SynthesisToSpeakerAsync(prepReplyPrompt);

            string txtContext = "This is a human friendly email generator responding to "+ _overallSentiment + " topics.";
            txtContext += "Sender email: Do you know how to solve the project issue? I am in rush and have no ideea how to solve it. Please help me.  ";
            txtContext += "Seed words: solve , project issue, rush, no ideea, solve it, help";
            txtContext += "Reply Email: Ok, I'll look into it as soon as possible. No stress is necessary, we got this under control. ";
            txtContext += "Sender email:" + _userMails;
            txtContext += "Seed words:" + strKeyPhrases;
            txtContext += "Reply Email:";
            var email_result = await api.Completions.CreateCompletionAsync(txtContext, temperature: 0.8, max_tokens: 40, top_p: 1, frequencyPenalty: 0, presencePenalty: 0, stopSequences: "Human, AI");
            string prepGeneratedEmail = "Here is my reply: " + email_result + ". Shall we send it?";

            Console.WriteLine($"\n\t{prepGeneratedEmail}");
            await SynthesisToSpeakerAsync(prepGeneratedEmail);

            var speechConfig = SpeechConfig.FromSubscription(_stKey, _stRegion);
            string myRequest = await FromMic(speechConfig);

            var client = new TextAnalyticsClient(endpoint, credentials);
            // LanguageDetection(client, _userMails);
            DocumentSentiment documentSentiment = client.AnalyzeSentiment(myRequest);
           // Console.WriteLine($"\tConfirmed sentiment: {documentSentiment.Sentiment}\n");

            string replySentiment = documentSentiment.Sentiment.ToString(); 
             if (documentSentiment.Sentiment != TextSentiment.Positive)
               {
                 await SynthesisToSpeakerAsync("Ok. Done, it's sent!");
               }

               else {
                await SynthesisToSpeakerAsync(" Ok fine... ");
                } 
            }
         
        // you can also have conversational ai with the model 
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

        // creates a speech synthesizer using the default speaker as audio output.
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
            Console.WriteLine("\n\tSpeak into your microphone.");
            var result = await recognizer.RecognizeOnceAsync();
            string strResult = result.Text;
            Console.WriteLine($"\n\tHUMAN: {strResult}");
            return strResult; 
        }
    }
}
