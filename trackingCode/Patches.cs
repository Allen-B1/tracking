using System.Reflection;
using System.Reflection.Metadata.Ecma335;
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
    public static Node? panel;

    /** Run code in main thread */
    public static void run(Action action) {
        Callable.From(action).CallDeferred();
    }

    /** Must be run in main thread */    
    public static void updatePanel() {
        if (Patches.panel == null || CombatState.instance == null) {
            return;
        }

        TrackingPanel.updateWith(Patches.panel, CombatState.instance.damage);
    }
}

[HarmonyPatch(typeof(Hook))]
public static class HookPatches {
    [HarmonyPatch(nameof(Hook.BeforeCombatStart))]
    [HarmonyPostfix]
    public static void BeforeCombat(IRunState runState, ICombatState? combatState) {
        Patches.run(() => {
            if (combatState == null) {
                return;
            }

            if (Patches.panel == null) {
                if (Engine.GetMainLoop() is SceneTree tree && tree.Root != null) {
                    Patches.panel = TrackingPanel.create(tree.Root);
                    tree.Root.AddChild(Patches.panel);
                }
            }

            CombatState.instance = new CombatState(runState.Players.Count);

            if (Patches.panel != null) {
                TrackingPanel.updateHeader(Patches.panel, combatState.Encounter?.Title.GetRawText() ?? "Combat");
                Patches.updatePanel();
            }
        });
    }

    [HarmonyPatch(nameof(Hook.AfterCombatEnd))]
    [HarmonyPostfix]
    public static void AfterCombat() {
        Patches.run(() => {
            CombatState.instance = null;        
        });
    }

    [HarmonyPatch(nameof(Hook.AfterPlayerTurnStart))]
    [HarmonyPostfix]
    public static void TurnStart(ICombatState combatState) {
        int roundNumber = combatState.RoundNumber;

        Patches.run(() => {
            if (CombatState.instance == null) {
                return;
            }

            CombatState.instance.damage.turns = roundNumber;
        });
    }

    [HarmonyPatch(nameof(Hook.AfterTurnEnd))]
    [HarmonyPostfix]
    public static void TurnEnd(CombatSide side) {
        if (side == CombatSide.Player) {
            Patches.run(() => {
                Patches.updatePanel();               
            });
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

        Patches.run(() => {
            if (CombatState.instance == null) {
                return;
            }
                
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
        });
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

        Patches.run(() => {
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
        });
    }
}

[HarmonyPatch(typeof(DoomPower), nameof(DoomPower.DoomKill))]
public sealed class AfterDoomPatch {
    [HarmonyPrefix]
    public static void Prefix(IReadOnlyList<Creature> creatures) {
        MainFile.Logger.Info("ticking doom...");

        Patches.run(() => {
            if (CombatState.instance == null) {
                return;
            }

            foreach (var target in creatures) {
                CombatState.instance.tickDoom(target, target.CurrentHp);
            }

            Patches.updatePanel();
        });
    }
}

[HarmonyPatch(typeof(PoisonPower), nameof(PoisonPower.AfterSideTurnStart))]
public static class AfterPoisonPatch {
    [HarmonyPrefix]
    public static void Prefix(CombatSide side, ICombatState combatState, PoisonPower __instance) {
        if (side != __instance.Owner.Side) {
            return;
        }

        int triggerCount = (int)typeof(PoisonPower)
            .GetProperty("TriggerCount", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(__instance)!;
        Patches.run(() => {
            if (CombatState.instance == null) {
                return;
            }

            MainFile.Logger.Info("ticking poison " + triggerCount + " times...");
            for (var i = 0; i < triggerCount; i++) {
                CombatState.instance.tickPoison(__instance.Owner, __instance.Amount > __instance.Owner.CurrentHp ? __instance.Owner.CurrentHp : __instance.Amount);
            }   

            Patches.updatePanel();            
        });
    }
}

[HarmonyPatch]
public static class StatusPatch {
    [HarmonyPatch(typeof(VulnerablePower), nameof(VulnerablePower.AfterTurnEnd))]
    [HarmonyPrefix]
    public static void TickVuln(VulnerablePower __instance, CombatSide side) {
        if (side != CombatSide.Enemy || !__instance.Owner.IsEnemy) {
            return;
        }

        Patches.run(() => {
            CombatState.instance?.tickVuln(__instance.Owner);             
        });
    }

    [HarmonyPatch(typeof(WeakPower), nameof(WeakPower.AfterTurnEnd))]
    [HarmonyPrefix]
    public static void TickWeak(WeakPower __instance, CombatSide side) {
        if (side != CombatSide.Enemy || !__instance.Owner.IsEnemy) {
            return;
        }

        Patches.run(() => {
            CombatState.instance?.tickWeak(__instance.Owner);        
        });
    }
}