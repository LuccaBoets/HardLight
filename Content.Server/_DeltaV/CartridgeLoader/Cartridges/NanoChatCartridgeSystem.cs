// SPDX-FileCopyrightText: 2024 Skubman <ba.fallaria@gmail.com>
// SPDX-FileCopyrightText: 2024 Tadeo <td12233a@gmail.com>
// SPDX-FileCopyrightText: 2025 Evaisa <evagiacosa1@gmail.com>
// SPDX-FileCopyrightText: 2025 EvaisaDev <mail@evaisa.dev>
// SPDX-FileCopyrightText: 2025 Icepick <122653407+Icepicked@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 corresp0nd <46357632+corresp0nd@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later AND MIT

using System.Linq;
using Content.Server.Access.Systems;
using Content.Server.Administration.Logs;
using Content.Server.CartridgeLoader;
using Content.Server.GameTicking.Events;
using Content.Server.Radio;
using Content.Server.Radio.Components;
using Content.Server.Station.Systems;
using Content.Shared.Access.Components;
using Content.Shared.CartridgeLoader;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared._DeltaV.CartridgeLoader.Cartridges;
using Content.Shared._DeltaV.NanoChat;
using Content.Shared.PDA;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;


namespace Content.Server._DeltaV.CartridgeLoader.Cartridges;

public sealed class NanoChatCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridge = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedNanoChatSystem _nanoChat = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly StationSystem _station = default!;

    private ISawmill _sawmill = default!;

    // Messages in notifications get cut off after this point
    // no point in storing it on the comp
    private const int NotificationMaxLength = 64;

    // The max length of the name and job title on the notification before being truncated.
    private const int NotificationTitleMaxLength = 32;

    private readonly Dictionary<uint, HashSet<EntityUid>> _cardsByNumber = [];
    private readonly Dictionary<EntityUid, uint> _cardNumbers = [];
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _cartridgesByCard = [];
    private readonly Dictionary<EntityUid, EntityUid?> _trackedCardByCartridge = [];
    // Cache of cards with ListNumber=true: cardUid → (number, name). Avoids full entity scan in UpdateUI.
    private readonly Dictionary<EntityUid, (uint Number, string Name)> _listedCardInfo = [];
    private bool _cardNumberCacheInitialized;
    private bool _cartridgeTrackingCacheInitialized;
    private bool _listedCardInfoCacheInitialized;



    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("nanochat");

        SubscribeLocalEvent<NanoChatCardComponent, ComponentStartup>(OnCardStartup);
        SubscribeLocalEvent<NanoChatCardComponent, ComponentShutdown>(OnCardShutdown);
        SubscribeLocalEvent<NanoChatCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<NanoChatCartridgeComponent, ComponentStartup>(OnCartridgeStartup);
        SubscribeLocalEvent<NanoChatCartridgeComponent, ComponentShutdown>(OnCartridgeShutdown);
        SubscribeLocalEvent<NanoChatCartridgeComponent, CartridgeMessageEvent>(OnMessage);
        SubscribeLocalEvent<NanoChatCardComponent, NanoChatNumberChangedEvent>(OnCardNumberChanged);
        SubscribeLocalEvent<EntityRenamedEvent>(OnEntityRenamed, after: new[] { typeof(IdCardSystem) });
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _cardsByNumber.Clear();
        _cardNumbers.Clear();
        _cartridgesByCard.Clear();
        _trackedCardByCartridge.Clear();
        _listedCardInfo.Clear();
        _cardNumberCacheInitialized = false;
        _cartridgeTrackingCacheInitialized = false;
        _listedCardInfoCacheInitialized = false;
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        // Round startup is the safest point to repopulate from live entities.
        RebuildCardNumberCache();
        RebuildCartridgeTrackingCache();
        RebuildListedCache();
    }

    private void OnCardStartup(Entity<NanoChatCardComponent> ent, ref ComponentStartup args)
    {
        RegisterCardNumber(ent.Owner, ent.Comp.Number);
        if (TryUpdateListedCache(ent.Owner, ent.Comp))
            UpdateUIForAllCards();
    }

    private void OnCardShutdown(Entity<NanoChatCardComponent> ent, ref ComponentShutdown args)
    {
        UnregisterCardNumber(ent.Owner, ent.Comp.Number);
        if (_listedCardInfo.Remove(ent.Owner))
            UpdateUIForAllCards();
    }

    private void OnCartridgeStartup(Entity<NanoChatCartridgeComponent> ent, ref ComponentStartup args)
    {
        TrackCartridgeCard(ent.Owner, ent.Comp.Card);
    }

    private void OnCartridgeShutdown(Entity<NanoChatCartridgeComponent> ent, ref ComponentShutdown args)
    {
        TrackCartridgeCard(ent.Owner, null);
        _trackedCardByCartridge.Remove(ent.Owner);
    }

    private void OnCardNumberChanged(EntityUid uid, NanoChatCardComponent comp, ref NanoChatNumberChangedEvent args)
    {
        UnregisterCardNumber(args.CardUid, args.OldNumber);
        RegisterCardNumber(args.CardUid, args.NewNumber);
        if (TryUpdateListedCache(args.CardUid, comp))
            UpdateUIForAllCards();
    }

    private void OnEntityRenamed(ref EntityRenamedEvent ev)
    {
        if (!TryComp<NanoChatCardComponent>(ev.Uid, out var card))
            return;

        if (TryUpdateListedCache(ev.Uid, card))
            UpdateUIForAllCards();
    }

    private bool TryUpdateListedCache(EntityUid cardUid, NanoChatCardComponent card)
    {
        var hadEntry = _listedCardInfo.TryGetValue(cardUid, out var previous);

        if (!card.ListNumber ||
            card.Number is not uint number ||
            !TryComp<IdCardComponent>(cardUid, out var idCard) ||
            idCard.FullName is not string name)
        {
            if (!hadEntry)
                return false;

            _listedCardInfo.Remove(cardUid);
            return true;
        }

        if (hadEntry && previous.Number == number && previous.Name == name)
            return false;

        _listedCardInfo[cardUid] = (number, name);
        return true;
    }

    /// <summary>
    ///     Rebuilds listed contacts from live entities when cache entries are missing due to init ordering.
    /// </summary>
    private void RebuildListedCache()
    {
        _listedCardInfo.Clear();

        var query = EntityQueryEnumerator<NanoChatCardComponent, IdCardComponent>();
        while (query.MoveNext(out var cardUid, out var nanoChatCard, out var idCard))
        {
            if (!nanoChatCard.ListNumber || nanoChatCard.Number is not uint number)
                continue;

            // Use FullName if available, otherwise use number as placeholder.
            // This ensures cards appear in contacts even if identity hasn't resolved yet,
            // avoiding the stale-name issue after round restarts with carryover entities.
            var name = idCard.FullName ?? $"#{number:D4}";
            _listedCardInfo[cardUid] = (number, name);
        }

        _listedCardInfoCacheInitialized = true;
    }

    /// <summary>
    ///     Fully rebuilds card-number indexes from live entities.
    /// </summary>
    private void RebuildCardNumberCache()
    {
        _cardsByNumber.Clear();
        _cardNumbers.Clear();

        var query = EntityQueryEnumerator<NanoChatCardComponent>();
        while (query.MoveNext(out var cardUid, out var card))
        {
            RegisterCardNumber(cardUid, card.Number);
        }

        _cardNumberCacheInitialized = true;
    }

    /// <summary>
    ///     Fully rebuilds cartridge-to-card tracking from live entities.
    /// </summary>
    private void RebuildCartridgeTrackingCache()
    {
        _cartridgesByCard.Clear();
        _trackedCardByCartridge.Clear();

        var query = EntityQueryEnumerator<NanoChatCartridgeComponent>();
        while (query.MoveNext(out var cartridgeUid, out var cartridge))
        {
            TrackCartridgeCard(cartridgeUid, cartridge.Card);
        }

        _cartridgeTrackingCacheInitialized = true;
    }

    /// <summary>
    ///     Rebuilds card-number indexes when startup events were missed (e.g. round carryover entities).
    /// </summary>
    private void EnsureCardNumberCachePopulated()
    {
        if (_cardNumberCacheInitialized)
            return;

        RebuildCardNumberCache();
    }

    /// <summary>
    ///     Rebuilds cartridge-to-card tracking when startup events were missed (e.g. round carryover entities).
    /// </summary>
    private void EnsureCartridgeTrackingCachePopulated()
    {
        if (_cartridgeTrackingCacheInitialized)
            return;

        RebuildCartridgeTrackingCache();
    }

    private void RegisterCardNumber(EntityUid cardUid, uint? number)
    {
        if (number is not uint currentNumber)
            return;

        if (!_cardsByNumber.TryGetValue(currentNumber, out var cards))
        {
            cards = [];
            _cardsByNumber[currentNumber] = cards;
        }

        cards.Add(cardUid);
        _cardNumbers[cardUid] = currentNumber;
    }

    private void UnregisterCardNumber(EntityUid cardUid, uint? number)
    {
        if (number is not uint currentNumber)
            return;

        if (_cardsByNumber.TryGetValue(currentNumber, out var cards))
        {
            cards.Remove(cardUid);
            if (cards.Count == 0)
                _cardsByNumber.Remove(currentNumber);
        }

        if (_cardNumbers.TryGetValue(cardUid, out var trackedNumber) && trackedNumber == currentNumber)
            _cardNumbers.Remove(cardUid);
    }

    private void TrackCartridgeCard(EntityUid cartridgeUid, EntityUid? newCard)
    {
        if (_trackedCardByCartridge.TryGetValue(cartridgeUid, out var currentCard) && currentCard == newCard)
            return;

        if (currentCard is { } oldCard && _cartridgesByCard.TryGetValue(oldCard, out var cartridges))
        {
            cartridges.Remove(cartridgeUid);
            if (cartridges.Count == 0)
                _cartridgesByCard.Remove(oldCard);
        }

        _trackedCardByCartridge[cartridgeUid] = newCard;

        if (newCard is not { } cardUid)
            return;

        if (!_cartridgesByCard.TryGetValue(cardUid, out var cardCartridges))
        {
            cardCartridges = [];
            _cartridgesByCard[cardUid] = cardCartridges;
        }

        cardCartridges.Add(cartridgeUid);
    }

    private void UpdateGroupRecipientForMembers(uint chatNumber, NanoChatRecipient updatedRecipient, IEnumerable<uint> members)
    {
        foreach (var memberNumber in members)
        {
            if (!_cardsByNumber.TryGetValue(memberNumber, out var memberCardUids))
                continue;

            foreach (var memberCardUid in memberCardUids)
            {
                if (!TryComp<NanoChatCardComponent>(memberCardUid, out var memberCard))
                    continue;

                _nanoChat.SetRecipient((memberCardUid, memberCard), chatNumber, updatedRecipient);
                UpdateUIForCard(memberCardUid);
            }
        }
    }

    private void UpdateClosed(Entity<NanoChatCartridgeComponent> ent)
    {
        if (!TryComp<CartridgeComponent>(ent, out var cartridge) ||
            cartridge.LoaderUid is not { } pda ||
            !TryComp<CartridgeLoaderComponent>(pda, out var loader) ||
            !GetCardEntity(pda, out var card))
        {
            return;
        }

        // if you switch to another program or close the pda UI, allow notifications for the selected chat
        _nanoChat.SetClosed((card, card.Comp), loader.ActiveProgram != ent.Owner || !_ui.IsUiOpen(pda, PdaUiKey.Key));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Update card references for any cartridges that need it
        var query = EntityQueryEnumerator<NanoChatCartridgeComponent, CartridgeComponent>();
        while (query.MoveNext(out var uid, out var nanoChat, out var cartridge))
        {
            if (cartridge.LoaderUid == null)
            {
                if (nanoChat.Card != null)
                {
                    nanoChat.Card = null;
                    TrackCartridgeCard(uid, null);
                }

                continue;
            }

            // keep it up to date without handling ui open/close events on the pda or adding code when changing active program
            UpdateClosed((uid, nanoChat));

            // Check if we need to update our card reference
            if (!TryComp<PdaComponent>(cartridge.LoaderUid, out var pda))
                continue;

            var newCard = pda.ContainedId;
            var currentCard = nanoChat.Card;

            // If the cards match, nothing to do
            if (newCard == currentCard)
                continue;

            // Update card reference
            nanoChat.Card = newCard;
            TrackCartridgeCard(uid, newCard);

            // Update UI state since card reference changed
            UpdateUI((uid, nanoChat), cartridge.LoaderUid.Value);
        }
    }

    /// <summary>
    ///     Handles incoming UI messages from the NanoChat cartridge.
    /// </summary>
    private void OnMessage(Entity<NanoChatCartridgeComponent> ent, ref CartridgeMessageEvent args)
    {
        if (args is not NanoChatUiMessageEvent msg)
            return;

        if (!GetCardEntity(GetEntity(args.LoaderUid), out var card))
            return;

        switch (msg.Type)
        {
            case NanoChatUiMessageType.NewChat:
                HandleNewChat(card, msg);
                break;
            case NanoChatUiMessageType.SelectChat:
                HandleSelectChat(card, msg);
                break;
            case NanoChatUiMessageType.EditChat:
                HandleEditChat(card, msg);
                break;
            case NanoChatUiMessageType.CloseChat:
                HandleCloseChat(card);
                break;
            case NanoChatUiMessageType.ToggleMute:
                HandleToggleMute(card);
                break;
            case NanoChatUiMessageType.ToggleMuteChat:
                HandleToggleMuteChat(card, msg);
                break;
            case NanoChatUiMessageType.DeleteChat:
                HandleDeleteChat(card, msg);
                break;
            case NanoChatUiMessageType.SendMessage:
                HandleSendMessage(ent, card, msg);
                break;
            case NanoChatUiMessageType.ToggleListNumber:
                HandleToggleListNumber(card);
                break;
            // Funky Station Start - Group Chat Functionality
            case NanoChatUiMessageType.CreateGroupChat:
                HandleCreateGroupChat(card, msg);
                break;
            case NanoChatUiMessageType.InviteToGroup:
                HandleInviteToGroup(card, msg);
                break;
            case NanoChatUiMessageType.KickFromGroup:
                HandleKickFromGroup(card, msg);
                break;
            case NanoChatUiMessageType.AdminUser:
                HandleAdminUser(card, msg);
                break;
            case NanoChatUiMessageType.DeadminUser:
                HandleDeadminUser(card, msg);
                break;
            // Funky Station End - Group Chat Functionality
        }

        UpdateUI(ent, GetEntity(args.LoaderUid));
    }

    /// <summary>
    ///     Gets the ID card entity associated with a PDA.
    /// </summary>
    /// <param name="loaderUid">The PDA entity ID</param>
    /// <param name="card">Output parameter containing the found card entity and component</param>
    /// <returns>True if a valid NanoChat card was found</returns>
    private bool GetCardEntity(
        EntityUid loaderUid,
        out Entity<NanoChatCardComponent> card)
    {
        card = default;

        // Get the PDA and check if it has an ID card
        if (!TryComp<PdaComponent>(loaderUid, out var pda) ||
            pda.ContainedId == null ||
            !TryComp<NanoChatCardComponent>(pda.ContainedId, out var idCard))
            return false;

        card = (pda.ContainedId.Value, idCard);
        return true;
    }

    /// <summary>
    ///     Handles creation of a new chat conversation.
    /// </summary>
    private void HandleNewChat(Entity<NanoChatCardComponent> card, NanoChatUiMessageEvent msg)
    {
        if (msg.RecipientNumber == null || msg.Content == null || msg.RecipientNumber == card.Comp.Number)
            return;

        var name = msg.Content;
        if (!string.IsNullOrWhiteSpace(name))
        {
            name = name.Trim();
            if (name.Length > IdCardConsoleComponent.MaxFullNameLength)
                name = name[..IdCardConsoleComponent.MaxFullNameLength];
        }

        var jobTitle = msg.RecipientJob;
        if (!string.IsNullOrWhiteSpace(jobTitle))
        {
            jobTitle = jobTitle.Trim();
            if (jobTitle.Length > IdCardConsoleComponent.MaxJobTitleLength)
                jobTitle = jobTitle[..IdCardConsoleComponent.MaxJobTitleLength];
        }

        // Add new recipient
        var recipient = new NanoChatRecipient(msg.RecipientNumber.Value,
            name,
            jobTitle);

        // Initialize or update recipient
        _nanoChat.SetRecipient((card, card.Comp), msg.RecipientNumber.Value, recipient);

        _adminLogger.Add(LogType.Action,
            LogImpact.Low,
            $"{ToPrettyString(msg.Actor):user} created new NanoChat conversation with #{msg.RecipientNumber:D4} ({name})");

        var recipientEv = new NanoChatRecipientUpdatedEvent(card);
        RaiseLocalEvent(ref recipientEv);
        UpdateUIForCard(card);
    }

    /// <summary>
    ///     Handles selecting a chat conversation.
    /// </summary>
    private void HandleSelectChat(Entity<NanoChatCardComponent> card, NanoChatUiMessageEvent msg)
    {
        if (msg.RecipientNumber == null)
            return;

        _nanoChat.SetCurrentChat((card, card.Comp), msg.RecipientNumber);

        // Clear unread flag when selecting chat
        if (_nanoChat.GetRecipient((card, card.Comp), msg.RecipientNumber.Value) is { } recipient)
        {
            _nanoChat.SetRecipient((card, card.Comp),
                msg.RecipientNumber.Value,
                recipient with { HasUnread = false });
        }
    }

    /// <summary>
    ///     Handles editing the current chat conversation.
    /// </summary>
    private void HandleEditChat(Entity<NanoChatCardComponent> card, NanoChatUiMessageEvent msg)
    {
        if (msg.RecipientNumber == null || msg.Content == null || msg.RecipientNumber == card.Comp.Number ||
            _nanoChat.GetRecipient((card, card.Comp), msg.RecipientNumber.Value) is not { } recipient)
            return;

        var name = msg.Content;
        if (!string.IsNullOrWhiteSpace(name))
        {
            name = name.Trim();
            if (name.Length > IdCardConsoleComponent.MaxFullNameLength)
                name = name[..IdCardConsoleComponent.MaxFullNameLength];
        }

        var jobTitle = msg.RecipientJob;
        if (!string.IsNullOrWhiteSpace(jobTitle))
        {
            jobTitle = jobTitle.Trim();
            if (jobTitle.Length > IdCardConsoleComponent.MaxJobTitleLength)
                jobTitle = jobTitle[..IdCardConsoleComponent.MaxJobTitleLength];
        }

        // Update recipient
        recipient.Name = name;
        recipient.JobTitle = jobTitle;

        _nanoChat.SetRecipient((card, card.Comp), msg.RecipientNumber.Value, recipient);

        var recipientEv = new NanoChatRecipientUpdatedEvent(card);
        RaiseLocalEvent(ref recipientEv);
        UpdateUIForCard(card);
    }

    /// <summary>
    ///     Handles closing the current chat conversation.
    /// </summary>
    private void HandleCloseChat(Entity<NanoChatCardComponent> card)
    {
        _nanoChat.SetCurrentChat((card, card.Comp), null);
    }

    /// <summary>
    ///     Handles deletion of a chat conversation.
    /// </summary>
    private void HandleDeleteChat(Entity<NanoChatCardComponent> card, NanoChatUiMessageEvent msg)
    {
        if (msg.RecipientNumber == null || card.Comp.Number == null)
            return;

        // Funky Station Start - Group Chat Handling
        var chatNumber = msg.RecipientNumber.Value;
        var recipient = _nanoChat.GetRecipient((card, card.Comp), chatNumber);

        // If it's a group chat, remove this user from the group
        if (recipient != null && recipient.Value.IsGroup)
        {
            var members = recipient.Value.Members ?? new HashSet<uint>();
            if (members.Remove(card.Comp.Number.Value))
            {
                var admins = recipient.Value.Admins ?? new HashSet<uint>();
                admins.Remove(card.Comp.Number.Value);

                // If the creator is leaving, transfer ownership
                uint? newCreatorId = recipient.Value.CreatorId;
                var isCreatorLeaving = card.Comp.Number.Value == recipient.Value.CreatorId;
                if (isCreatorLeaving)
                {
                    if (admins.Count > 0)
                    {
                        newCreatorId = admins.First();
                    }
                    else if (members.Count > 0)
                    {
                        newCreatorId = members.First();
                    }
                    else
                    {
                        newCreatorId = null;
                    }
                }

                // Update the group for all remaining members
                var updatedRecipient = newCreatorId != null
                    ? recipient.Value with { Members = members, Admins = admins, CreatorId = newCreatorId }
                    : recipient.Value with { Members = members, Admins = admins };

                UpdateGroupRecipientForMembers(chatNumber, updatedRecipient, members);
            }
        }
        // Funky Station End - Group Chat Handling

        // Delete chat but keep the messages
        var deleted = _nanoChat.TryDeleteChat((card, card.Comp), chatNumber, true); // Funky Station - Stored chatNumber earlier so we don't have to get it multiple times.

        if (!deleted)
            return;

        _adminLogger.Add(LogType.Action,
            LogImpact.Low,
            $"{ToPrettyString(msg.Actor):user} deleted NanoChat conversation with #{chatNumber:D4}"); // Funky Station - Used stored chatNumber.

        UpdateUIForCard(card);
    }

    /// <summary>
    ///     Handles toggling notification mute state.
    /// </summary>
    private void HandleToggleMute(Entity<NanoChatCardComponent> card)
    {
        _nanoChat.SetNotificationsMuted((card, card.Comp), !_nanoChat.GetNotificationsMuted((card, card.Comp)));
        UpdateUIForCard(card);
    }

    private void HandleToggleMuteChat(Entity<NanoChatCardComponent> card, NanoChatUiMessageEvent msg)
    {
        if (msg.RecipientNumber is not uint chat)
            return;
        _nanoChat.ToggleChatMuted((card, card.Comp), chat);
        UpdateUIForCard(card);
    }

    private void HandleToggleListNumber(Entity<NanoChatCardComponent> card)
    {
        _nanoChat.SetListNumber((card, card.Comp), !_nanoChat.GetListNumber((card, card.Comp)));
        TryUpdateListedCache(card.Owner, card.Comp);
        UpdateUIForAllCards();
    }

    /// <summary>
    ///     Handles sending a new message in a chat conversation.
    /// </summary>
    private void HandleSendMessage(Entity<NanoChatCartridgeComponent> cartridge,
        Entity<NanoChatCardComponent> card,
        NanoChatUiMessageEvent msg)
    {
        if (msg.RecipientNumber == null || msg.Content == null || card.Comp.Number == null)
            return;

        // Funky Station Begin - Group Chats (Check if this is a group chat before trying to ensure recipient exists)
        var recipient = _nanoChat.GetRecipient((card, card.Comp), msg.RecipientNumber.Value);
        var isGroupChat = recipient?.IsGroup ?? false;

        // Only ensure recipient exists for non-group chats
        if (!isGroupChat && !EnsureRecipientExists(card, msg.RecipientNumber.Value))
            return;
        // Funky Station End - Group Chats

        var content = msg.Content;
        if (!string.IsNullOrWhiteSpace(content))
        {
            content = content.Trim();
            if (content.Length > NanoChatMessage.MaxContentLength)
                content = content[..NanoChatMessage.MaxContentLength];
        }


        // Create and store message for sender
        var message = new NanoChatMessage(
            _timing.CurTime,
            content,
            (uint)card.Comp.Number
        );

        // Funky Station Start - Group Chat Handling
        List<Entity<NanoChatCardComponent>> recipients;
        bool deliveryFailed;

        if (isGroupChat && recipient != null)
        {
            // For group chats, deliver to all members
            (deliveryFailed, recipients) = AttemptGroupMessageDelivery(cartridge, recipient.Value, card.Comp.Number.Value);
        }
        else
        {
            // For regular chats, deliver to single recipient
            (deliveryFailed, recipients) = AttemptMessageDelivery(cartridge, msg.RecipientNumber.Value);
        }
        // Funky Station End - Group Chat Handling

        // Update delivery status
        message = message with { DeliveryFailed = deliveryFailed };

        // Store message in sender's outbox under recipient's number
        _nanoChat.AddMessage((card, card.Comp), msg.RecipientNumber.Value, message);

        // Log message attempt
        var recipientsText = recipients.Count > 0
            ? string.Join(", ", recipients.Select(r => ToPrettyString(r)))
            : $"#{msg.RecipientNumber:D4}";

        _adminLogger.Add(LogType.Chat,
            LogImpact.Low,
            $"{ToPrettyString(card):user} sent NanoChat message to {recipientsText}: {content}{(deliveryFailed ? " [DELIVERY FAILED]" : "")}");

        var msgEv = new NanoChatMessageReceivedEvent(card);
        RaiseLocalEvent(ref msgEv);

        if (deliveryFailed)
            return;

        foreach (var recipientCard in recipients)
        {
            DeliverMessageToRecipient(card, recipientCard, message, msg.RecipientNumber.Value, recipient); // Funky Station - Modified for Group Chats.
        }
    }

    /// <summary>
    ///     Ensures a recipient exists in the sender's contacts.
    /// </summary>
    /// <param name="card">The card to check contacts for</param>
    /// <param name="recipientNumber">The recipient's number to check</param>
    /// <returns>True if the recipient exists or was created successfully</returns>
    private bool EnsureRecipientExists(Entity<NanoChatCardComponent> card, uint recipientNumber)
    {
        return _nanoChat.EnsureRecipientExists((card, card.Comp), recipientNumber, GetCardInfo(recipientNumber));
    }

    /// <summary>
    ///     Attempts to deliver a message to recipients.
    /// </summary>
    /// <param name="sender">The sending cartridge entity</param>
    /// <param name="recipientNumber">The recipient's number</param>
    /// <returns>Tuple containing delivery status and recipients if found.</returns>
    private (bool failed, List<Entity<NanoChatCardComponent>> recipient) AttemptMessageDelivery(
        Entity<NanoChatCartridgeComponent> sender,
        uint recipientNumber)
    {
        EnsureCardNumberCachePopulated();

        var channel = _prototype.Index(sender.Comp.RadioChannel);
        var radioSource = GetRadioSource(sender);
        var sendAttemptEvent = new RadioSendAttemptEvent(channel, radioSource);
        RaiseLocalEvent(ref sendAttemptEvent);
        if (sendAttemptEvent.Cancelled)
            return (true, new List<Entity<NanoChatCardComponent>>());

        var foundRecipients = new List<Entity<NanoChatCardComponent>>();
        if (_cardsByNumber.TryGetValue(recipientNumber, out var recipientCardUids))
        {
            foreach (var cardUid in recipientCardUids)
            {
                if (!TryComp<NanoChatCardComponent>(cardUid, out var card))
                    continue;

                foundRecipients.Add((cardUid, card));
            }
        }
        else
        {
            _sawmill.Warning($"[cache-miss] _cardsByNumber miss for #{recipientNumber:D4} — falling back to live query");
            var fallbackQuery = EntityQueryEnumerator<NanoChatCardComponent>();
            while (fallbackQuery.MoveNext(out var fallbackUid, out var fallbackCard))
            {
                if (fallbackCard.Number != recipientNumber)
                    continue;

                foundRecipients.Add((fallbackUid, fallbackCard));
            }
        }

        if (foundRecipients.Count == 0)
            return (true, foundRecipients);

        var deliverableRecipients = new List<Entity<NanoChatCardComponent>>();
        foreach (var recipient in foundRecipients)
        {
            if (recipient.Comp.PdaUid is not { } recipientPda)
            continue;
            var receiveAttemptEv = new RadioReceiveAttemptEvent(channel, radioSource, recipientPda);
            RaiseLocalEvent(ref receiveAttemptEv);
            if (receiveAttemptEv.Cancelled)
                continue;

            deliverableRecipients.Add(recipient);
            break;
        }

        return (deliverableRecipients.Count == 0, deliverableRecipients);
    }

    // Funky Station Start - Heavily modified to support group chats

    /// <summary>
    ///     Delivers a message to the recipient and handles associated notifications.
    /// </summary>
    /// <param name="sender">The sender's card entity</param>
    /// <param name="recipient">The recipient's card entity</param>
    /// <param name="message">The <see cref="NanoChatMessage" /> to deliver</param>
    /// <param name="chatNumber">The chat number (for group chats, this is the group number)</param>
    /// <param name="groupRecipient">Optional group recipient info if this is a group chat</param>
    private void DeliverMessageToRecipient(Entity<NanoChatCardComponent> sender,
        Entity<NanoChatCardComponent> recipient,
        NanoChatMessage message,
        uint chatNumber,
        NanoChatRecipient? groupRecipient = null)
    {
        if (sender.Comp.Number is not uint senderNumber)
            return;

        // For group chats, use the group number. For regular chats, use the sender's number
        var recipientNumber = (groupRecipient != null && groupRecipient.Value.IsGroup) ? chatNumber : senderNumber;

        if (groupRecipient != null && groupRecipient.Value.IsGroup)
        {
            var existingRecipient = _nanoChat.GetRecipient((recipient, recipient.Comp), recipientNumber);

            if (existingRecipient == null || !existingRecipient.Value.IsGroup)
            {
                _nanoChat.SetRecipient((recipient, recipient.Comp), recipientNumber, groupRecipient.Value);
            }
            else if (groupRecipient.Value.Members != null)
            {
                var existingMembers = existingRecipient.Value.Members ?? new HashSet<uint>();
                if (!existingMembers.SetEquals(groupRecipient.Value.Members))
                {
                    _nanoChat.SetRecipient((recipient, recipient.Comp), recipientNumber, groupRecipient.Value);
                }
                else
                {
                    var existingAdmins = existingRecipient.Value.Admins ?? new HashSet<uint>();
                    var groupAdmins = groupRecipient.Value.Admins ?? new HashSet<uint>();
                    if (!existingAdmins.SetEquals(groupAdmins))
                    {
                        _nanoChat.SetRecipient((recipient, recipient.Comp), recipientNumber, groupRecipient.Value);
                    }
                }
            }
        }
        else if (!EnsureRecipientExists(recipient, recipientNumber))
        {
            return;
        }

        _nanoChat.AddMessage((recipient, recipient.Comp), recipientNumber, message with { DeliveryFailed = false });

        if (recipient.Comp.IsClosed || _nanoChat.GetCurrentChat((recipient, recipient.Comp)) != recipientNumber)
            HandleUnreadNotification(recipient, message, recipientNumber);

        var msgEv = new NanoChatMessageReceivedEvent(recipient);
        RaiseLocalEvent(ref msgEv);
        UpdateUIForCard(recipient);
    }
    // Funky Station End - Heavily modified to support group chats


    /// <summary>
    ///     Attempts to deliver a message to all members of a group chat.
    /// </summary>
    private (bool failed, List<Entity<NanoChatCardComponent>> recipients) AttemptGroupMessageDelivery(
        Entity<NanoChatCartridgeComponent> sender,
        NanoChatRecipient groupRecipient,
        uint senderNumber) // Funky Station - Group Chats
    {
        if (groupRecipient.Members == null)
            return (true, new List<Entity<NanoChatCardComponent>>());

        var deliverableRecipients = new List<Entity<NanoChatCardComponent>>();

        foreach (var memberNumber in groupRecipient.Members)
        {
            if (memberNumber == senderNumber)
                continue;

            var (failed, memberCards) = AttemptMessageDelivery(sender, memberNumber);
            if (!failed)
                deliverableRecipients.AddRange(memberCards);
        }

        return (deliverableRecipients.Count == 0, deliverableRecipients);
    }

    /// <summary>
    ///     Handles unread message notifications and updates unread status.
    /// </summary>
    private void HandleUnreadNotification(Entity<NanoChatCardComponent> recipient,
        NanoChatMessage message,
        uint senderNumber)
    {
        // Get sender name from contacts or fall back to number
        var recipients = _nanoChat.GetRecipients((recipient, recipient.Comp));
        var senderName = recipients.TryGetValue(senderNumber, out var senderRecipient)
            ? senderRecipient.Name
            : $"#{senderNumber:D4}"; // Funky Station - senderNumber is used now in order to support group chats.
        var hasSelectedCurrentChat = _nanoChat.GetCurrentChat((recipient, recipient.Comp)) == senderNumber;

        // Update unread status
        if (!hasSelectedCurrentChat)
            _nanoChat.SetRecipient((recipient, recipient.Comp),
                senderNumber, // Funky Station - senderNumber is used now in order to support group chats.
                senderRecipient with { HasUnread = true });

        // Temporary local to avoid trouble with read-only access; Contains doesn't modify the collection
        HashSet<uint> mutedChats = recipient.Comp.MutedChats;
        if (recipient.Comp.NotificationsMuted ||
            mutedChats.Contains(senderNumber) || // Funky Station - senderNumber is used now in order to support group chats.
            recipient.Comp.PdaUid is not { } pdaUid ||
            !TryComp<CartridgeLoaderComponent>(pdaUid, out var loader) ||
            // Don't notify if the recipient has the NanoChat program open with this chat selected.
            (hasSelectedCurrentChat &&
                _ui.IsUiOpen(pdaUid, PdaUiKey.Key) &&
                HasComp<NanoChatCartridgeComponent>(loader.ActiveProgram)))
            return;

        var title = "";
        if (!string.IsNullOrEmpty(senderRecipient.JobTitle))
        {
            var titleRecipient = SharedNanoChatSystem.Truncate(Loc.GetString("nano-chat-new-message-title-recipient",
                ("sender", senderName), ("jobTitle", senderRecipient.JobTitle)), NotificationTitleMaxLength, " \\[...\\]");
            title = Loc.GetString("nano-chat-new-message-title", ("sender", titleRecipient));
        }
        else
            title = Loc.GetString("nano-chat-new-message-title", ("sender", senderName));

        _cartridge.SendNotification(pdaUid,
            title,
            Loc.GetString("nano-chat-new-message-body", ("message", SharedNanoChatSystem.Truncate(message.Content, NotificationMaxLength, " [...]"))),
            loader);
    }

    /// <summary>
    ///     Updates the UI for any PDAs containing the specified card.
    /// </summary>
    private void UpdateUIForCard(EntityUid cardUid)
    {
        EnsureCartridgeTrackingCachePopulated();

        if (!_cartridgesByCard.TryGetValue(cardUid, out var cartridgeUids))
        {
            // Cache miss — fall back to live query (same timing issue as _cardsByNumber).
            _sawmill.Warning($"[cache-miss] _cartridgesByCard miss for card={ToPrettyString(cardUid)} — falling back to live query");
            var fallback = EntityQueryEnumerator<NanoChatCartridgeComponent, CartridgeComponent>();
            while (fallback.MoveNext(out var fUid, out var fComp, out var fCartridge))
            {
                if (fComp.Card != cardUid || fCartridge.LoaderUid == null)
                    continue;

                UpdateUI((fUid, fComp), fCartridge.LoaderUid.Value);
            }
            return;
        }

        foreach (var uid in cartridgeUids)
        {
            if (!TryComp<NanoChatCartridgeComponent>(uid, out var comp) ||
                !TryComp<CartridgeComponent>(uid, out var cartridge) ||
                cartridge.LoaderUid == null)
                continue;

            UpdateUI((uid, comp), cartridge.LoaderUid.Value);
        }
    }

    /// <summary>
    ///     Updates the UI for all PDAs containing a NanoChat cartridge.
    /// </summary>
    private void UpdateUIForAllCards()
    {
        // Find any PDA containing this card and update its UI
        var query = EntityQueryEnumerator<NanoChatCartridgeComponent, CartridgeComponent>();
        while (query.MoveNext(out var uid, out var comp, out var cartridge))
        {
            if (cartridge.LoaderUid is { } loader)
                UpdateUI((uid, comp), loader);
        }
    }

    /// <summary>
    ///     Gets the <see cref="NanoChatRecipient" /> for a given NanoChat number.
    /// </summary>
    private NanoChatRecipient? GetCardInfo(uint number)
    {
        EnsureCardNumberCachePopulated();

        if (!_cardsByNumber.TryGetValue(number, out var cardUids))
        {
            // Cache miss — fall back to live query (same as AttemptMessageDelivery).
            _sawmill.Warning($"[cache-miss] _cardsByNumber miss in GetCardInfo for #{number:D4} — falling back to live query");
            cardUids = [];
            var fallback = EntityQueryEnumerator<NanoChatCardComponent>();
            while (fallback.MoveNext(out var fUid, out var fCard))
            {
                if (fCard.Number == number)
                    cardUids.Add(fUid);
            }

            if (cardUids.Count == 0)
                return null;
        }

        foreach (var uid in cardUids)
        {
            if (!TryComp<NanoChatCardComponent>(uid, out _))
                continue;

            // Try to get job title from ID card if possible
            string? jobTitle = null;
            var name = "Unknown";
            if (TryComp<IdCardComponent>(uid, out var idCard))
            {
                jobTitle = idCard.LocalizedJobTitle;
                name = idCard.FullName ?? name;
            }

            return new NanoChatRecipient(number, name, jobTitle);
        }

        return null;
    }

    /// <summary>
    ///     Gets the most representative entity to use as a radio source for this cartridge.
    /// </summary>
    private EntityUid GetRadioSource(Entity<NanoChatCartridgeComponent> cartridge)
    {
        if (cartridge.Comp.Card is { } cardUid &&
            TryComp<NanoChatCardComponent>(cardUid, out var card) &&
            card.PdaUid is { } pdaUid)
        {
            return pdaUid;
        }

        return cartridge.Owner;
    }

    private void OnUiReady(Entity<NanoChatCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        _cartridge.RegisterBackgroundProgram(args.Loader, ent);
        UpdateUI(ent, args.Loader);
    }

    private void UpdateUI(Entity<NanoChatCartridgeComponent> ent, EntityUid loader)
    {
        if (_station.GetOwningStation(loader) is { } station)
            ent.Comp.Station = station;

        if (!_listedCardInfoCacheInitialized)
            RebuildListedCache();

        var contacts = new List<NanoChatRecipient>();

        foreach (var (entityId, (nanoChatNumber, fullName)) in _listedCardInfo)
        {
            if (entityId != ent.Comp.Card)
                contacts.Add(new NanoChatRecipient(nanoChatNumber, fullName));
        }

        contacts.Sort((contactA, contactB) => string.CompareOrdinal(contactA.Name, contactB.Name));

        var recipients = new Dictionary<uint, NanoChatRecipient>();
        var messages = new Dictionary<uint, List<NanoChatMessage>>();
        var mutedChats = new HashSet<uint>();
        uint? currentChat = null;
        uint ownNumber = 0;
        var maxRecipients = 50;
        var notificationsMuted = false;
        var listNumber = false;

        if (ent.Comp.Card != null && TryComp<NanoChatCardComponent>(ent.Comp.Card, out var card))
        {
            recipients = card.Recipients;
            messages = card.Messages;
            mutedChats = card.MutedChats;
            currentChat = card.CurrentChat;
            ownNumber = card.Number ?? 0;
            maxRecipients = card.MaxRecipients;
            notificationsMuted = card.NotificationsMuted;
            listNumber = card.ListNumber;
        }

        var state = new NanoChatUiState(recipients,
            messages,
            mutedChats,
            contacts,
            currentChat,
            ownNumber,
            maxRecipients,
            notificationsMuted,
            listNumber);
        _cartridge.UpdateCartridgeUiState(loader, state);
    }

    /// <summary>
    ///     Handles creation of a new group chat.
    /// </summary>
    private void HandleCreateGroupChat(Entity<NanoChatCardComponent> card, NanoChatUiMessageEvent msg) // Funky Station - Group Chats
    {
        if (msg.Content == null || card.Comp.Number == null)
            return;

        var name = msg.Content;
        if (!string.IsNullOrWhiteSpace(name))
        {
            name = name.Trim();
            if (name.Length > IdCardConsoleComponent.MaxFullNameLength)
                name = name[..IdCardConsoleComponent.MaxFullNameLength];
        }

        // Generate a unique group number (surely unique I actually have no idea how to generate good unique numbers.)
        var groupNumber = (uint) (HashCode.Combine(card.Comp.Number.Value, _timing.CurTime.Ticks) & 0x7FFFFFFF);

        // This fucking sucks (fire emoji)
        while (_nanoChat.GetRecipient((card, card.Comp), groupNumber) != null)
        {
            groupNumber++;
        }

        // Create group chat recipient
        var members = new HashSet<uint> { card.Comp.Number.Value };
        var recipient = new NanoChatRecipient(
            groupNumber,
            name,
            null,
            false,
            true,
            members,
            card.Comp.Number.Value
        );

        _nanoChat.SetRecipient((card, card.Comp), groupNumber, recipient);

        _adminLogger.Add(LogType.Action,
            LogImpact.Low,
            $"{ToPrettyString(msg.Actor):user} created group chat '{name}' (#{groupNumber:D4})");

        var recipientEv = new NanoChatRecipientUpdatedEvent(card);
        RaiseLocalEvent(ref recipientEv);
        UpdateUIForCard(card);
    }

    /// <summary>
    ///     Handles inviting a user to a group chat.
    /// </summary>
    private void HandleInviteToGroup(Entity<NanoChatCardComponent> card, NanoChatUiMessageEvent msg) // Funky Station - Group Chats
    {
        if (msg.RecipientNumber == null || msg.Content == null || card.Comp.Number == null)
            return;

        var groupNumber = msg.RecipientNumber.Value;
        if (!uint.TryParse(msg.Content, out var inviteeNumber))
            return;

        var recipient = _nanoChat.GetRecipient((card, card.Comp), groupNumber);
        if (recipient == null || !recipient.Value.IsGroup)
            return;

        // Only the creator or admins can invite
        var isCreator = recipient.Value.CreatorId == card.Comp.Number.Value;
        var admins = recipient.Value.Admins ?? new HashSet<uint>();
        var isAdmin = admins.Contains(card.Comp.Number.Value);

        if (!isCreator && !isAdmin)
            return;

        var members = recipient.Value.Members ?? new HashSet<uint>();
        if (members.Contains(inviteeNumber))
            return;

        // Add member to group
        members.Add(inviteeNumber);
        var updatedRecipient = recipient.Value with { Members = members };
        _nanoChat.SetRecipient((card, card.Comp), groupNumber, updatedRecipient);

        UpdateGroupRecipientForMembers(groupNumber, updatedRecipient, members);

        _adminLogger.Add(LogType.Action,
            LogImpact.Low,
            $"{ToPrettyString(msg.Actor):user} invited #{inviteeNumber:D4} to group chat #{groupNumber:D4}");

        var recipientEv = new NanoChatRecipientUpdatedEvent(card);
        RaiseLocalEvent(ref recipientEv);
        UpdateUIForCard(card);
    }

    /// <summary>
    ///     Handles kicking a user from a group chat.
    /// </summary>
    private void HandleKickFromGroup(Entity<NanoChatCardComponent> card, NanoChatUiMessageEvent msg) // Funky Station - Group Chats
    {
        if (msg.RecipientNumber == null || msg.Content == null || card.Comp.Number == null)
            return;

        var groupNumber = msg.RecipientNumber.Value;
        if (!uint.TryParse(msg.Content, out var kickeeNumber))
            return;

        var recipient = _nanoChat.GetRecipient((card, card.Comp), groupNumber);
        if (recipient == null || !recipient.Value.IsGroup)
            return;

        var isCreator = recipient.Value.CreatorId == card.Comp.Number.Value;
        var admins = recipient.Value.Admins ?? new HashSet<uint>();
        var isAdmin = admins.Contains(card.Comp.Number.Value);
        var isSelfKick = kickeeNumber == card.Comp.Number.Value;

        if (!isSelfKick && !isCreator && !isAdmin)
            return;

        // Allow creator to leave the group but not be kicked by other members.
        var isCreatorLeaving = kickeeNumber == recipient.Value.CreatorId && isCreator;
        if (kickeeNumber == recipient.Value.CreatorId && !isCreatorLeaving)
            return;

        if (_cardsByNumber.TryGetValue(kickeeNumber, out var kickeeCardUids))
        {
            foreach (var kickeeCardUid in kickeeCardUids)
            {
                if (!TryComp<NanoChatCardComponent>(kickeeCardUid, out var kickeeCard))
                    continue;

                var deleteMsg = new NanoChatUiMessageEvent(NanoChatUiMessageType.DeleteChat, groupNumber, null, null)
                {
                    Actor = msg.Actor
                };
                HandleDeleteChat((kickeeCardUid, kickeeCard), deleteMsg);
            }
        }

        if (isCreatorLeaving)
        {
            _adminLogger.Add(LogType.Action,
                LogImpact.Low,
                $"{ToPrettyString(msg.Actor):user} left group chat #{groupNumber:D4}");
        }
        else
        {
            _adminLogger.Add(LogType.Action,
                LogImpact.Low,
                $"{ToPrettyString(msg.Actor):user} kicked #{kickeeNumber:D4} from group chat #{groupNumber:D4}");
        }

        var recipientEv = new NanoChatRecipientUpdatedEvent(card);
        RaiseLocalEvent(ref recipientEv);
        UpdateUIForCard(card);
    }

    /// <summary>
    ///     Handles promoting a user to admin in a group chat.
    /// </summary>
    private void HandleAdminUser(Entity<NanoChatCardComponent> card, NanoChatUiMessageEvent msg) // Funky Station - Group Chats
    {
        if (msg.RecipientNumber == null || msg.Content == null || card.Comp.Number == null)
            return;

        var groupNumber = msg.RecipientNumber.Value;
        if (!uint.TryParse(msg.Content, out var targetNumber))
            return;

        var recipient = _nanoChat.GetRecipient((card, card.Comp), groupNumber);
        if (recipient == null || !recipient.Value.IsGroup)
            return;

        // Only the creator or admins can make admins
        var isCreator = recipient.Value.CreatorId == card.Comp.Number.Value;
        var admins = recipient.Value.Admins ?? new HashSet<uint>();
        var isAdmin = admins.Contains(card.Comp.Number.Value);

        if (!isCreator && !isAdmin)
            return;

        var members = recipient.Value.Members ?? new HashSet<uint>();
        if (!members.Contains(targetNumber))
            return; // how tf

        if (targetNumber == recipient.Value.CreatorId)
            return;

        if (!isCreator && admins.Contains(targetNumber))
            return;

        if (!admins.Add(targetNumber))
            return;

        var updatedRecipient = recipient.Value with { Admins = admins };
        _nanoChat.SetRecipient((card, card.Comp), groupNumber, updatedRecipient);

        // Sync to all members
        UpdateGroupRecipientForMembers(groupNumber, updatedRecipient, members);

        _adminLogger.Add(LogType.Action,
            LogImpact.Low,
            $"{ToPrettyString(msg.Actor):user} promoted #{targetNumber:D4} to admin in group chat #{groupNumber:D4}");

        var recipientEv = new NanoChatRecipientUpdatedEvent(card);
        RaiseLocalEvent(ref recipientEv);
        UpdateUIForCard(card);
    }

    /// <summary>
    ///     Handles demoting a user from admin in a group chat.
    /// </summary>
    private void HandleDeadminUser(Entity<NanoChatCardComponent> card, NanoChatUiMessageEvent msg) // Funky Station - Group Chats
    {
        if (msg.RecipientNumber == null || msg.Content == null || card.Comp.Number == null)
            return;

        var groupNumber = msg.RecipientNumber.Value;
        if (!uint.TryParse(msg.Content, out var targetNumber))
            return;

        var recipient = _nanoChat.GetRecipient((card, card.Comp), groupNumber);
        if (recipient == null || !recipient.Value.IsGroup)
            return;

        // Only the creator or admins can remove admins
        var isCreator = recipient.Value.CreatorId == card.Comp.Number.Value;
        var admins = recipient.Value.Admins ?? new HashSet<uint>();
        var isAdmin = admins.Contains(card.Comp.Number.Value);

        if (!isCreator && !isAdmin)
            return;

        var members = recipient.Value.Members ?? new HashSet<uint>();
        if (!members.Contains(targetNumber))
            return; // how

        if (!isCreator && (targetNumber == recipient.Value.CreatorId || admins.Contains(targetNumber)))
            return;

        if (!admins.Remove(targetNumber))
            return;

        var updatedRecipient = recipient.Value with { Admins = admins };
        _nanoChat.SetRecipient((card, card.Comp), groupNumber, updatedRecipient);

        // Sync to all members
        UpdateGroupRecipientForMembers(groupNumber, updatedRecipient, members);

        _adminLogger.Add(LogType.Action,
            LogImpact.Low,
            $"{ToPrettyString(msg.Actor):user} removed admin from #{targetNumber:D4} in group chat #{groupNumber:D4}");

        var recipientEv = new NanoChatRecipientUpdatedEvent(card);
        RaiseLocalEvent(ref recipientEv);
        UpdateUIForCard(card);
    }
}

