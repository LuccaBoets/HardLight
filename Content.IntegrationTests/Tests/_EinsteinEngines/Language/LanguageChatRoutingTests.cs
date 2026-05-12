#nullable enable

using System.Linq;
using Content.Client.UserInterface.Systems.Chat;
using Content.IntegrationTests.Pair;
using Content.Server._EinsteinEngines.Language;
using Content.Server.Chat.Systems;
using Content.Shared._EinsteinEngines.Language;
using Content.Shared.Chat;
using Content.Shared.Speech.Muting;
using Robust.Client.UserInterface;
using Robust.Server.Player;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._EinsteinEngines.Language;

[TestFixture]
public sealed class LanguageChatRoutingTests
{
        [TestPrototypes]
        private const string Prototypes = @"
- type: language
  id: EmpathicTestLanguage
  speech:
    empathySpeech: true
";

    [Test]
    public async Task SpeakAndWhisperRouteByLanguageComprehension()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = true,
        });

        var server = pair.Server;
        var client = pair.Client;
        var testMap = await pair.CreateTestMap();
        await server.WaitIdleAsync();

        var serverEnt = server.ResolveDependency<IEntityManager>();
        var players = server.ResolveDependency<IPlayerManager>();
        var chat = serverEnt.System<ChatSystem>();
        var language = serverEnt.System<LanguageSystem>();

        EntityUid listener = default;

        EntityUid speaker = default;
        NetEntity speakerNet = default;
        await server.WaitAssertion(() =>
        {
            listener = serverEnt.SpawnEntity("MobHuman", testMap.GridCoords);
            players.SetAttachedEntity(players.Sessions.Single(), listener);

            speaker = serverEnt.SpawnEntity("MobHuman", testMap.GridCoords);
            speakerNet = serverEnt.GetNetEntity(speaker);

            ConfigureBaseLanguages(language, serverEnt, speaker);
            ConfigureBaseLanguages(language, serverEnt, listener);

            language.AddLanguage(speaker, "Sign", addSpoken: true, addUnderstood: true);
            language.SetLanguage(speaker, "Sign");
        });

        await pair.RunTicksSync(5);

        var ui = client.ResolveDependency<IUserInterfaceManager>();
        var chatUi = ui.GetUIController<ChatUIController>();

        // Speak path: listener does not understand Sign yet, so should receive obfuscation.
        var baseline = await GetHistoryCount(pair, chatUi);
        await server.WaitPost(() => chat.TrySendInGameICMessage(speaker, "ZORBLAX_UNDERSTAND_TEST", InGameICChatType.Speak, hideChat: false, checkRadioPrefix: false));
        await pair.RunTicksSync(5);
        var unknownSpeak = await GetNewestMessageForSender(pair, chatUi, baseline, ChatChannel.Local, speakerNet);
        Assert.That(unknownSpeak, Is.Not.Null);
        Assert.That(unknownSpeak!.Message.Contains("ZORBLAX_UNDERSTAND_TEST", StringComparison.OrdinalIgnoreCase), Is.False);

        // Speak path: once listener understands Sign, they should receive clear text.
        await server.WaitAssertion(() =>
        {
            language.AddLanguage(listener, "Sign", addSpoken: false, addUnderstood: true);
        });
        await pair.RunTicksSync(2);

        baseline = await GetHistoryCount(pair, chatUi);
        await server.WaitPost(() => chat.TrySendInGameICMessage(speaker, "ZORBLAX_CLEAR_TEST", InGameICChatType.Speak, hideChat: false, checkRadioPrefix: false));
        await pair.RunTicksSync(5);
        var clearSpeak = await GetNewestMessageForSender(pair, chatUi, baseline, ChatChannel.Local, speakerNet);
        Assert.That(clearSpeak, Is.Not.Null);
        Assert.That(clearSpeak!.Message.Contains("ZORBLAX_CLEAR_TEST", StringComparison.OrdinalIgnoreCase), Is.True);

        // Whisper path: reset listener back to no Sign understanding and verify obfuscation.
        await server.WaitAssertion(() =>
        {
            ConfigureBaseLanguages(language, serverEnt, listener);
        });
        await pair.RunTicksSync(2);

        baseline = await GetHistoryCount(pair, chatUi);
        await server.WaitPost(() => chat.TrySendInGameICMessage(speaker, "WHISPER_OBFUSCATED_TEST", InGameICChatType.Whisper, hideChat: false, checkRadioPrefix: false));
        await pair.RunTicksSync(5);
        var unknownWhisper = await GetNewestMessageForSender(pair, chatUi, baseline, ChatChannel.Whisper, speakerNet);
        Assert.That(unknownWhisper, Is.Not.Null);
        Assert.That(unknownWhisper!.Message.Contains("WHISPER_OBFUSCATED_TEST", StringComparison.OrdinalIgnoreCase), Is.False);

        // Whisper path: add understanding and verify clear whisper content.
        await server.WaitAssertion(() =>
        {
            language.AddLanguage(listener, "Sign", addSpoken: false, addUnderstood: true);
        });
        await pair.RunTicksSync(2);

        baseline = await GetHistoryCount(pair, chatUi);
        await server.WaitPost(() => chat.TrySendInGameICMessage(speaker, "WHISPER_CLEAR_TEST", InGameICChatType.Whisper, hideChat: false, checkRadioPrefix: false));
        await pair.RunTicksSync(5);
        var clearWhisper = await GetNewestMessageForSender(pair, chatUi, baseline, ChatChannel.Whisper, speakerNet);
        Assert.That(clearWhisper, Is.Not.Null);
        Assert.That(clearWhisper!.Message.Contains("WHISPER_CLEAR_TEST", StringComparison.OrdinalIgnoreCase), Is.True);

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RequireSpeechAndAllowRadioBehavior()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = true,
        });

        var server = pair.Server;
        var client = pair.Client;
        var testMap = await pair.CreateTestMap();
        await server.WaitIdleAsync();

        var serverEnt = server.ResolveDependency<IEntityManager>();
        var players = server.ResolveDependency<IPlayerManager>();
        var chat = serverEnt.System<ChatSystem>();
        var language = serverEnt.System<LanguageSystem>();

        EntityUid listener = default;

        EntityUid speaker = default;
        NetEntity speakerNet = default;
        await server.WaitAssertion(() =>
        {
            listener = serverEnt.SpawnEntity("MobHuman", testMap.GridCoords);
            players.SetAttachedEntity(players.Sessions.Single(), listener);

            speaker = serverEnt.SpawnEntity("MobHuman", testMap.GridCoords);
            speakerNet = serverEnt.GetNetEntity(speaker);

            ConfigureBaseLanguages(language, serverEnt, speaker);
            ConfigureBaseLanguages(language, serverEnt, listener);
            language.AddLanguage(listener, "Sign", addSpoken: false, addUnderstood: true);

            language.AddLanguage(speaker, "Sign", addSpoken: true, addUnderstood: true);
            language.SetLanguage(speaker, "Sign");
            serverEnt.EnsureComponent<MutedComponent>(speaker);
        });

        await pair.RunTicksSync(5);

        var ui = client.ResolveDependency<IUserInterfaceManager>();
        var chatUi = ui.GetUIController<ChatUIController>();

        // RequireSpeech=false (Sign) should still allow muted entities to communicate.
        var baseline = await GetHistoryCount(pair, chatUi);
        await server.WaitPost(() => chat.TrySendInGameICMessage(speaker, "MUTED_SIGN_OK", InGameICChatType.Speak, hideChat: false, checkRadioPrefix: false));
        await pair.RunTicksSync(5);
        var signMessage = await GetNewestMessageForSender(pair, chatUi, baseline, ChatChannel.Local, speakerNet);
        Assert.That(signMessage, Is.Not.Null);
        Assert.That(signMessage!.Message.Contains("MUTED_SIGN_OK", StringComparison.OrdinalIgnoreCase), Is.True);

        // RequireSpeech=true (TauCetiBasic) should block muted entities.
        await server.WaitAssertion(() => language.SetLanguage(speaker, "TauCetiBasic"));
        await pair.RunTicksSync(2);

        baseline = await GetHistoryCount(pair, chatUi);
        await server.WaitPost(() => chat.TrySendInGameICMessage(speaker, "MUTED_BASIC_BLOCKED", InGameICChatType.Speak, hideChat: false, checkRadioPrefix: false));
        await pair.RunTicksSync(5);
        var blocked = await GetNewestMessageForSender(pair, chatUi, baseline, ChatChannel.Local, speakerNet);
        Assert.That(blocked, Is.Null);

        // AllowRadio=false (Sign) should keep ;prefixed message in local chat instead of radio whisper routing.
        await server.WaitAssertion(() => language.SetLanguage(speaker, "Sign"));
        await pair.RunTicksSync(2);

        baseline = await GetHistoryCount(pair, chatUi);
        await server.WaitPost(() => chat.TrySendInGameICMessage(speaker, ";RADIO_SHOULD_STAY_LOCAL", InGameICChatType.Speak, hideChat: false));
        await pair.RunTicksSync(5);

        var local = await GetNewestMessageForSender(pair, chatUi, baseline, ChatChannel.Local, speakerNet);
        var whisper = await GetNewestMessageForSender(pair, chatUi, baseline, ChatChannel.Whisper, speakerNet);
        Assert.That(local, Is.Not.Null);
        Assert.That(local!.Message.Contains("RADIO_SHOULD_STAY_LOCAL", StringComparison.OrdinalIgnoreCase), Is.True);
        Assert.That(whisper, Is.Null);

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EmpathySpeechEquivalentUsesTelepathicChannelByLanguage()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = true,
        });

        var server = pair.Server;
        var client = pair.Client;
        var testMap = await pair.CreateTestMap();
        await server.WaitIdleAsync();

        var serverEnt = server.ResolveDependency<IEntityManager>();
        var players = server.ResolveDependency<IPlayerManager>();
        var chat = serverEnt.System<ChatSystem>();
        var language = serverEnt.System<LanguageSystem>();

        EntityUid listener = default;
        EntityUid speaker = default;
        NetEntity speakerNet = default;

        await server.WaitAssertion(() =>
        {
            listener = serverEnt.SpawnEntity("MobHuman", testMap.GridCoords);
            players.SetAttachedEntity(players.Sessions.Single(), listener);

            speaker = serverEnt.SpawnEntity("MobHuman", testMap.GridCoords);
            speakerNet = serverEnt.GetNetEntity(speaker);

            ConfigureBaseLanguages(language, serverEnt, speaker);
            ConfigureBaseLanguages(language, serverEnt, listener);

            language.AddLanguage(speaker, "EmpathicTestLanguage", addSpoken: true, addUnderstood: true);
            language.SetLanguage(speaker, "EmpathicTestLanguage");
        });

        await pair.RunTicksSync(5);

        var ui = client.ResolveDependency<IUserInterfaceManager>();
        var chatUi = ui.GetUIController<ChatUIController>();

        // Listener does not understand this empathy-enabled language yet: no telepathic relay should be received.
        var baseline = await GetHistoryCount(pair, chatUi);
        await server.WaitPost(() => chat.TrySendInGameICMessage(speaker, "EMPATHY_BLOCKED_FOR_UNKNOWN", InGameICChatType.Speak, hideChat: false, checkRadioPrefix: false));
        await pair.RunTicksSync(5);
        var unknownTelepathy = await GetNewestMessageForSender(pair, chatUi, baseline, ChatChannel.Telepathic, speakerNet);
        Assert.That(unknownTelepathy, Is.Null);

        // Once listener understands the language, telepathic relay should appear.
        await server.WaitAssertion(() => language.AddLanguage(listener, "EmpathicTestLanguage", addSpoken: false, addUnderstood: true));
        await pair.RunTicksSync(2);

        baseline = await GetHistoryCount(pair, chatUi);
        await server.WaitPost(() => chat.TrySendInGameICMessage(speaker, "EMPATHY_VISIBLE_FOR_KNOWN", InGameICChatType.Speak, hideChat: false, checkRadioPrefix: false));
        await pair.RunTicksSync(5);
        var knownTelepathy = await GetNewestMessageForSender(pair, chatUi, baseline, ChatChannel.Telepathic, speakerNet);
        Assert.That(knownTelepathy, Is.Not.Null);
        Assert.That(knownTelepathy!.Message.Contains("EMPATHY_VISIBLE_FOR_KNOWN", StringComparison.OrdinalIgnoreCase), Is.True);

        await pair.CleanReturnAsync();
    }

    private static void ConfigureBaseLanguages(LanguageSystem language, IEntityManager entMan, EntityUid uid)
    {
        var knowledge = entMan.EnsureComponent<LanguageKnowledgeComponent>(uid);
        knowledge.SpokenLanguages.Clear();
        knowledge.UnderstoodLanguages.Clear();
        knowledge.SpokenLanguages.Add("TauCetiBasic");
        knowledge.UnderstoodLanguages.Add("TauCetiBasic");
        language.UpdateEntityLanguages(uid);
        language.SetLanguage(uid, "TauCetiBasic");
    }

    private static async Task<int> GetHistoryCount(TestPair pair, ChatUIController chatUi)
    {
        var count = 0;
        await pair.Client.WaitAssertion(() => count = chatUi.History.Count);
        return count;
    }

    private static async Task<ChatMessage?> GetNewestMessageForSender(
        TestPair pair,
        ChatUIController chatUi,
        int startIndex,
        ChatChannel channel,
        NetEntity sender)
    {
        ChatMessage? message = null;
        await pair.Client.WaitAssertion(() =>
        {
            message = chatUi.History
                .Skip(startIndex)
                .Select(x => x.Msg)
                .LastOrDefault(x => x.Channel == channel && x.SenderEntity == sender);
        });

        return message;
    }
}