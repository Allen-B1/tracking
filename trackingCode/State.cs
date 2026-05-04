
using System.Security.Cryptography.X509Certificates;
using BaseLib.Patches.Content;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Random;

namespace tracking.trackingCode;

public class RunState {
    public CombatDamage damage;

    public static RunState? instance;

    public RunState(int players) {
        damage = new CombatDamage(players);
    }
}

public class CombatState {
    public CombatDamage damage;
    public int[] poison;
    public int[] doom;

    public int players {
        get => damage.damage.Length;
    }

    public int enemies {
        get => poison.Length / players;
    }

    public static CombatState? instance;

    public CombatState(int players, int enemies) {
        poison = new int[players * enemies];
        doom = new int[players * enemies];
        damage = new CombatDamage(players);
    }

    public void addDirect(int player, int amt) {
        damage.damage[player].direct += amt;
        damage.total += amt;
    }

    public void addPoison(int player, int enemy, int amt) {
        poison[player*enemies+enemy] += amt;
    }

    public void tickPoison(int enemy, int total) {
        int sum = 0;
        for (int i = 0; i < players; i++) {
            sum += poison[i*enemies+enemy];
        }

        double ratio = ((double)total) / ((double)sum);
        damage.total += total;

        for (int i = 0; i < players; i++) {
            damage.damage[i].poison += (int)((double)poison[i*enemies+enemy] * ratio);
            poison[i*enemies+enemy] -= 1;
        }
    }

    public void addDoom(int player, int enemy, int amt) {
        doom[player*enemies+enemy] += amt;
    }

    public void tickDoom(int enemy, int total) {
        int sum = 0;
        for (int i = 0; i < players; i++) {
            sum += doom[i*enemies+enemy];
        }

        double ratio = ((double)total) / ((double)sum) ;
        damage.total += total;

        for (int i = 0; i < players; i++) {
            damage.damage[i].doom += (int)((double)doom[i*enemies+enemy] * ratio);
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