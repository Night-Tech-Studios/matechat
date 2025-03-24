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

        private static readonly Color MikuTeal = new Color(0.07f, 0.82f, 0.82f, 0.95f);
        private static readonly Color DarkTeal = new Color(0.05f, 0.4f, 0.4f, 0.95f);
        private static readonly Color WindowBackground = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        private static readonly Color InputBackground = new Color(1, 1, 1, 0.15f);
        private static readonly Color ContentBackground = new Color(1, 1, 1, 0.1f);
        private const int TitleBarHeight = 30;
        private const int InputHeight = 25;
        private const int Padding = 10;
        private const int MaxChatHistory = 50;

        private bool isResizing;
        private bool isMoving;
        private Vector2 resizeStartPosition;
        private Rect originalWindowRect;
        private ResizeDirection currentResizeDirection;
        private Vector2 dragOffset;

        private enum ResizeDirection
        {
            None,
            Top,
            Bottom,
            Left,
            Right,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }

        private const int ResizeHandleSize = 8;
        private const int MinWindowWidth = 200;
        private const int MinWindowHeight = 150;

        public ChatFeature() : base("Chat", Config.CHAT_KEYBIND.Value)
        {
            textStyle = new GUIStyle
            {
                normal = { textColor = Color.white },
                fontSize = Config.CHAT_WINDOW_FONT_SIZE.Value,
                wordWrap = true
            };
            UpdateWindowRect();
            UpdateSettings();
        }

        public void UpdateWindowRect()
        {
            windowRect = new Rect(
                Config.CHAT_WINDOW_X.Value, Config.CHAT_WINDOW_Y.Value,
                Config.CHAT_WINDOW_WIDTH.Value, Config.CHAT_WINDOW_HEIGHT.Value);
        }

        public void DrawGUI()
        {
            if (!IsEnabled)
                return;
            if (Event.current?.type == EventType.MouseDown)
            {
                isChatFocused = windowRect.Contains(Event.current.mousePosition);
            }
            DrawWindow();
        }

        private void DrawWindow()
        {
            Color originalBgColor = GUI.backgroundColor;
            DrawShadow();
            DrawMainWindow();
            DrawTitleBar();
            DrawChatContent();
            DrawInputArea();
            HandleWindowDragAndResize();
            GUI.backgroundColor = originalBgColor;
        }

        private void DrawChatContent()
        {
            GUI.backgroundColor = ContentBackground;
            Rect contentRect = new Rect(
                windowRect.x + Padding,
                windowRect.y + TitleBarHeight + Padding,
                windowRect.width - (Padding * 2),
                windowRect.height - TitleBarHeight - InputHeight - (Padding * 3));

            GUI.Box(contentRect, string.Empty);

            // Calculate content height
            float contentHeight = textStyle.CalcHeight(new GUIContent(responseText),
                contentRect.width - 10); // 10 for scrollbar width

            // Only allow manual scrolling when not waiting for response
            if (contentRect.Contains(Event.current.mousePosition) && !isWaitingForResponse)
            {
                float scroll = Input.mouseScrollDelta.y * 20f;
                scrollPosition.y = Mathf.Clamp(scrollPosition.y - scroll, 0,
                    Mathf.Max(0, contentHeight - contentRect.height));
            }

            GUI.BeginGroup(contentRect);
            GUI.Label(new Rect(5, -scrollPosition.y, contentRect.width - 10,
                Mathf.Max(contentRect.height, contentHeight)),
                responseText, textStyle);
            GUI.EndGroup();
        }

        private void DrawShadow()
        {
            GUI.backgroundColor = Color.black;
            GUI.Box(new Rect(windowRect.x + 2, windowRect.y + 2, windowRect.width,
                             windowRect.height), string.Empty);
        }

        private void DrawMainWindow()
        {
            GUI.backgroundColor = WindowBackground;
            GUI.Box(windowRect, string.Empty);
        }

        private void DrawTitleBar()
        {
            Rect titleBarRect = new Rect(windowRect.x, windowRect.y, windowRect.width,
                                         TitleBarHeight);
            GUI.backgroundColor = MikuTeal;
            GUI.Box(titleBarRect, string.Empty);
            GUI.Label(new Rect(windowRect.x + 60, windowRect.y + 5,
                               windowRect.width - 120, 20), "✧ Mate Chat ♪ ✧");

            Rect clearButtonRect = new Rect(windowRect.x + windowRect.width - 55,
                                            windowRect.y + 5, 50, 20);
            GUI.backgroundColor =
                clearButtonRect.Contains(Event.current.mousePosition)
                    ? new Color(DarkTeal.r * 1.2f, DarkTeal.g * 1.2f,
                                DarkTeal.b * 1.2f, DarkTeal.a)
                    : DarkTeal;
            if (GUI.Button(clearButtonRect, "Clear"))
            {
                ClearChat();
            }
        }

        private void DrawInputArea()
        {
            GUI.backgroundColor = InputBackground;
            Rect inputRect = new Rect(windowRect.x + Padding,
                                      windowRect.y + windowRect.height - InputHeight - Padding,
                                      windowRect.width - 90, InputHeight);
            GUI.Box(inputRect, string.Empty);
            
            // Create a text area with word wrap instead of a simple label
            GUIStyle inputStyle = new GUIStyle();
            inputStyle.normal.textColor = textStyle.normal.textColor;
            inputStyle.fontSize = textStyle.fontSize;
            inputStyle.wordWrap = true;
            inputStyle.clipping = TextClipping.Clip;
            
            // Calculate text dimensions to determine if scrolling is needed
            float textWidth = inputStyle.CalcSize(new GUIContent(inputText)).x;
            float visibleWidth = inputRect.width - 10; // 10 for some padding
            
            // Draw text with horizontal scrolling if needed
            float scrollX = Mathf.Max(0, textWidth - visibleWidth);
            GUI.BeginClip(inputRect);
            GUI.Label(new Rect(5 - (textWidth > visibleWidth ? scrollX : 0), 0, 
                               Mathf.Max(visibleWidth, textWidth), InputHeight), 
                      inputText, inputStyle);
            GUI.EndClip();
            
            HandleInputEvents();
            DrawSendButton(inputRect);
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

        private void DrawSendButton(Rect inputRect)
        {
            GUI.backgroundColor = MikuTeal;
            Rect sendButtonRect = new Rect(inputRect.x + inputRect.width + 10,
                                           inputRect.y, 60, InputHeight);
            if (GUI.Button(sendButtonRect, "♪ Send") && !string.IsNullOrEmpty(inputText))
            {
                SendMessage();
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

        public void UpdateSettings()
        {
            textStyle.fontSize = Config.CHAT_WINDOW_FONT_SIZE.Value;
            UpdateWindowRect();
        }

        private void HandleWindowDragAndResize()
        {
            Event e = Event.current;

            if (e.type == EventType.MouseDown)
            {
                // Check if mouse is in resize area
                ResizeDirection direction = GetResizeDirection(e.mousePosition);

                if (direction != ResizeDirection.None)
                {
                    isResizing = true;
                    isMoving = false;
                    currentResizeDirection = direction;
                    resizeStartPosition = e.mousePosition;
                    originalWindowRect = windowRect;
                    e.Use();
                }
                // Check if mouse is in title bar for moving
                else if (IsInTitleBar(e.mousePosition))
                {
                    isMoving = true;
                    isResizing = false;
                    dragOffset = e.mousePosition - new Vector2(windowRect.x, windowRect.y);
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseUp)
            {
                if (isResizing || isMoving)
                {
                    isResizing = false;
                    isMoving = false;

                    // Save window position and size to config
                    Config.CHAT_WINDOW_X.Value = (int)windowRect.x;
                    Config.CHAT_WINDOW_Y.Value = (int)windowRect.y;
                    Config.CHAT_WINDOW_WIDTH.Value = (int)windowRect.width;
                    Config.CHAT_WINDOW_HEIGHT.Value = (int)windowRect.height;

                    e.Use();
                }
            }
            else if (e.type == EventType.MouseDrag)
            {
                if (isResizing)
                {
                    ResizeWindow(e.mousePosition);
                    e.Use();
                }
                else if (isMoving)
                {
                    windowRect.x = e.mousePosition.x - dragOffset.x;
                    windowRect.y = e.mousePosition.y - dragOffset.y;
                    e.Use();
                }
            }

            // Change cursor based on mouse position
            if (!isResizing && !isMoving)
            {
                UpdateCursor(e.mousePosition);
            }

            // Draw resize handles
            DrawResizeHandles();
        }

        private void DrawResizeHandles()
        {
            // Only draw handles when the mouse is over the window for cleaner UI
            if (!windowRect.Contains(Event.current.mousePosition) && !isResizing)
                return;

            Color handleColor = new Color(MikuTeal.r, MikuTeal.g, MikuTeal.b, 0.5f);
            GUI.backgroundColor = handleColor;

            // Draw corner handles
            GUI.Box(new Rect(windowRect.x, windowRect.y, ResizeHandleSize, ResizeHandleSize), string.Empty);
            GUI.Box(new Rect(windowRect.x + windowRect.width - ResizeHandleSize, windowRect.y, ResizeHandleSize, ResizeHandleSize), string.Empty);
            GUI.Box(new Rect(windowRect.x, windowRect.y + windowRect.height - ResizeHandleSize, ResizeHandleSize, ResizeHandleSize), string.Empty);
            GUI.Box(new Rect(windowRect.x + windowRect.width - ResizeHandleSize, windowRect.y + windowRect.height - ResizeHandleSize, ResizeHandleSize, ResizeHandleSize), string.Empty);

            // Draw edge handles
            GUI.Box(new Rect(windowRect.x + ResizeHandleSize, windowRect.y, windowRect.width - 2 * ResizeHandleSize, ResizeHandleSize), string.Empty);
            GUI.Box(new Rect(windowRect.x + ResizeHandleSize, windowRect.y + windowRect.height - ResizeHandleSize, windowRect.width - 2 * ResizeHandleSize, ResizeHandleSize), string.Empty);
            GUI.Box(new Rect(windowRect.x, windowRect.y + ResizeHandleSize, ResizeHandleSize, windowRect.height - 2 * ResizeHandleSize), string.Empty);
            GUI.Box(new Rect(windowRect.x + windowRect.width - ResizeHandleSize, windowRect.y + ResizeHandleSize, ResizeHandleSize, windowRect.height - 2 * ResizeHandleSize), string.Empty);
        }

        private ResizeDirection GetResizeDirection(Vector2 mousePosition)
        {
            bool isLeft = mousePosition.x >= windowRect.x && mousePosition.x <= windowRect.x + ResizeHandleSize;
            bool isRight = mousePosition.x >= windowRect.x + windowRect.width - ResizeHandleSize && mousePosition.x <= windowRect.x + windowRect.width;
            bool isTop = mousePosition.y >= windowRect.y && mousePosition.y <= windowRect.y + ResizeHandleSize;
            bool isBottom = mousePosition.y >= windowRect.y + windowRect.height - ResizeHandleSize && mousePosition.y <= windowRect.y + windowRect.height;

            if (isTop && isLeft) return ResizeDirection.TopLeft;
            if (isTop && isRight) return ResizeDirection.TopRight;
            if (isBottom && isLeft) return ResizeDirection.BottomLeft;
            if (isBottom && isRight) return ResizeDirection.BottomRight;
            if (isTop) return ResizeDirection.Top;
            if (isBottom) return ResizeDirection.Bottom;
            if (isLeft) return ResizeDirection.Left;
            if (isRight) return ResizeDirection.Right;

            return ResizeDirection.None;
        }

        private void ResizeWindow(Vector2 currentPosition)
        {
            Vector2 delta = currentPosition - resizeStartPosition;
            float newWidth = originalWindowRect.width;
            float newHeight = originalWindowRect.height;
            float newX = originalWindowRect.x;
            float newY = originalWindowRect.y;

            switch (currentResizeDirection)
            {
                case ResizeDirection.Right:
                    newWidth += delta.x;
                    break;
                case ResizeDirection.Left:
                    newWidth -= delta.x;
                    newX += delta.x;
                    break;
                case ResizeDirection.Bottom:
                    newHeight += delta.y;
                    break;
                case ResizeDirection.Top:
                    newHeight -= delta.y;
                    newY += delta.y;
                    break;
                case ResizeDirection.TopLeft:
                    newWidth -= delta.x;
                    newHeight -= delta.y;
                    newX += delta.x;
                    newY += delta.y;
                    break;
                case ResizeDirection.TopRight:
                    newWidth += delta.x;
                    newHeight -= delta.y;
                    newY += delta.y;
                    break;
                case ResizeDirection.BottomLeft:
                    newWidth -= delta.x;
                    newHeight += delta.y;
                    newX += delta.x;
                    break;
                case ResizeDirection.BottomRight:
                    newWidth += delta.x;
                    newHeight += delta.y;
                    break;
            }

            // Enforce minimum window size
            if (newWidth < MinWindowWidth)
            {
                if (newX != originalWindowRect.x)
                    newX = originalWindowRect.x + originalWindowRect.width - MinWindowWidth;
                newWidth = MinWindowWidth;
            }

            if (newHeight < MinWindowHeight)
            {
                if (newY != originalWindowRect.y)
                    newY = originalWindowRect.y + originalWindowRect.height - MinWindowHeight;
                newHeight = MinWindowHeight;
            }

            windowRect = new Rect(newX, newY, newWidth, newHeight);
        }

        private bool IsInTitleBar(Vector2 position)
        {
            return position.x >= windowRect.x && position.x <= windowRect.x + windowRect.width &&
                   position.y >= windowRect.y && position.y <= windowRect.y + TitleBarHeight;
        }

        private void UpdateCursor(Vector2 mousePosition)
        {
            ResizeDirection direction = GetResizeDirection(mousePosition);

            switch (direction)
            {
                case ResizeDirection.Left:
                case ResizeDirection.Right:
                    Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                    break;
                case ResizeDirection.Top:
                case ResizeDirection.Bottom:
                    Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                    break;
                case ResizeDirection.TopLeft:
                case ResizeDirection.BottomRight:
                    Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                    break;
                case ResizeDirection.TopRight:
                case ResizeDirection.BottomLeft:
                    Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                    break;
                default:
                    if (IsInTitleBar(mousePosition))
                        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                    else
                        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                    break;
            }
        }
    }
}
