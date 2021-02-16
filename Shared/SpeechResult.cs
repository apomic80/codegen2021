using System.Collections.Generic;

namespace codegen2021.Shared
{
    public class SpeechResult
    {
        public string Text { get; set; }

        public List<SpeechResultEntity> Entities { get; set; } = new List<SpeechResultEntity>();
        public List<string> KeyPhrases { get; set; } = new List<string>();

        public string TopIntent { get; set; }
        public List<SpeechResultIntent> Intents { get; set; } = new List<SpeechResultIntent>();
        public List<KeyValuePair<string, string>> PredictionEntities { get; set; }
    }

    public class SpeechResultIntent
    {
        public string Intent { get; set; }
        public double? Score { get; set; }
    }

    public class SpeechResultEntity
    {
        public string Text { get; set; }
        public string Category { get; set; }
        public double ConfidenceScore { get; set; }
    }
}