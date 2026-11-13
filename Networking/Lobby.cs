using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppPhoton.Pun;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Thanatophobia;

internal static class Lobby {
    // The cap is enforced late so the game's own room setup (which sets a cap of 4) runs first.
    private const float RaiseCapDelay = 2f;

    private static float? _joinedAt;
    private static ServerManager _server;

    // Captured when the lobby loads; null outside it (e.g. during a contract), which gates the overrides.
    public static void OnLobbyStart(ServerManager server) {
        _server = server;
        _joinedAt = null;
    }

    public static void Update() {
        if (!PhotonNetwork.InRoom) {
            ExtraVolumeSlidersPatch.Volumes.Clear(); // keyed by actor number, which restarts in the next room
            _joinedAt = null;
            return;
        }

        _joinedAt ??= Time.time;

        if (_server == null) return; // only act while the lobby exists, so missions can still lock the room
        var elapsed = Time.time - _joinedAt.Value;

        if (elapsed >= RaiseCapDelay && PhotonNetwork.IsMasterClient) {
            // The game keeps resetting the room to a four-player cap, so re-apply ours every frame it drifts.
            var room = PhotonNetwork.CurrentRoom;
            if (room.MaxPlayers != Settings.MaxPlayers.Value) room.MaxPlayers = Settings.MaxPlayers.Value;
            if (!room.IsOpen) room.IsOpen = true; // Photon closes a full room; reopen it once the cap is raised
        }
    }
}

// The browser row appends a fixed "/4" to the player count. Show just the count.
[HarmonyPatch(typeof(ServerListItem), nameof(ServerListItem.SetUI))]
internal static class RoomCountTextPatch {
    private static void Postfix(ServerListItem __instance, Il2CppPhoton.Realtime.RoomInfo __2) {
        var text = __instance.field_Public_Text_1;
        if (text != null && __2 != null) text.text = __2.PlayerCount.ToString();
    }
}

// The server browser's own filter drops every room with more than three players, so put the bigger
// ones back while they still have space.
[HarmonyPatch(typeof(LobbyRoomList), nameof(LobbyRoomList.OnRoomListUpdate))]
internal static class BigRoomListPatch {
    private static void Postfix(LobbyRoomList __instance, Il2CppSystem.Collections.Generic.List<Il2CppPhoton.Realtime.RoomInfo> __0) {
        var rooms = __instance.field_Public_Dictionary_2_String_RoomInfo_0;
        if (rooms == null || __0 == null) return;

        foreach (var info in __0) {
            if (info.RemovedFromList || !info.IsOpen || !info.IsVisible || info.PlayerCount <= 3) continue;
            if (info.MaxPlayers != 0 && info.PlayerCount >= info.MaxPlayers) continue; // full
            rooms[info.Name] = info;
        }
    }
}

// Clicking a listed room refuses anything with four or more players. Joining by name does the same
// thing without that check, so send the bigger rooms that way.
[HarmonyPatch(typeof(LobbyManager), nameof(LobbyManager.JoinServer))]
internal static class BigRoomJoinPatch {
    private static bool Prefix(LobbyManager __instance, Il2CppPhoton.Realtime.RoomInfo __0) {
        if (__0 == null || !__0.IsOpen || !__0.IsVisible || __0.PlayerCount < 4) return true;
        __instance.JoinServerByName(__0.Name);
        return false;
    }
}

// StartGame closes and caps the room for the mission while the lobby still exists, so stop overriding
// or we'd reopen it and people could join mid-game.
[HarmonyPatch(typeof(ServerManager), nameof(ServerManager.StartGame))]
internal static class LobbyStartGamePatch {
    private static void Postfix() {
        var room = PhotonNetwork.CurrentRoom;
        if (room != null && !room.IsOpen) Lobby.OnLobbyStart(null); // still open = the game refused to start
    }
}

// The lobby loads fresh after each contract, resetting the cap, so re-capture it and re-apply.
[HarmonyPatch(typeof(ServerManager), "Start")]
internal static class LobbyStartPatch {
    private static void Postfix(ServerManager __instance) => Lobby.OnLobbyStart(__instance);
}

// A fifth player would make the native UI throw before it updates Start and Ready.
// shortcut: rows only shrink to fit, so text is hard to read past ~12 players; add paging if that matters.
[HarmonyPatch]
internal static class LobbyRowsPatch {
    private const int NativeRows = 4;
    private const float RowHeight = 100f;
    private static readonly Vector3 ListTopLeft = new(-505f, 196f, 0f);

    private static RectTransform _list;

    private static IEnumerable<MethodBase> TargetMethods() {
        yield return AccessTools.Method(typeof(ServerManager), nameof(ServerManager.UpdateUI));
        yield return AccessTools.Method(typeof(ServerManager), nameof(ServerManager.NetworkedReady));
        yield return AccessTools.Method(typeof(ServerManager), nameof(ServerManager.UpdateCharacter));
        yield return AccessTools.Method(typeof(ServerManager), nameof(ServerManager.SetPlayerInformation));
    }

    private static void Prefix(ServerManager __instance) {
        var players = Math.Max(PhotonNetwork.PlayerList.Length, __instance.field_Public_List_1_ObjectPublicBoInPlInPlUnique_0.Count);
        if (players <= NativeRows && _list == null) return;

        if (_list == null) CreateList(__instance);
        AddRows(__instance, players);
        AddKickButtons(__instance, players);

        // The lobby UI is reused between rooms, so hide the rows we added that this room doesn't need.
        var rows = __instance.field_Public_List_1_ServerItem_0;
        for (var i = NativeRows; i < rows.Count; i++) rows[i].gameObject.SetActive(i < players);

        // Same for the kick buttons we added, or a kicked player's X stays behind over the buttons below.
        var kicks = __instance.kickPlayerButtons;
        for (var i = NativeRows; i < kicks.Length; i++) kicks[i].gameObject.SetActive(i < players && PhotonNetwork.IsMasterClient);

        var scale = Mathf.Min(1f, (float)NativeRows / Math.Max(players, NativeRows));
        _list.localScale = Vector3.one * scale;
        _list.localPosition = ListTopLeft * (1f - scale);
    }

    private static void CreateList(ServerManager server) {
        var parent = server.field_Public_List_1_ServerItem_0[0].transform.parent;
        _list = new GameObject("PlayerRows").AddComponent<RectTransform>();
        _list.gameObject.layer = parent.gameObject.layer;
        _list.SetParent(parent, false);
        _list.sizeDelta = Vector2.zero;

        foreach (var row in server.field_Public_List_1_ServerItem_0) row.transform.SetParent(_list, false);
        foreach (var button in server.kickPlayerButtons) button.transform.SetParent(_list, false);
    }

    private static void AddRows(ServerManager server, int count) {
        var rows = server.field_Public_List_1_ServerItem_0;
        var template = rows[NativeRows - 1];

        for (var i = rows.Count; i < count; i++) {
            var row = Object.Instantiate(template, _list);
            row.transform.localPosition = PositionBelow(template.transform, i);
            rows.Add(row);
        }
    }

    private static void AddKickButtons(ServerManager server, int count) {
        var buttons = server.kickPlayerButtons;
        if (buttons.Length >= count) return;

        var grown = new Il2CppReferenceArray<Button>(count);
        for (var i = 0; i < buttons.Length; i++) grown[i] = buttons[i];

        var template = buttons[NativeRows - 1];
        for (var i = buttons.Length; i < count; i++) {
            var index = i;
            var button = Object.Instantiate(template, _list);
            button.transform.localPosition = PositionBelow(template.transform, i);

            // The copied click event would still kick the template's player.
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener((UnityAction)new Action(() => server.KickPlayer(index)));
            grown[i] = button;
        }
        server.kickPlayerButtons = grown;
    }

    private static Vector3 PositionBelow(Transform template, int index) =>
        template.localPosition + Vector3.down * (RowHeight * (index - (NativeRows - 1)));
}
