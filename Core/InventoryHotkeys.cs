using Il2Cpp;
using UnityEngine.InputSystem;

namespace Thanatophobia;

// Number keys pick an inventory slot directly; vanilla can only cycle. Same guards as PCPropGrab.OnInventorySwap.
internal static class InventoryHotkeys {
    public static void Update() {
        var kb = Keyboard.current;
        if (kb == null) return;
        var target = kb.digit1Key.wasPressedThisFrame ? 0 : kb.digit2Key.wasPressedThisFrame ? 1 : kb.digit3Key.wasPressedThisFrame ? 2 : -1;
        if (target < 0) return;

        var game = GameController.field_Public_Static_GameController_0;
        if (game == null || game.field_Public_Boolean_1) return;
        var grab = game.field_Public_ObjectPublicPlStPlUnique_0?.field_Public_Player_0?.field_Public_PCPropGrab_0;
        if (grab == null || !grab.isActiveAndEnabled) return;
        if (grab.pcCanvas != null && grab.pcCanvas.field_Public_Boolean_0) return;
        var journal = LevelController.field_Public_Static_LevelController_0?.field_Public_JournalController_0;
        if (journal != null && journal.field_Public_Boolean_0) return;

        var slots = grab.field_Public_List_1_PhotonObjectInteract_0;
        var current = grab.field_Public_Int32_0;
        if (slots == null || target >= slots.Count || target == current) return;
        grab.SwitchHand(target - current); // SwitchHand takes a delta and only wraps out-of-range sums
    }
}
