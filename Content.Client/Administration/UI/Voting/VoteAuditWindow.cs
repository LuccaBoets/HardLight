using System.Numerics;
using Content.Client.Administration.UI.CustomControls;
using Content.Client.Voting;
using Content.Shared.Voting;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;

namespace Content.Client.Administration.UI.Voting;

/// <summary>
///     Admin popup that shows a selectable list of recent votes and a per-option
///     player-name breakdown on the right-hand side.
/// </summary>
public sealed class VoteAuditWindow : DefaultWindow
{
    [Dependency] private readonly IVoteManager _voteManager = default!;

    // Left panel – vote list
    private readonly ItemList _voteList;
    private readonly Label _listStatus;

    // Right panel – inspect view
    private readonly Label _inspectTitle;
    private readonly Label _inspectMeta;
    private readonly BoxContainer _optionButtons;   // one button per option
    private readonly ItemList _playerList;
    private readonly Label _playerListLabel;

    // Data
    private readonly List<VoteAuditEntry> _entries = new();
    private VoteAuditOption[]? _currentOptions;
    private int _selectedOption = -1;
    // Latest inspect-request id; responses for older ids are ignored to avoid races.
    private int _pendingInspectId = -1;

    public VoteAuditWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = "Vote Audit";
        MinSize = new Vector2(640, 420);
        SetSize = new Vector2(780, 500);

        // ── Vote list (left) ──────────────────────────────────────────────
        _listStatus = new Label
        {
            Text = "Loading…",
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
        };

        _voteList = new ItemList
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            SelectMode = ItemList.ItemListSelectMode.Single,
        };
        _voteList.OnItemSelected += OnVoteSelected;
        _voteList.Visible = false;

        var refreshButton = new Button
        {
            Text = "Refresh",
            HorizontalAlignment = HAlignment.Right,
        };
        refreshButton.OnPressed += _ => RequestList();

        var leftPanel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            MinWidth = 280,
            HorizontalExpand = true,
        };
        leftPanel.AddChild(new Label { Text = "Recent Votes", StyleClasses = { "LabelHeading" } });
        leftPanel.AddChild(_listStatus);
        leftPanel.AddChild(_voteList);
        leftPanel.AddChild(refreshButton);

        // ── Inspect panel (right) ─────────────────────────────────────────
        _inspectTitle = new Label
        {
            Text = "Select a vote",
            StyleClasses = { "LabelHeading" },
            ClipText = true,
        };

        _inspectMeta = new Label
        {
            StyleClasses = { "LabelSubText" },
            ClipText = true,
        };

        _optionButtons = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4,
        };

        _playerListLabel = new Label
        {
            Text = "Players",
            StyleClasses = { "LabelSubText" },
        };

        _playerList = new ItemList
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            SelectMode = ItemList.ItemListSelectMode.None,
        };

        var rightPanel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            HorizontalExpand = true,
            MinWidth = 300,
        };
        rightPanel.AddChild(_inspectTitle);
        rightPanel.AddChild(_inspectMeta);
        rightPanel.AddChild(new HSeparator());
        rightPanel.AddChild(_optionButtons);
        rightPanel.AddChild(_playerListLabel);
        rightPanel.AddChild(_playerList);

        // ── Root layout ───────────────────────────────────────────────────
        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Margin = new Thickness(8),
        };
        root.AddChild(leftPanel);
        root.AddChild(new Control { MinWidth = 1, HorizontalExpand = false });
        root.AddChild(rightPanel);

        Contents.AddChild(root);

        // Subscribe to the vote manager's response event and fetch list immediately
        _voteManager.VoteAuditResponseReceived += OnResponseReceived;
        RequestList();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _voteManager.VoteAuditResponseReceived -= OnResponseReceived;
    }

    // ── Networking ────────────────────────────────────────────────────────

    private void RequestList()
    {
        _listStatus.Text = "Loading…";
        _listStatus.Visible = true;
        _voteList.Visible = false;
        _voteManager.RequestVoteAuditList();
    }

    private void RequestInspect(int voteId)
    {
        _pendingInspectId = voteId;
        _voteManager.RequestVoteAuditInspect(voteId);
    }

    private void OnResponseReceived(MsgVoteAuditResponse msg)
    {
        if (!msg.IsInspect)
        {
            PopulateList(msg.Votes);
            return;
        }

        // Discard stale inspect responses (user clicked another vote in the meantime).
        if (_pendingInspectId != msg.InspectId)
            return;

        PopulateInspect(msg);
    }

    // ── List panel ────────────────────────────────────────────────────────

    private void PopulateList(VoteAuditEntry[] votes)
    {
        _entries.Clear();
        _voteList.Clear();

        if (votes.Length == 0)
        {
            _listStatus.Text = "No votes found.";
            _listStatus.Visible = true;
            _voteList.Visible = false;
            return;
        }

        _listStatus.Visible = false;
        _voteList.Visible = true;

        foreach (var v in votes)
        {
            _entries.Add(v);
            var label = $"[{v.Id}] {v.Status} – {v.Title}";
            _voteList.AddItem(label);
        }
    }

    private void OnVoteSelected(ItemList.ItemListSelectedEventArgs args)
    {
        if (args.ItemIndex < 0 || args.ItemIndex >= _entries.Count)
            return;

        var entry = _entries[args.ItemIndex];
        // Show placeholder while waiting for server
        _inspectTitle.Text = entry.Title;
        _inspectMeta.Text = $"[{entry.Id}]  {entry.Status}  ·  {entry.Initiator}";
        _playerList.Clear();
        _playerListLabel.Text = "Players – loading…";
        ClearOptionButtons();
        RequestInspect(entry.Id);
    }

    // ── Inspect panel ─────────────────────────────────────────────────────

    private void PopulateInspect(MsgVoteAuditResponse msg)
    {
        _inspectTitle.Text = msg.InspectTitle;
        _inspectMeta.Text = $"[{msg.InspectId}]  {msg.InspectStatus}  ·  {msg.InspectInitiator}";
        _currentOptions = msg.Options;
        _selectedOption = -1;

        ClearOptionButtons();

        if (msg.Options.Length == 0)
        {
            _playerListLabel.Text = "No votes cast.";
            _playerList.Clear();
            return;
        }

        for (var i = 0; i < msg.Options.Length; i++)
        {
            var opt = msg.Options[i];
            var idx = i; // capture for closure
            var btn = new Button
            {
                Text = $"{opt.Text} ({opt.Voters.Length})",
                ToggleMode = true,
            };
            btn.OnToggled += args =>
            {
                if (args.Pressed)
                {
                    SelectOption(idx);
                }
                else if (_selectedOption == idx)
                {
                    // Re-press: the user just clicked the active option button; keep it selected.
                    btn.Pressed = true;
                }
            };
            _optionButtons.AddChild(btn);
        }

        // Auto-select first option
        SelectOption(0);
        if (_optionButtons.ChildCount > 0 && _optionButtons.GetChild(0) is Button first)
            first.Pressed = true;
    }

    private void SelectOption(int idx)
    {
        if (_currentOptions == null || idx < 0 || idx >= _currentOptions.Length)
            return;

        _selectedOption = idx;
        var opt = _currentOptions[idx];

        // Un-press all other buttons
        for (var i = 0; i < _optionButtons.ChildCount; i++)
        {
            if (_optionButtons.GetChild(i) is Button btn && i != idx)
                btn.Pressed = false;
        }

        _playerListLabel.Text = $"Option: {opt.Text}  –  {opt.Voters.Length} vote(s)";
        _playerList.Clear();

        if (opt.Voters.Length == 0)
        {
            _playerList.AddItem("(no votes)");
            return;
        }

        foreach (var name in opt.Voters)
            _playerList.AddItem(name);
    }

    private void ClearOptionButtons()
    {
        _optionButtons.RemoveAllChildren();
    }
}
