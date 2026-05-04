using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

namespace tracking.trackingCode;

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]

public static class BeforeCombatPatch {
    [HarmonyPostfix]
    public static void Postfix(IRunState runState, ICombatState? combatState) {
        if (combatState == null) {
            Log.Info("combatState is null");
            return;
        }

        CombatState.instance = new CombatState(runState.Players.Count);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatEnd))]

public static class AfterCombatPatch {
    [HarmonyPostfix]
    public static void Postfix() {
        CombatState.instance = null;

        // TODO: add CombatState.instance to RunState.instance
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterPlayerTurnStart))]

public static class TurnStartPatch {
    [HarmonyPostfix]
    public static void Postfix() {
        if (CombatState.instance == null) {
            return;
        }

        lock (CombatState.instance) {
            CombatState.instance.damage.turns += 1;            
        }
    }
}

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.BeforeApplied))] 
public static class BeforeApplyPowerPatch {
    [HarmonyPostfix]
    public static void Postfix(Creature applier, decimal amount, Creature target, PowerModel __instance) {
        var player = applier == null ? null : applier.Player != null ? applier.Player : applier.PetOwner;
        var state = player?.RunState;
        if (state == null || CombatState.instance == null || target.CombatState == null) {
            return;
        }

        var playerIdx = state.Players.IndexOf(player);
        if (playerIdx < 0) {
            return;
        }

        lock (CombatState.instance) {
            if (__instance is DoomPower) {
                MainFile.Logger.Info("applying doom...");
                CombatState.instance.addDoom(playerIdx, target, (int)amount);
            } else if (__instance is PoisonPower) {
                MainFile.Logger.Info("applying poison...");
                CombatState.instance.addPoison(playerIdx, target, (int)amount);
            }            
        }
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDamageGiven))]
public static class AfterDamagePatch {
    [HarmonyPostfix]
    public static void Postfix(ICombatState combatState, Creature dealer, DamageResult results, Creature target) {
        var player = dealer == null ? null : dealer.Player != null ? dealer.Player : dealer.PetOwner;
        var state = player?.RunState;
        if (state == null) {
            return;
        }
        var playerIdx = state.Players.IndexOf(player);
        var enemyIdx = combatState.Enemies.IndexOf(target);
        if (playerIdx < 0 || enemyIdx < 0) {
            return;
        }

        MainFile.Logger.Info("damage " + playerIdx + "," + enemyIdx + ": " + (results.UnblockedDamage + results.BlockedDamage));

        if (CombatState.instance == null) {
            return;
        }

        lock (CombatState.instance) {
            CombatState.instance.addDirect(playerIdx, results.UnblockedDamage + results.BlockedDamage);
        }

        TrackingPanel.update();
    }
}


[HarmonyPatch(typeof(DoomPower), nameof(DoomPower.DoomKill))]
public sealed class AfterDoomPatch {
    [HarmonyPrefix]
    public static void Prefix(IReadOnlyList<Creature> creatures) {
        var cstate = CombatState.instance;
        if (cstate == null) {
            return;
        }

        MainFile.Logger.Info("ticking doom...");

        lock (cstate) {
            foreach (var target in creatures) {
                cstate.tickDoom(target, target.CurrentHp);
            }            
        }
        TrackingPanel.update();
    }
}

[HarmonyPatch(typeof(PoisonPower), nameof(PoisonPower.AfterSideTurnStart))]
public static class AfterPoisonPatch {
    [HarmonyPrefix]
    public static void Prefix(CombatSide side, ICombatState combatState, PoisonPower __instance) {
        if (side != __instance.Owner.Side || CombatState.instance == null) {
            return;
        }

        lock (CombatState.instance) {
            int triggerCount = (int)typeof(PoisonPower)
                .GetProperty("TriggerCount", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(__instance)!;
            MainFile.Logger.Info("ticking poison " + triggerCount + " times...");
            for (var i = 0; i < triggerCount; i++) {
                CombatState.instance.tickPoison(__instance.Owner, __instance.Amount > __instance.Owner.CurrentHp ? __instance.Owner.CurrentHp : __instance.Amount);
            }   
        }

        TrackingPanel.update();
    }
}
