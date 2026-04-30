using System;
using System.Numerics;
using Content.Client.Administration.Systems;
using Content.Client.UserInterface.Controls;
using Content.Shared.Administration;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Client.Administration.UI.Bwoink
{
    /// <summary>
    ///     Window for admins to manage a player's bank account.
    ///     Allows viewing current balance and adding/removing money.
    /// </summary>
    public sealed class BankingManagementWindow : DefaultWindow
    {
        private readonly NetUserId _playerUserId;
        private readonly RichTextLabel _balanceLabel;
        private readonly LineEdit _amountInput;
        private readonly Label _reasonLabel;
        private readonly LineEdit _reasonInput;
        private readonly Button _addButton;
        private readonly Button _removeButton;
        private readonly ConfirmButton _confiscateButton;
        private readonly RichTextLabel _statusLabel;
        private int _currentBalance;

        public BankingManagementWindow(string playerName, NetUserId playerUserId, int initialBalance)
        {
            _playerUserId = playerUserId;
            _currentBalance = initialBalance;
            Title = Loc.GetString("bwoink-banking-window-title", ("name", playerName));
            MinSize = new Vector2(400, 300);
            SetSize = new Vector2(500, 350);

            _balanceLabel = new RichTextLabel
            {
                VerticalAlignment = VAlignment.Top,
            };
            UpdateBalanceLabel(initialBalance);

            var amountContainer = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 4,
            };

            amountContainer.AddChild(new Label
            {
                Text = Loc.GetString("bwoink-banking-amount-label"),
                VerticalAlignment = VAlignment.Center,
            });

            _amountInput = new LineEdit
            {
                HorizontalExpand = true,
                PlaceHolder = "0",
            };
            amountContainer.AddChild(_amountInput);

            _reasonLabel = new Label
            {
                Text = Loc.GetString("bwoink-banking-reason-label"),
                VerticalAlignment = VAlignment.Top,
                Margin = new Thickness(0, 4, 0, 0),
            };

            _reasonInput = new LineEdit
            {
                HorizontalExpand = true,
                PlaceHolder = Loc.GetString("bwoink-banking-reason-placeholder"),
                MinHeight = 40,
            };

            var buttonContainer = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 4,
                HorizontalAlignment = HAlignment.Center,
            };

            _addButton = new Button
            {
                Text = Loc.GetString("bwoink-banking-add-button"),
                MinWidth = 80,
                StyleClasses = { "OpenRight" },
            };
            _addButton.OnPressed += _ => OnAddMoney();
            buttonContainer.AddChild(_addButton);

            _removeButton = new Button
            {
                Text = Loc.GetString("bwoink-banking-remove-button"),
                MinWidth = 80,
                StyleClasses = { "OpenLeft" },
            };
            _removeButton.OnPressed += _ => OnRemoveMoney();
            buttonContainer.AddChild(_removeButton);

            _confiscateButton = new ConfirmButton
            {
                Text = Loc.GetString("bwoink-banking-confiscate-button"),
                ConfirmationText = Loc.GetString("bwoink-banking-confiscate-confirm"),
                ToolTip = Loc.GetString("bwoink-banking-confiscate-tooltip"),
                MinWidth = 160,
                Margin = new Thickness(8, 0, 0, 0),
            };
            _confiscateButton.OnPressed += _ => OnConfiscateAll();
            buttonContainer.AddChild(_confiscateButton);

            _statusLabel = new RichTextLabel
            {
                VerticalAlignment = VAlignment.Bottom,
                MinHeight = 40,
            };

            var closeButton = new Button
            {
                Text = Loc.GetString("bwoink-banking-close-button"),
                HorizontalAlignment = HAlignment.Right,
            };
            closeButton.OnPressed += _ => Close();

            var container = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                SeparationOverride = 4,
                Margin = new Thickness(8),
                VerticalExpand = true,
            };

            container.AddChild(_balanceLabel);
            container.AddChild(new BoxContainer { MinHeight = 8 });
            container.AddChild(amountContainer);
            container.AddChild(_reasonLabel);
            container.AddChild(_reasonInput);
            container.AddChild(new BoxContainer { MinHeight = 4 });
            container.AddChild(buttonContainer);
            container.AddChild(_statusLabel);
            container.AddChild(closeButton);

            Contents.AddChild(container);

            // Subscribe to bank modification responses
            try
            {
                var bwoinkSystem = IoCManager.Resolve<IEntityManager>().System<BwoinkSystem>();
                bwoinkSystem.PlayerBankModified += OnBankModified;
            }
            catch
            {
                // Silently continue if system initialization fails
            }
        }

        private void UpdateBalanceLabel(int balance)
        {
            _currentBalance = balance;
            var msg = new FormattedMessage();
            msg.PushColor(Color.White);
            msg.AddText(Loc.GetString("bwoink-banking-balance-label") + " ");
            msg.Pop();
            msg.PushColor(balance >= 0 ? Color.LightGreen : Color.Salmon);
            msg.AddText($"${balance}");
            msg.Pop();
            _balanceLabel.SetMessage(msg);
        }

        private void OnAddMoney()
        {
            if (!int.TryParse(_amountInput.Text, out var amount) || amount <= 0)
            {
                SetStatus(Loc.GetString("bwoink-banking-invalid-amount"), Color.Salmon);
                return;
            }

            var reason = string.IsNullOrWhiteSpace(_reasonInput.Text)
                ? "Admin adjustment"
                : _reasonInput.Text;

            SendBankModification(amount, reason);
        }

        private void OnRemoveMoney()
        {
            if (!int.TryParse(_amountInput.Text, out var amount) || amount <= 0)
            {
                SetStatus(Loc.GetString("bwoink-banking-invalid-amount"), Color.Salmon);
                return;
            }

            var reason = string.IsNullOrWhiteSpace(_reasonInput.Text)
                ? "Admin adjustment"
                : _reasonInput.Text;

            SendBankModification(-amount, reason);
        }

        private void OnConfiscateAll()
        {
            if (_currentBalance <= 0)
            {
                SetStatus(Loc.GetString("bwoink-banking-confiscate-nothing"), Color.Salmon);
                return;
            }

            var reason = string.IsNullOrWhiteSpace(_reasonInput.Text)
                ? Loc.GetString("bwoink-banking-confiscate-reason-default")
                : _reasonInput.Text;

            SendBankModification(-_currentBalance, reason);
        }

        private void SendBankModification(int amount, string reason)
        {
            SetStatus(Loc.GetString("bwoink-banking-sending"), Color.LightGray);
            _addButton.Disabled = true;
            _removeButton.Disabled = true;
            _confiscateButton.Disabled = true;

            try
            {
                var bwoinkSystem = IoCManager.Resolve<IEntityManager>().System<BwoinkSystem>();
                bwoinkSystem.RequestModifyPlayerBank(_playerUserId, amount, reason);
            }
            catch
            {
                SetStatus(Loc.GetString("bwoink-banking-error-send"), Color.Salmon);
                _addButton.Disabled = false;
                _removeButton.Disabled = false;
                _confiscateButton.Disabled = false;
            }
        }

        private void OnBankModified(ModifyPlayerBankResponseMessage response)
        {
            if (!string.Equals(response.OwnerUserId, _playerUserId.UserId.ToString(), StringComparison.Ordinal))
                return;

            _addButton.Disabled = false;
            _removeButton.Disabled = false;
            _confiscateButton.Disabled = false;

            if (!string.IsNullOrEmpty(response.Error))
            {
                SetStatus(Loc.GetString("bwoink-banking-error-result", ("error", response.Error)), Color.Salmon);
                return;
            }

            UpdateBalanceLabel(response.NewBalance);
            _amountInput.Clear();
            _reasonInput.Clear();
            SetStatus(Loc.GetString("bwoink-banking-success"), Color.LightGreen);
        }

        private void SetStatus(string message, Color color)
        {
            var msg = new FormattedMessage();
            msg.PushColor(color);
            msg.AddText(message);
            msg.Pop();
            _statusLabel.SetMessage(msg);
        }
    }
}
