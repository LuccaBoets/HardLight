using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client.Administration.UI.Bwoink
{
    /// <summary>
    ///     Lightweight admin-only popup used to display ahelp triage results
    ///     (ship inspection, player snapshot) without polluting the bwoink chat.
    /// </summary>
    public sealed class TriageInfoWindow : DefaultWindow
    {
        private readonly OutputPanel _output;
        private readonly BoxContainer _buttonRow;
        private readonly Button _closeButton;

        public TriageInfoWindow(string title)
        {
            Title = title;
            MinSize = new Vector2(420, 220);
            SetSize = new Vector2(560, 320);

            _output = new OutputPanel
            {
                VerticalExpand = true,
                HorizontalExpand = true,
            };

            _buttonRow = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 6,
            };

            _closeButton = new Button
            {
                Text = Loc.GetString("bwoink-triage-popup-close"),
                HorizontalAlignment = HAlignment.Right,
            };
            _closeButton.OnPressed += _ => Close();
            _buttonRow.AddChild(_closeButton);

            var container = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                SeparationOverride = 6,
                Margin = new Thickness(8),
            };
            container.AddChild(_output);
            container.AddChild(_buttonRow);

            Contents.AddChild(container);
        }

        public void AddLine(FormattedMessage line) => _output.AddMessage(line);

        public void AddMarkup(string markup)
        {
            var msg = new FormattedMessage(1);
            msg.AddMarkupOrThrow(markup);
            _output.AddMessage(msg);
        }

        public void AddActionButton(string text, Action onPressed, bool closeOnPressed = false)
        {
            var action = new Button
            {
                Text = text,
            };

            action.OnPressed += _ =>
            {
                onPressed();
                if (closeOnPressed)
                    Close();
            };

            _buttonRow.RemoveChild(_closeButton);
            _buttonRow.AddChild(action);
            _buttonRow.AddChild(_closeButton);
        }
    }
}
