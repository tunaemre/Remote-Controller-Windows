using Newtonsoft.Json.Linq;
using System;
using System.Security.Authentication;

namespace RemoteController.Desktop
{
    public class BaseAction
    {
        public ActionType action { get; internal set; }

        public BaseAction(JObject jsonObject)
        {
            string actionString = jsonObject["Action"].ToString();

            ActionType tempAction;
            if (!Enum.TryParse(actionString, out tempAction))
                throw new InvalidCastException();

            this.action = tempAction;
        }

        public enum ActionType
        {
            Hello,
            Authenticate,
            Goodbye,
            ScreenRecognize,
            Clipboard,
            Text,
            MouseMove,
            MouseMoveRelative,
            MouseDown,
            MouseUp,
            MouseClick,
            MouseDrag,
            MouseDragRelative,
            Scroll,
            VolumeUp,
            VolumeDown,
            VolumeMute,
            MediaPlayPause,
            MediaStop,
            MediaNextTrack,
            MediaPrevTrack,
            MediaFastForward,
            MediaFastRewind
        }

        public enum MouseButtonType
        {
            Left,
            Right
        }

        public enum ScrollDirectionType
        {
            Vertical,
            Horizontal
        }
    }

    public class AuthBaseAction : BaseAction
    {
        public string secret { get; internal set; }

        public AuthBaseAction(JObject obj, bool checkSecret) : base(obj)
        {
            this.secret = obj["Secret"].ToString();
            if (!this.secret.Equals(MainWindow.CurrentToken))
                throw new AuthenticationException();
        }
    }

    public class HelloCommand : BaseAction
    {
        public string pin { get; internal set; }
       
        public HelloCommand(JObject obj) : base(obj)
        {
            this.pin = obj["PIN"].ToString();
        }
    }

    public class ScreenRecognizeCommand : AuthBaseAction
    {
        public int remoteScreenX { get; internal set; }
        public int remoteScreenY { get; internal set; }

        public ScreenRecognizeCommand(JObject obj) : base(obj, true)
        {
            this.remoteScreenX = obj["X"].ToObject<int>();
            this.remoteScreenY = obj["Y"].ToObject<int>();
        }
    }

    public class ClipboardCommand : AuthBaseAction
    {
        public string data { get; internal set; }

        public ClipboardCommand(JObject obj) : base(obj, true)
        {
            this.data = obj["Data"].ToString();
        }
    }

    public class TextCommand : AuthBaseAction
    {
        public string data { get; internal set; }

        public TextCommand(JObject obj) : base(obj, true)
        {
            this.data = obj["Data"].ToString();
        }
    }

    public class MouseMoveCommand : AuthBaseAction
    {
        public int x { get; internal set; }
        public int y { get; internal set; }

        public MouseMoveCommand(JObject obj) : base(obj, true)
        {
            this.x = obj["X"].ToObject<int>();
            this.y = obj["Y"].ToObject<int>();
        }
    }
   
    public class MouseClickCommand : AuthBaseAction
    {
        public MouseButtonType button { get; internal set; }

        public MouseClickCommand(JObject obj) : base(obj, true)
        {
            string buttonString = obj["Button"].ToString();

            MouseButtonType tempButtonType;
            if (!Enum.TryParse(buttonString, out tempButtonType))
                throw new InvalidCastException();
            
            this.button = tempButtonType;
        }
    }

    public class MouseDragCommand : AuthBaseAction
    {
        public int x { get; internal set; }
        public int y { get; internal set; }
        public MouseButtonType button { get; set; }

        public MouseDragCommand(JObject obj) : base(obj, true)
        {
            this.x = obj["X"].ToObject<int>();
            this.y = obj["Y"].ToObject<int>();

            string buttonString = obj["Button"].ToString();

            MouseButtonType tempButton;
            if (!Enum.TryParse(buttonString, out tempButton))
                throw new InvalidCastException();

            this.button = tempButton;
        }
    }

    public class ScrollCommand : AuthBaseAction
    {
        public int amount { get; internal set; }
        public ScrollDirectionType direction { get; set; }

        public ScrollCommand(JObject obj) : base(obj, true)
        {
            this.amount = obj["Amount"].ToObject<int>();

            string directionString = obj["Direction"].ToString();

            ScrollDirectionType tempDirection;
            if (!Enum.TryParse(directionString, out tempDirection))
                throw new InvalidCastException();

            this.direction = tempDirection;
        }
    }
}
