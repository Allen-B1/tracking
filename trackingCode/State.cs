
using System.Collections;
using System.Security.Cryptography.X509Certificates;
using BaseLib.Patches.Content;
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
    public Dictionary<Creature, int[]> vuln;
    public Dictionary<Creature, int[]> weak;

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

        double ratio = ((double)total) / ((double)sum) ;
        damage.total += total;

        for (int i = 0; i < players; i++) {
            damage.damage[i].doom += (int)(enemyDoom[i] * ratio);
        }
    }

    public void endTurn() {
        damage.turns += 1;
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
        }
    }
}

public struct PlayerDamage {
    public int direct;
    public int doom;
    public int poison;
}