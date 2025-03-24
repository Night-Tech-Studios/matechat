using Il2CppSteamworks;
using MelonLoader;
using System.Collections;
using UnityEngine;

namespace matechat
{
    public static class Config
    {
        public static MelonPreferences_Category category;

        public static MelonPreferences_Entry<KeyCode> CHAT_KEYBIND;
        public static MelonPreferences_Entry<string> ENGINE_TYPE; // Engine selection
        public static MelonPreferences_Entry<string> ACCOUNT_ID;  // Cloudflare-specific
        public static MelonPreferences_Entry<string> API_KEY;
        public static MelonPreferences_Entry<string> MODEL_NAME;
        public static MelonPreferences_Entry<string> SYSTEM_PROMPT;
        public static MelonPreferences_Entry<string> AI_NAME;
        public static MelonPreferences_Entry<string> NAME; // The Name that the AI Will know you by 

        // new feature for openai compatible 
        public static MelonPreferences_Entry<string> BASE_URL;

        // Audio Features
        public static MelonPreferences_Entry<bool> ENABLE_TTS;
        public static MelonPreferences_Entry<string> TTS_ENGINE;
        public static MelonPreferences_Entry<string> TTS_API_URL;

        public static MelonPreferences_Entry<string> TTS_TEXT_LANG;
        public static MelonPreferences_Entry<string> TTS_REF_AUDIO_PATH;
        public static MelonPreferences_Entry<string> TTS_PROMPT_TEXT;
        public static MelonPreferences_Entry<string> TTS_PROMPT_LANG;
        public static MelonPreferences_Entry<string> TTS_TEXT_SPLIT_METHOD;
        public static MelonPreferences_Entry<int> TTS_BATCH_SIZE;
        public static MelonPreferences_Entry<string> TTS_MEDIA_TYPE;
        public static MelonPreferences_Entry<bool> TTS_STREAMING_MODE;

        public static MelonPreferences_Entry<bool> ENABLE_AUDIO_MODEL;
        public static MelonPreferences_Entry<string> AUDIO_MODEL_API_URL;
        public static MelonPreferences_Entry<string> AUDIO_MODEL_ENGINE;


        public static MelonPreferences_Entry<int> CHAT_WINDOW_WIDTH;
        public static MelonPreferences_Entry<int> CHAT_WINDOW_HEIGHT;
        public static MelonPreferences_Entry<int> CHAT_WINDOW_X;
        public static MelonPreferences_Entry<int> CHAT_WINDOW_Y;
        public static MelonPreferences_Entry<int> CHAT_WINDOW_FONT_SIZE;
        

        private static string lastUsedEngineType;
        private static string lastUsedSystemPrompt;

        public static void Initialize()
        {
            category = MelonPreferences.CreateCategory("MateChat");

            CHAT_KEYBIND = category.CreateEntry("CHAT_KEYBIND", KeyCode.F8);
            ENGINE_TYPE = category.CreateEntry("ENGINE_TYPE", "Cloudflare"); // Default to Cloudflare
            ACCOUNT_ID = category.CreateEntry("ACCOUNT_ID", "");  // Optional for OpenRouter/OpenAI
            API_KEY = category.CreateEntry("API_KEY", "");        // Mandatory for all engines
            MODEL_NAME = category.CreateEntry("MODEL_NAME", "llama-3.1-8b-instruct"); // Default Cloudflare model
            SYSTEM_PROMPT = category.CreateEntry(
                "SYSTEM_PROMPT",
                "You are a cheerful digital companion inspired by Hatsune Miku! Keep responses brief and energetic. Use musical notes (♪), kaomoji (◕‿◕), and cute text markers (✧) naturally. Express yourself in a sweet, J-pop idol style while being helpful and concise. Add '~' to soften statements occasionally. End responses with a musical note or kaomoji when fitting. Keep answers short and direct, but always maintain a cute and supportive tone!"
            );
            AI_NAME = category.CreateEntry("AI_NAME", "Desktop Mate");
            NAME = category.CreateEntry("NAME", "USER");

            BASE_URL = category.CreateEntry("BASE_URL", "");

            ENABLE_TTS = category.CreateEntry("ENABLE_TTS", false);
            TTS_ENGINE = category.CreateEntry("TTS_ENGINE", "GPT-SoVITS");
            TTS_API_URL = category.CreateEntry("TTS_API_URL", "http://localhost:9880");

            TTS_TEXT_LANG = category.CreateEntry("TTS_TEXT_LANG", "ja");
            TTS_REF_AUDIO_PATH = category.CreateEntry("TTS_REF_AUDIO_PATH", "reference.wav");
            TTS_PROMPT_TEXT = category.CreateEntry("TTS_PROMPT_TEXT", "1杯の珈琲と、 至福の時をアナタに…。");
            TTS_PROMPT_LANG = category.CreateEntry("TTS_PROMPT_LANG", "ja");
            TTS_TEXT_SPLIT_METHOD = category.CreateEntry("TTS_TEXT_SPLIT_METHOD", "cut5");
            TTS_BATCH_SIZE = category.CreateEntry("TTS_BATCH_SIZE", 1);
            TTS_MEDIA_TYPE = category.CreateEntry("TTS_MEDIA_TYPE", "wav");
            TTS_STREAMING_MODE = category.CreateEntry("TTS_STREAMING_MODE", false);

            // for 'omni' model, such as gpt-4o, gemini
            ENABLE_AUDIO_MODEL = category.CreateEntry("ENABLE_AUDIO_MODEL", false);
            //AUDIO_MODEL_API_URL = category.CreateEntry("AUDIO", "http://localhost:8000/v1/audio-model");

            CHAT_WINDOW_WIDTH = category.CreateEntry("CHAT_WINDOW_WIDTH", 400);
            CHAT_WINDOW_HEIGHT = category.CreateEntry("CHAT_WINDOW_HEIGHT", 500);
            CHAT_WINDOW_X = category.CreateEntry("CHAT_WINDOW_X", 20);
            CHAT_WINDOW_Y = category.CreateEntry("CHAT_WINDOW_Y", 20);
            CHAT_WINDOW_FONT_SIZE = category.CreateEntry("CHAT_WINDOW_FONT_SIZE", 16);

            category.SetFilePath("UserData/MateChat.cfg");
            category.SaveToFile();

            lastUsedEngineType = ENGINE_TYPE.Value;
            lastUsedSystemPrompt = SYSTEM_PROMPT.Value;
        }

        public static bool TestConfig()
        {
            bool isValid = true;

            void LogError(string message)
            {
                MelonLogger.Error(message);
                isValid = false;
            }

            // ENGINE_TYPE Validation
            if (string.IsNullOrEmpty(ENGINE_TYPE.Value) ||
                (ENGINE_TYPE.Value.ToLower() != "cloudflare" &&
                 ENGINE_TYPE.Value.ToLower() != "openrouter" &&
                 ENGINE_TYPE.Value.ToLower() != "openai" &&
                 ENGINE_TYPE.Value.ToLower() != "openaicompatible"))
            {
                LogError($"Invalid ENGINE_TYPE: {ENGINE_TYPE.Value}. Supported: Cloudflare, OpenRouter, OpenAI, OpenAICompatible.");
            }

            // API_KEY Validation
            if (string.IsNullOrEmpty(API_KEY.Value))
                LogError("API Key is not configured!");

            // ACCOUNT_ID Validation for Cloudflare
            if (ENGINE_TYPE.Value == "Cloudflare" && string.IsNullOrEmpty(ACCOUNT_ID.Value))
                LogError("ACCOUNT_ID is required for Cloudflare!");
            else if (ENGINE_TYPE.Value == "OpenAICompatible" && string.IsNullOrEmpty(BASE_URL.Value))
                LogError("BASE_URL is required for OpenAICompatible.");

            // MODEL_NAME Validation
            if (string.IsNullOrEmpty(MODEL_NAME.Value))
                LogError("MODEL_NAME is not configured!");

            // SYSTEM_PROMPT Validation
            if (string.IsNullOrEmpty(SYSTEM_PROMPT.Value))
                LogError("SYSTEM_PROMPT is empty!");
            else if (SYSTEM_PROMPT.Value.Length > 4096 && ENGINE_TYPE.Value.ToLower() != "openaicompatible")
                LogError("SYSTEM_PROMPT exceeds the maximum length of 4096 characters!");

            // AI_NAME Validation
            if (string.IsNullOrEmpty(AI_NAME.Value))
                LogError("AI_NAME is not configured!");
            else if (AI_NAME.Value.Length > 32)
                LogError("AI_NAME is too long (maximum 32 characters)!");


            if (TTS_ENGINE.Value.ToLower() == "gpt-sovits" && TTS_STREAMING_MODE.Value)
            {
                LogError("GPT-Sovits does not fully support streaming mode. Please set TTS_STREAMING_MODE false");
            }
            // Chat Window Configuration Validation
            ValidateRange(CHAT_WINDOW_WIDTH.Value, 200, Screen.width, "chat window width");
            ValidateRange(CHAT_WINDOW_HEIGHT.Value, 200, Screen.height, "chat window height");
            ValidateRange(CHAT_WINDOW_FONT_SIZE.Value, 8, 72, "font size");

            return isValid;

            void ValidateRange(int value, int min, int max, string name)
            {
                if (value < min || value > max)
                    LogError($"Invalid {name}: {value}. Should be between {min} and {max}!");
            }
        }

        public static string GetAPIUrl()
        {
            switch (ENGINE_TYPE.Value)
            {
                case "Cloudflare":
                    return $"https://api.cloudflare.com/client/v4/accounts/{ACCOUNT_ID.Value}/ai/run/@cf/meta/{MODEL_NAME.Value}";
                case "OpenRouter":
                    return "https://openrouter.ai/api/v1/chat/completions";  // Fixed
                                                                             // endpoint
                case "OpenAI":
                    return "https://api.openai.com/v1/chat/completions";  // Fixed
                                                                          // endpoint
                default:
                    throw new System.Exception(
                        $"Unsupported ENGINE_TYPE: {ENGINE_TYPE.Value}");
            }
        }

        public static void DisplayConfigurationInstructions()
        {
            switch (ENGINE_TYPE.Value)
            {
                case "Cloudflare":
                    MelonLogger.Msg(
                        "Cloudflare Configuration: Provide your ACCOUNT_ID and API_KEY. Select a model like llama-3.1-8b-instruct.");
                    break;
                case "OpenRouter":
                    MelonLogger.Msg(
                        "OpenRouter Configuration: Provide your API_KEY. No ACCOUNT_ID is required. Select models like gpt-3.5-turbo.");
                    break;
                case "OpenAI":
                    MelonLogger.Msg(
                        "OpenAI Configuration: Provide your API_KEY. No ACCOUNT_ID is required. Use models like gpt-3.5-turbo or gpt-4.");
                    break;
                default:
                    MelonLogger.Error($"Unsupported ENGINE_TYPE: {ENGINE_TYPE.Value}");
                    break;
            }
        }

        public static void ReloadConfig()
        {
            category.LoadFromFile();

            if (TestConfig())
            {
                if (ENABLE_TTS.Value == true)
                {
                    Core.GetAudioEngine();
                }

                if (lastUsedSystemPrompt != SYSTEM_PROMPT.Value || lastUsedEngineType != ENGINE_TYPE.Value)
                {
                    MelonLogger.Msg("Engine or system prompt changed. Reloading engine...");
                    lastUsedSystemPrompt = SYSTEM_PROMPT.Value;
                    lastUsedEngineType = ENGINE_TYPE.Value;

                    // Reload the AI Engine
                    Core.ReloadAIEngines();
                }

                Core.GetChatFeature()?.UpdateSettings();
                MelonLogger.Msg("Config reloaded successfully!");

                // Test the selected engine
                MelonCoroutines.Start(TestEngineCoroutine());
            }
            else
            {
                MelonLogger.Error("Config reload failed - please check your settings!");
            }
        }

        private static IEnumerator TestEngineCoroutine()
        {
            var task = Core.GetAIEngineManager().TestEngineAsync();

            // Wait until the task is completed
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsCompletedSuccessfully && task.Result)
            {
                MelonLogger.Msg("AI engine test successful!");
            }
            else
            {
                MelonLogger.Error("AI engine test failed!");
            }
        }
    }
}
