using System.Collections;
using MelonLoader;
using UnityEngine;
using matechat.sdk.Feature;
using matechat.database;

namespace matechat.feature
{
    public class ChatFeature : Feature
    {
        private bool isWaitingForResponse;
        private string inputText = string.Empty;
        private string responseText = string.Empty;
        private bool isChatFocused;
        private Vector2 scrollPosition;
        private GUIStyle textStyle;
        private Rect windowRect;

        private bool isResizing = false;
        // Remove unused drag variables that conflict with GUI.DragWindow
        private Vector2 resizeStartPosition;
        private Rect originalRect;
        private const float ResizeHandleSize = 20f;
        private const float MinWindowWidth = 300f;
        private const float MinWindowHeight = 200f;

        private static readonly Color MikuTeal = new Color(0.07f, 0.82f, 0.82f, 0.95f);
        private static readonly Color DarkTeal = new Color(0.05f, 0.4f, 0.4f, 0.95f);
        private static readonly Color WindowBackground = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        private static readonly Color InputBackground = new Color(1, 1, 1, 0.15f);
        private static readonly Color ContentBackground = new Color(1, 1, 1, 0.1f);
        private const int TitleBarHeight = 30;
        private const int InputHeight = 25;
        private const int Padding = 10;
        private const int MaxChatHistory = 50;

        public ChatFeature() : base("Chat", Config.CHAT_KEYBIND.Value)
        {
            try
            {
                textStyle = new GUIStyle();
                if (GUI.skin != null && GUI.skin.label != null)
                {
                    textStyle.fontSize = GUI.skin.label.fontSize;
                    textStyle.font = GUI.skin.label.font;
                    textStyle.normal = GUI.skin.label.normal;
                }
                textStyle.normal.textColor = Color.white;
                textStyle.fontSize = Config.CHAT_WINDOW_FONT_SIZE.Value;
                textStyle.wordWrap = true;
                textStyle.richText = true;
                UpdateWindowRect();
                UpdateSettings();
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error initializing ChatFeature: {ex.Message}");
            }
        }

        public void UpdateWindowRect()
        {
            windowRect = new Rect(
                Config.CHAT_WINDOW_X.Value, Config.CHAT_WINDOW_Y.Value,
                Config.CHAT_WINDOW_WIDTH.Value, Config.CHAT_WINDOW_HEIGHT.Value);
        }

        public void DrawGUI()
        {
            try
            {
                if (!IsEnabled)
                    return;

                windowRect = GUI.Window(0, windowRect, (GUI.WindowFunction)DrawWindowContents, string.Empty);
                
                // Keep window within screen bounds
                windowRect.x = Mathf.Clamp(windowRect.x, 0, Screen.width - windowRect.width);
                windowRect.y = Mathf.Clamp(windowRect.y, 0, Screen.height - windowRect.height);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error in GUI update: {ex.Message}");
                IsEnabled = false;
            }
        }

        private void DrawWindowContents(int windowID)
        {
            Color originalBgColor = GUI.backgroundColor;

            // Main window background
            GUI.backgroundColor = WindowBackground;
            GUI.Box(new Rect(0, 0, windowRect.width, windowRect.height), string.Empty);

            // Title bar
            GUI.backgroundColor = MikuTeal;
            Rect titleBarRect = new Rect(0, 0, windowRect.width, TitleBarHeight);
            GUI.Box(titleBarRect, string.Empty);
            GUI.Label(new Rect(60, 5, windowRect.width - 120, 20), "✧ Mate Chat ♪ ✧");

            // Clear button
            Rect clearButtonRect = new Rect(windowRect.width - 55, 5, 50, 20);
            GUI.backgroundColor = clearButtonRect.Contains(Event.current.mousePosition) 
                ? new Color(DarkTeal.r * 1.2f, DarkTeal.g * 1.2f, DarkTeal.b * 1.2f, DarkTeal.a)
                : DarkTeal;
            if (GUI.Button(clearButtonRect, "Clear"))
            {
                ClearChat();
            }

            // Chat content area
            GUI.backgroundColor = ContentBackground;
            Rect contentRect = new Rect(
                Padding,
                TitleBarHeight + Padding,
                windowRect.width - (Padding * 2),
                windowRect.height - TitleBarHeight - InputHeight - (Padding * 3)
            );
            GUI.Box(contentRect, string.Empty);

            // Chat messages scroll view
            float contentHeight = CalculateContentHeight(responseText, contentRect.width - 10);
            Rect viewRect = new Rect(0, 0, contentRect.width - 10, Mathf.Max(contentHeight, contentRect.height));
            scrollPosition = GUI.BeginScrollView(contentRect, scrollPosition, viewRect);
            GUI.Label(new Rect(5, 0, viewRect.width - 5, contentHeight), responseText, textStyle);
            GUI.EndScrollView();

            // Input area
            GUI.backgroundColor = InputBackground;
            Rect inputRect = new Rect(
                Padding,
                windowRect.height - InputHeight - Padding,
                windowRect.width - 90,
                InputHeight
            );
            GUI.Box(inputRect, string.Empty);
            
            // Handle focus on input area click
            if (Event.current.type == EventType.MouseDown && inputRect.Contains(Event.current.mousePosition))
            {
                isChatFocused = true;
                Event.current.Use();
            }
            else if (Event.current.type == EventType.MouseDown && !inputRect.Contains(Event.current.mousePosition))
            {
                isChatFocused = false;
            }
            
            GUI.Label(inputRect, inputText, textStyle);
            HandleInputEvents();

            // Send button
            GUI.backgroundColor = MikuTeal;
            Rect sendButtonRect = new Rect(
                inputRect.x + inputRect.width + 10,
                inputRect.y,
                60,
                InputHeight
            );
            if (GUI.Button(sendButtonRect, "♪ Send") && !string.IsNullOrEmpty(inputText))
            {
                SendMessage();
            }

            // Resize handle - FIX: Use window-local coordinates
            Rect resizeHandle = new Rect(
                windowRect.width - ResizeHandleSize,
                windowRect.height - ResizeHandleSize,
                ResizeHandleSize,
                ResizeHandleSize
            );

            // Handle resize events (all using window-local coordinates)
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                if (resizeHandle.Contains(Event.current.mousePosition))
                {
                    isResizing = true;
                    resizeStartPosition = Event.current.mousePosition;
                    originalRect = new Rect(0, 0, windowRect.width, windowRect.height);
                    Event.current.Use();
                }
            }
            else if (Event.current.type == EventType.MouseUp)
            {
                isResizing = false;
            }

            // Handle resizing - fixed to use proper coordinates
            if (isResizing && Event.current.type == EventType.MouseDrag)
            {
                Vector2 diff = Event.current.mousePosition - resizeStartPosition;
                windowRect.width = Mathf.Max(originalRect.width + diff.x, MinWindowWidth);
                windowRect.height = Mathf.Max(originalRect.height + diff.y, MinWindowHeight);
                SaveWindowPosition();
                Event.current.Use();
            }

            // Draw resize handle
            GUI.backgroundColor = MikuTeal;
            GUI.Box(resizeHandle, "⟲");
            
            GUI.backgroundColor = originalBgColor;

            // Handle window dragging - use Unity's built-in dragging from title bar
            GUI.DragWindow(titleBarRect);

            if (GUI.changed)
            {
                SaveWindowPosition();
            }
        }

        private float CalculateContentHeight(string text, float width)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            textStyle.wordWrap = true;
            return textStyle.CalcHeight(new GUIContent(text), width);
        }

        private void HandleInputEvents()
        {
            if (!isChatFocused || Event.current?.type != EventType.KeyDown)
                return;

            switch (Event.current.keyCode)
            {
                case KeyCode.Return when !string.IsNullOrEmpty(inputText):
                    SendMessage();
                    Event.current.Use();
                    break;
                case KeyCode.Backspace when inputText.Length > 0:
                    inputText = inputText[..^1];
                    Event.current.Use();
                    break;
                default:
                    if (!char.IsControl(Event.current.character))
                    {
                        inputText += Event.current.character;
                        Event.current.Use();
                    }
                    break;
            }
        }

        private void SendMessage()
        {
            if (string.IsNullOrEmpty(inputText) || isWaitingForResponse)
                return;

            AppendToChatHistory($"You: {inputText}");
            string userMessage = inputText;
            inputText = string.Empty;
            isWaitingForResponse = true;
            MelonCoroutines.Start(SendMessageCoroutine(userMessage));            
        }


        private IEnumerator SendMessageCoroutine(string userMessage)
        {
            var aiManager = Core.GetAIEngineManager();
            string engineName = Config.ENGINE_TYPE.Value;
            string model = Config.MODEL_NAME.Value;
            string systemprompt = Config.SYSTEM_PROMPT.Value;

            systemprompt += string.Format(" You are Responding to {0}, and your name is {1}", Config.NAME.Value, Config.AI_NAME.Value);

            // Add typing message
            string typingMessage = $"{Config.AI_NAME.Value}: typing...";
            AppendToChatHistory(typingMessage);

            // Send the request asynchronously
            var task = aiManager.SendRequestAsync(userMessage, engineName, model, systemprompt);

            // Wait for the task to complete
            while (!task.IsCompleted)
            {
                yield return null;
            }

            // Remove the typing message
            responseText = responseText.Replace($"\n\n{typingMessage}", "");
            if (responseText.EndsWith(typingMessage))
            {
                responseText = responseText.Substring(0, responseText.Length - typingMessage.Length);
            }

            // Process the result
            if (task.Exception == null && task.IsCompletedSuccessfully)
            {
                string assistantMessage = task.Result;
                // Append the assistant's response to the chat history
                AppendToChatHistory($"{Config.AI_NAME.Value}: {assistantMessage}");
            }
            else
            {
                AppendToChatHistory($"Error: {(task.Exception?.Message ?? "Unknown error occurred.")}");
            }

            // Reset the waiting state
            isWaitingForResponse = false;
            LimitChatHistory();

            // run tts engine
            if (task.Exception == null && task.IsCompletedSuccessfully)
            {
                if (Config.ENABLE_TTS.Value)
                {
                    MelonCoroutines.Start(PlayMessageCoroutine(task.Result));
                }
            }
        }
        
        private IEnumerator PlayMessageCoroutine(string responseText)
        {
            var audioManager = Core.GetAudioEngine();
            var taskCompletionSource = new TaskCompletionSource<string>();

            MelonLogger.Msg("[TTS] Converting AI response to speech...");

            MelonCoroutines.Start(ProcessAudioCoroutine(responseText, taskCompletionSource));

            while (!taskCompletionSource.Task.IsCompleted)
            {
                yield return null;
            }

            if (taskCompletionSource.Task.Exception != null)
            {
                MelonLogger.Error($"[TTS] Failed to play: {taskCompletionSource.Task.Exception.Message}");
            }
            else
            {
                string audioPath = taskCompletionSource.Task.Result;
                MelonLogger.Msg($"[TTS] Successfully played audio from path: {audioPath}");

                Core.databaseAudioManager.AddAudioPath(responseText, audioPath);
            }
        }
        
        private IEnumerator ProcessAudioCoroutine(string text, TaskCompletionSource<string> tcs)
        {
            var audioManager = Core.GetAudioEngine();

            var task = audioManager.ProcessAudioAsync(text);

            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.Exception != null)
            {
                tcs.SetException(task.Exception);
            }
            else
            {
                tcs.SetResult(task.Result);
            }
        }

        private void LimitChatHistory()
        {
            string[] lines = responseText.Split('\n');
            if (lines.Length > MaxChatHistory)
            {
                responseText = string.Join("\n", lines.Skip(lines.Length - MaxChatHistory));
            }
        }
        
        private void ClearChat()
        {
            responseText = string.Empty;
            inputText = string.Empty;
            scrollPosition = Vector2.zero;

            Core.databaseManager.ClearMessages();
        }

        private void AppendToChatHistory(string message)
        {
            if (responseText.Length > 0)
                responseText += "\n\n";

            responseText += message;

            // Auto-scroll to bottom
            float contentHeight = textStyle.CalcHeight(new GUIContent(responseText),
                windowRect.width - (Padding * 2) - 10); 
            scrollPosition.y = Mathf.Max(0, contentHeight);
        }

        private void SaveWindowPosition()
        {
            Config.CHAT_WINDOW_X.Value = (int)windowRect.x;
            Config.CHAT_WINDOW_Y.Value = (int)windowRect.y;
            Config.CHAT_WINDOW_WIDTH.Value = (int)windowRect.width;
            Config.CHAT_WINDOW_HEIGHT.Value = (int)windowRect.height;
        }

        public void UpdateSettings()
        {
            textStyle.fontSize = Config.CHAT_WINDOW_FONT_SIZE.Value;
            UpdateWindowRect();
        }
    }
}
