using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using codegen2021.Shared;
using Microsoft.CognitiveServices.Speech;
using Microsoft.AspNetCore.Http;
using Microsoft.CognitiveServices.Speech.Audio;
using System.IO;
using Azure.AI.TextAnalytics;
using Azure;
using Microsoft.Azure.CognitiveServices.Language.LUIS.Runtime;
using Microsoft.Azure.CognitiveServices.Language.LUIS.Runtime.Models;
using Newtonsoft.Json.Linq;

namespace codegen2021.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SpeechController : ControllerBase
    {
        private readonly SpeechConfig speechConfig;
        private readonly string subscriptionKey = "<SUBSCRIPTION-KEY>";
        private string cognitiveEndpoint = "<COGNITIVE-ENDPOINT>";
        private TextAnalyticsClient analyticsClient;
        private Guid appId = new Guid("<LUIS-APP-ID>");
        private string predictionEndpoint = $"<PREDICTION-ENDPOINT>";
        private LUISRuntimeClient LUISClient;
        
        public SpeechController()
        {
            this.speechConfig = SpeechConfig.FromSubscription(
                subscriptionKey,
                "westeurope");
            speechConfig.SpeechRecognitionLanguage = "it-it";
            speechConfig.SpeechSynthesisLanguage = "it-it";
            speechConfig.SpeechSynthesisVoiceName = "it-IT-IsabellaNeural";

            analyticsClient = new TextAnalyticsClient(
                new Uri(cognitiveEndpoint), 
                new AzureKeyCredential(subscriptionKey));

            var credentials = new ApiKeyServiceClientCredentials(subscriptionKey);
            LUISClient = new LUISRuntimeClient(credentials) { Endpoint = predictionEndpoint };
        }
        
        [HttpPost()]
        [Route("{action}")]
        public async Task<IActionResult> RecognizeAudio(IFormFile file)
        {
            var audioStreamFormat = AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1);
            using var audioConfig = AudioConfig.FromStreamInput(new GenericInputAudioStream(file), audioStreamFormat);
            using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);
            var result = await recognizer.RecognizeOnceAsync();
            
            var speechResult = new SpeechResult { Text = result.Text };
            
            var entities = await analyticsClient.RecognizeEntitiesAsync(result.Text);
            
            foreach (var entity in entities.Value)
            {
                speechResult.Entities.Add(new SpeechResultEntity()
                {
                    Text = entity.Text,
                    Category = entity.Category.ToString(),
                    ConfidenceScore = entity.ConfidenceScore
                });
            }
            
            var keyphrases = await analyticsClient.ExtractKeyPhrasesAsync(result.Text);
            foreach (var keyphrase in keyphrases.Value)
            {
                speechResult.KeyPhrases.Add(keyphrase);
            }


            var request = new PredictionRequest
            {
                Query = result.Text
            };
            
            var prediction = await LUISClient.Prediction.GetSlotPredictionAsync(appId,
                "Staging",
                request,
                showAllIntents: true,
                log: true);
            
            speechResult.TopIntent = prediction.Prediction.TopIntent;
            foreach (var intent in prediction.Prediction.Intents)
            {
                speechResult.Intents.Add(new SpeechResultIntent()
                {
                    Intent = intent.Key,
                    Score = intent.Value.Score
                });
            }
            
            speechResult.PredictionEntities = ExtractProperties(prediction);

            return Ok(speechResult);
        }

        private List<KeyValuePair<string, string>> ExtractProperties(PredictionResponse prediction)
        {
            var properties = new List<KeyValuePair<string, string>>();
            foreach (var e in prediction.Prediction.Entities)
            {
                foreach (var token in e.Value as JArray)
                {
                    if (token.Type == JTokenType.Array)
                    {
                        foreach (var value in token.Values<string>())
                        {
                            properties.Add(new(e.Key, value));
                        }
                    }
                    else
                    {
                        properties.Add(new(e.Key, token.ToString()));
                    }
                }
            }
        
            return properties;
        }

        [HttpPost()]
        [Route("{action}")]
        public async Task<IActionResult> SynthetizeText(SynthesisRequest request)
        {
            var stream = new MemoryStream();
            speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Audio24Khz160KBitRateMonoMp3);
            using var audioConfig = AudioConfig.FromStreamOutput(new GenericOutputAudioStream(stream));
            using var synthesizer = new SpeechSynthesizer(speechConfig, audioConfig);
            var result = await synthesizer.SpeakTextAsync(request.Text);
            stream.Position = 0;
            return File(stream, "audio/mpeg");
        }
    }

    public class GenericOutputAudioStream : PushAudioOutputStreamCallback
    {
        private readonly Stream stream;
    
        public GenericOutputAudioStream(Stream stream)
        {
            this.stream = stream;
        }
    
        public override uint Write(byte[] dataBuffer)
        {
            stream.Write(dataBuffer);
            return (uint)dataBuffer.Length;
        }
    }

    public class GenericInputAudioStream : PullAudioInputStreamCallback
    {
        readonly Stream stream;
    
        public GenericInputAudioStream(IFormFile file)
        {
            stream = file.OpenReadStream();
        }
    
        override public int Read(byte[] buffer, uint size)
        {
            return stream.Read(buffer, 0, (int)size);
        }
    
        override public void Close()
        {
            stream.Close();
        }
    }
}
