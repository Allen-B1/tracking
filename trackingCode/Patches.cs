using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using BaseLib.Extensions;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
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
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer.Transport.ENet;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

using ICombatState = MegaCrit.Sts2.Core.Combat.CombatState;

namespace tracking.trackingCode;

public static class Patches {
    public static void updatePanel() {
        Callable.From(() => {
            if (TrackingPanel.instance == null || CombatState.instance == null) {
                return;
            }

            lock (CombatState.instance) {
                TrackingPanel.updateWith(TrackingPanel.instance, CombatState.instance.damage);                
            }
        }).CallDeferred();
    }
}

[HarmonyPatch(typeof(Hook))]
public static class HookPatches {
    [HarmonyPatch(nameof(Hook.BeforeCombatStart))]
    [HarmonyPostfix]
    public static void BeforeCombat(IRunState runState, ICombatState? combatState) {
        if (combatState == null) {
            Log.Info("combatState is null");
            return;
        }

        CombatState.instance = new CombatState(runState.Players.Count);
    }

    [HarmonyPatch(nameof(Hook.AfterCombatEnd))]
    [HarmonyPostfix]
    public static void AfterCombat() {
        CombatState.instance = null;
    }

    [HarmonyPatch(nameof(Hook.AfterPlayerTurnStart))]
    [HarmonyPostfix]
    public static void TurnStart(ICombatState combatState) {
        if (CombatState.instance == null) {
            return;
        }

        lock (CombatState.instance) {
            CombatState.instance.damage.turns = combatState.RoundNumber;
        }
        MainFile.Logger.Info("turn started: " + CombatState.instance.damage.turns);
    }

    [HarmonyPatch(nameof(Hook.AfterTurnEnd))]
    [HarmonyPostfix]
    public static void TurnEnd(CombatSide side) {
        if (side == CombatSide.Player) {
            Patches.updatePanel();   
        }
    }
    
    [HarmonyPatch(nameof(Hook.AfterDamageGiven))]
    [HarmonyPostfix]
    public static void AfterDamage(ICombatState combatState, Creature dealer, DamageResult results, Creature target, CardModel cardSource) {
        var player = dealer == null ? null : dealer.Player != null ? dealer.Player : dealer.PetOwner;
        var state = player?.RunState;
        if (state == null || player == null) {
            return;
        }
        var playerIdx = state.Players.IndexOf(player);
        var enemyIdx = combatState.Enemies.IndexOf(target);
        if (playerIdx < 0 || enemyIdx < 0) {
            return;
        }

        if (CombatState.instance == null) {
            return;
        }
                
        lock (CombatState.instance) {
            if (cardSource != null && cardSource.Type == CardType.Attack) {
                MainFile.Logger.Info("attack");
                CombatState.instance.addAttack(playerIdx, target, results.UnblockedDamage + results.BlockedDamage,
                    player.GetRelic<PaperPhrog>() != null, 
                    player.Creature.GetPower<CrueltyPower>() != null ? player.Creature.GetPower<CrueltyPower>()!.Amount : 0,
                    player.Creature.GetPower<TrackingPower>() != null ? player.Creature.GetPower<TrackingPower>()!.Amount : 0);
            } else {
                MainFile.Logger.Info("damage " + playerIdx + "," + enemyIdx + ": " + (results.UnblockedDamage + results.BlockedDamage));
                CombatState.instance.addDirect(playerIdx, results.UnblockedDamage + results.BlockedDamage);
            }
        }
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforePowerAmountChanged))] 
public static class BeforeApplyPowerPatch {
    [HarmonyPrefix]
    public static void Prefix(Creature? applier, decimal amount, Creature target, PowerModel power) {
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
            if (power is DoomPower) {
                MainFile.Logger.Info("applying doom...");
                CombatState.instance.addDoom(playerIdx, target, (int)amount);
            } else if (power is PoisonPower) {
                MainFile.Logger.Info("applying poison: " + amount);
                CombatState.instance.addPoison(playerIdx, target, (int)amount);
            } else if (power is VulnerablePower) {
                MainFile.Logger.Info("applying vuln: " + amount);
                CombatState.instance.addVuln(playerIdx, target, (int)amount);
            } else if (power is WeakPower) {
                CombatState.instance.addWeak(playerIdx, target, (int)amount); 
            }
        }
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

        Patches.updatePanel();
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

        Patches.updatePanel();
    }
}

[HarmonyPatch]
public static class StatusPatch {
    [HarmonyPatch(typeof(VulnerablePower), nameof(VulnerablePower.AfterTurnEnd))]
    [HarmonyPrefix]
    public static void TickVuln(VulnerablePower __instance, CombatSide side) {
        if (side != CombatSide.Enemy || CombatState.instance == null || !__instance.Owner.IsEnemy) {
            return;
        }

        lock (CombatState.instance) {
            CombatState.instance.tickVuln(__instance.Owner); 
        }
    }

    [HarmonyPatch(typeof(WeakPower), nameof(WeakPower.AfterTurnEnd))]
    [HarmonyPrefix]
    public static void TickWeak(WeakPower __instance, CombatSide side) {
        if (side != CombatSide.Enemy || CombatState.instance == null || !__instance.Owner.IsEnemy) {
            return;
        }

        lock (CombatState.instance) {
            CombatState.instance.tickWeak(__instance.Owner);        
        }
    }

}