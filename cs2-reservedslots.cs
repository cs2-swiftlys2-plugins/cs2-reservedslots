using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.ProtobufDefinitions;
using SwiftlyS2.Shared.SchemaDefinitions;
using System.Collections.Concurrent;

namespace ReservedSlots;

[PluginMetadata(Id = "ReservedSlots", Version = "v2", Name = "ReservedSlots", Author = "schwarper")]
public sealed class ReservedSlots(ISwiftlyCore core) : BasePlugin(core)
{
    public IConVar<string> reserved_flag = null!;
    public IConVar<int> reserved_slots = null!;
    public IConVar<bool> hide_slots = null!;
    public IConVar<int> reserve_type = null!;
    public IConVar<int> reserve_maxadmins = null!;
    public IConVar<int> reserve_kicktype = null!;
    public IConVar<int> sv_visiblemaxplayers = null!;

    public ConcurrentDictionary<ulong, double> GlobalPlayerTime = [];
    public HashSet<ulong> AdminsSteamIds { get; set; } = [];

    private enum KickType
    {
        Kick_HighestPing = 0,
        Kick_HighestTime,
        Kick_Random,
    };

    public override void Load(bool hotReload)
    {
        reserved_flag = Core.ConVar.Create("reserved_slots_flag", "Permission of reserved slot", "reserved.slot");
        reserved_slots = Core.ConVar.Create("reserved_slots", "Number of reserved player slots", 0);
        hide_slots = Core.ConVar.Create("hide_slots", "If set to 1, reserved slots will be hidden", false);
        reserve_type = Core.ConVar.Create("reserve_type", "Method of reserving slots", 0);
        reserve_maxadmins = Core.ConVar.Create("reserve_maxadmins", "Max admins for type 2", 0);
        reserve_kicktype = Core.ConVar.Create("reserve_kicktype", "Kick method", 0);

        sv_visiblemaxplayers = Core.ConVar.Find<int>("sv_visiblemaxplayers")!;

        if (hotReload) 
            CheckHiddenSlots();
    }

    public override void Unload()
    {
        ResetVisibleMax();
        AdminsSteamIds.Clear();
        GlobalPlayerTime.Clear();
    }

    [EventListener<EventDelegates.OnMapLoad>]
    public void OnMapStart(IOnMapLoadEvent _) => CheckHiddenSlots();

    [EventListener<EventDelegates.OnClientConnected>]
    public void OnPlayerConnect(IOnClientConnectedEvent @event)
    {
        IPlayer? player = Core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player == null)
            return;

        GlobalPlayerTime.TryAdd(player.UnauthorizedSteamID, Core.Engine.GlobalVars.CurrentTime);

        int reservedCount = reserved_slots.Value;
        if (reservedCount <= 0)
            return;

        int currentClients = GetClientCount();
        int limit = Core.Engine.GlobalVars.MaxClients - reservedCount;
        int type = reserve_type.Value;
        bool hasPermission = Core.Permission.PlayerHasPermission(player.UnauthorizedSteamID, reserved_flag.Value);

        if (type == 2 && hasPermission)
            AdminsSteamIds.Add(player.UnauthorizedSteamID);

        if (currentClients <= limit)
        {
            if (hasPermission && hide_slots.Value)
                SetVisibleMaxSlots(currentClients, limit);

            return;
        }

        if (hasPermission)
        {
            switch (type)
            {
                case 0:
                    if (hide_slots.Value)
                        SetVisibleMaxSlots(currentClients, limit);
                    return;

                case 1:
                    ProcessKickLogic(player, ref @event);
                    break;

                case 2 when AdminsSteamIds.Count < reserve_maxadmins.Value || AdminsSteamIds.Contains(player.UnauthorizedSteamID):
                    ProcessKickLogic(player, ref @event);
                    break;
            }
        }

        RejectIncomingPlayer(ref @event);
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnClientDisconnect(EventPlayerConnectFull @event)
    {
        IPlayer? player = @event.UserIdPlayer;
        if (player == null) return HookResult.Continue;

        CheckHiddenSlots();
        AdminsSteamIds.Remove(player.SteamID);
        GlobalPlayerTime.TryRemove(player.SteamID, out _);

        return HookResult.Continue;
    }

    [EventListener<EventDelegates.OnConVarValueChanged>]
    public void OnConVarValueChanged(IOnConVarValueChanged @event)
    {
        if (@event.ConVarName == "reserved_slots")
        {
            if (int.TryParse(@event.NewValue, out int value))
            {
                if (value <= 0)
                    ResetVisibleMax();
                else if (hide_slots.Value)
                    SetVisibleMaxSlots(GetClientCount(), Core.Engine.GlobalVars.MaxClients - value);
            }
        }
        else if (@event.ConVarName == "hide_slots")
        {
            if (bool.TryParse(@event.NewValue, out bool value))
            {
                if (!value)
                    ResetVisibleMax();
                else
                    SetVisibleMaxSlots(GetClientCount(), Core.Engine.GlobalVars.MaxClients - (value ? 1 : 0));
            }
        }
    }

    private void ProcessKickLogic(IPlayer incomingPlayer, ref IOnClientConnectedEvent @event)
    {
        IPlayer? target = SelectKickClient(incomingPlayer);
        if (target != null)
        {
            target.Kick("Kicked for reserved slot.", ENetworkDisconnectionReason.NETWORK_DISCONNECT_REJECT_RESERVED_FOR_LOBBY);
            return;
        }

        @event.Result = HookResult.Stop;
    }

    public void RejectIncomingPlayer(ref IOnClientConnectedEvent @event)
    {
        @event.Result = HookResult.Stop;
        CheckHiddenSlots();
    }

    public void CheckHiddenSlots()
    {
        if (hide_slots.Value)
            SetVisibleMaxSlots(GetClientCount(), Core.Engine.GlobalVars.MaxClients - 1);
    }

    public void SetVisibleMaxSlots(int clients, int limit)
    {
        int maxClients = Core.Engine.GlobalVars.MaxClients;
        int num = (clients >= maxClients) ? maxClients : (clients < limit ? limit : clients);

        if (sv_visiblemaxplayers.Value != num)
            sv_visiblemaxplayers.Value = num;
    }

    public void ResetVisibleMax() => sv_visiblemaxplayers.Value = -1;

    public IPlayer? SelectKickClient(IPlayer incomingAdmin)
    {
        KickType type = (KickType)reserve_kicktype.Value;
        IPlayer? bestTarget = null;
        IPlayer? bestSpecTarget = null;
        float highestValue = -1.0f;
        float highestSpecValue = -1.0f;
        bool specFound = false;

        var players = Core.PlayerManager.GetAllPlayers();
        string flag = reserved_flag.Value;

        foreach (var player in players)
        {
            if (player.SteamID == incomingAdmin.UnauthorizedSteamID)
                continue;

            if (Core.Permission.PlayerHasPermission(player.SteamID, flag))
                continue;

            if (player.Controller.Connected != PlayerConnectedState.PlayerConnected)
                continue;

            float value = 0.0f;

            switch (type)
            {
                case KickType.Kick_HighestPing:
                    value = player.Controller.Ping;
                    break;
                case KickType.Kick_HighestTime:
                    GlobalPlayerTime.TryGetValue(player.SteamID, out double connectTime);
                    value = (float)(Core.Engine.GlobalVars.CurrentTime - connectTime);
                    break;
                case KickType.Kick_Random:
                    value = Random.Shared.Next(0, 100);
                    break;
            }

            if (player.Controller.ObserverPawn.Value != null)
            {
                specFound = true;
                if (value > highestSpecValue)
                {
                    highestSpecValue = value;
                    bestSpecTarget = player;
                }
            }
            else
            {
                if (value > highestValue)
                {
                    highestValue = value;
                    bestTarget = player;
                }
            }
        }

        return specFound ? bestSpecTarget : bestTarget;
    }

    public int GetClientCount() => Core.PlayerManager.GetAllPlayers().Count();
}