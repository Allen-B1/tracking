
using System.Collections;
using System.Runtime;
using System.Security.Cryptography.X509Certificates;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Random;

namespace tracking.trackingCode;

/*
public class RunState {
    public CombatDamage damage;

    public static RunState? instance;

    public RunState(int players) {
        damage = new CombatDamage(players);
    }
}*/

public class CombatState {
    public CombatDamage damage;
    public Dictionary<Creature, double[]> poison;
    public Dictionary<Creature, int[]> doom;
    public Dictionary<Creature, List<int>> vuln;
    public Dictionary<Creature, List<int>> weak;

    public int players {
        get => damage.damage.Length;
    }

    public static CombatState? instance;

    public CombatState(int players) {
        poison = new();
        doom = new();
        vuln = new();
        weak = new();

        damage = new CombatDamage(players);
    }

    public void addVuln(int player, Creature target, int amt) {
        if (!vuln.ContainsKey(target)) {
            vuln[target] = new();
        }

        for (var i = 0; i < amt; i++) {
            vuln[target].Add(player);        
        }
    }

    public void tickVuln(Creature target) {
        if (!vuln.ContainsKey(target)) {
            return;
        }

        if (vuln.ContainsKey(target) && vuln[target].Count != 0) {
            vuln[target].RemoveAt(0);
        }
    }

    public void addWeak(int player, Creature target, int amt) {
        if (!weak.ContainsKey(target)) {
            weak[target] = new();
        }

        for (var i = 0; i < amt; i++) {
            weak[target].Add(player);        
        }
    }

    public void tickWeak(Creature target) {
        if (!weak.ContainsKey(target)) {
            return;
        }

        if (weak.ContainsKey(target) && weak[target].Count != 0) {
            weak[target].RemoveAt(0);
        }
    }

    public void addAttack(int player, Creature enemy, int totalAmt, bool phrog, int cruelty, int tracking) {
        damage.damage[player].direct += totalAmt;
        damage.total += totalAmt;

        // calculate assist damage
        for (var i = 0; i < players; i++) {
            if (i == player) {
                continue;
            }

            bool assistVuln = vuln.ContainsKey(enemy) && vuln[enemy].Count != 0 && vuln[enemy][0] == i,
                 assistWeak = weak.ContainsKey(enemy) && weak[enemy].Count != 0 && weak[enemy][0] == i;
            if (!assistVuln && !assistWeak) {
                continue;
            }

            float vulnMult = assistVuln ? 1.5f + ((float)cruelty)/100 + (phrog ? 0.25f : 0f) : 1f;
            float weakMult = assistWeak && tracking != 0 ? tracking : 1f;
            var withoutAssistAmt = (int)(totalAmt / (vulnMult * weakMult));
            var assistAmt = totalAmt - withoutAssistAmt < 0 ? 0 : totalAmt - withoutAssistAmt;

            damage.damage[i].assist += assistAmt;
        }
    }

    public void addDirect(int player, int amt) {
        damage.damage[player].direct += amt;
        damage.total += amt;
    }

    public void addPoison(int player, Creature enemy, int amt) {
        if (!poison.ContainsKey(enemy)) {
            poison[enemy] = new double[players];
        }
        poison[enemy][player] += amt;
    }

    public void tickPoison(Creature enemy, int total) {
        if (!poison.ContainsKey(enemy)) {
            return;
        }
        var enemyPoison = poison[enemy];

        double sum = enemyPoison.Sum();
        int nplayers = 0;
        for (int i = 0; i < players; i++) {
            if (enemyPoison[i] != 0) {
                nplayers += 1;            
            }
        }

        double ratio = total / ((double)sum);
        damage.total += total;

        for (int i = 0; i < players; i++) {
            damage.damage[i].poison += (int)(enemyPoison[i] * ratio);
            enemyPoison[i] -= 1.0f / (double)nplayers;
            if (enemyPoison[i] < 0) {
                enemyPoison[i] = 0;
            }
        }
    }

    public void addDoom(int player, Creature enemy, int amt) {
        if (!doom.ContainsKey(enemy)) {
            doom[enemy] = new int[players];
        }
        doom[enemy][player] += amt;
    }

    public void tickDoom(Creature enemy, int total) {
        if (!doom.ContainsKey(enemy)) {
            return;
        }
        var enemyDoom = doom[enemy];

        int sum = enemyDoom.Sum();

        double ratio = ((double)total) / sum;
        damage.total += total;

        for (int i = 0; i < players; i++) {
            damage.damage[i].doom += (int)(enemyDoom[i] * ratio);
        }
    }
}

public struct CombatDamage {
    public int turns;
    public int total;
    public PlayerDamage[] damage;

    public CombatDamage(int players) {
        total = 0;
        damage = new PlayerDamage[players];
        turns = 0;
    }

    public void add(CombatDamage other) {
        turns += other.turns;
        total += other.total;
        
        for (var i = 0; i < damage.Length; i++) {
            damage[i].direct += other.damage[i].direct;
            damage[i].doom   += other.damage[i].doom;
            damage[i].poison += other.damage[i].poison;
            damage[i].assist += other.damage[i].assist;
        }
    }
}

public struct PlayerDamage {
    public int direct;
    public int assist;
    public int doom;
    public int poison;
}