
using BaseLib.Patches.Features;
using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Platform.Null;
using MegaCrit.Sts2.Core.Runs;

namespace tracking.trackingCode;

public static class TrackingPanel {
    public static AddedNode<NCombatRoom, DragPanel> node = new((room) => {
        MainFile.Logger.Info("creating node");
        var WIDTH = Row.ROW_SIZE + 32*2;
        var root = new DragPanel {
            GlobalPosition = new(room.Size.X - WIDTH, 96),
            Size = new(0, 0),   
            ZIndex = 100,
        };

        root.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
            BgColor = new("111111ff"),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8
        });

        var inner = new MarginContainer {};
        inner.AddThemeConstantOverride("margin_top",    16);
        inner.AddThemeConstantOverride("margin_bottom", 16);
        inner.AddThemeConstantOverride("margin_left",   16);
        inner.AddThemeConstantOverride("margin_right",  16);
        root.AddChild(inner);

        var rows = new VBoxContainer {
            Size = new(Row.ROW_SIZE, 0f)
        };
        rows.AddThemeConstantOverride("separation", 16);
        inner.AddChild(rows);

        MainFile.Logger.Info("setting TrackingPanel.instance");
        instance = root;

        return root;
    });

    public static void updateWith(Node node, CombatDamage damage) {
        Control root = (Control)node;

        var players = RunManager.Instance.DebugOnlyGetState()?.Players;
        if (players == null) {
            MainFile.Logger.Error("megacrit::RunState is null");
            return;
        }

        var rows = root.GetChild(0).GetChild(0);
        for (var i = 0; i < damage.damage.Length; i++) {
            var name = PlatformUtil.PrimaryPlatform == PlatformType.None ?
                null
                : PlatformUtil.GetPlayerName(PlatformUtil.PrimaryPlatform, players[i].NetId);
            if (name == null || name == players[i].NetId.ToString()) {
                name = players[i].Character.Title.GetRawText();
            }

            var child = rows.GetChild(i);
            if (child == null) {
                MainFile.Logger.Info("creating bar");
                child = Row.create(name);
                rows.AddChild(child);
            }

            Row.set(child, damage.damage[i], damage.total, damage.turns);
        }
    }

    public static Node? instance;
}

public static class Util {
    public static Label label(string text) {
        var node = new Label {
            Text = text,
            ZIndex = 101,
            SizeFlagsHorizontal = 0,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(Row.ROW_LABEL_SIZE, 32f)
        };
        node.AddThemeFontSizeOverride("font_size", 14);
        node.AddThemeColorOverride("font_color", new("ffffffff"));
        return node;
    }

    public static float ROW_RECT_SIZE = 384;

    public static Node rect(Color color) {
        var root = new PanelContainer {
            CustomMinimumSize = new Vector2(0f, 32f),
            SizeFlagsHorizontal = 0,
            Visible = false
        };
        root.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
            BgColor = color
        });

        var node = new Label {
            Text = "",
            ZIndex = 101,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        node.AddThemeFontSizeOverride("font_size", 14);
        node.AddThemeColorOverride("font_color", new("ffffffff"));
        root.AddChild(node);

        return root;
    }

    public static void setRectText(Node rect, string text) {
        ((Label)rect.GetChild(0)).Text = text;   
    }

    public static void setRectSize(Node rect, float size) {
        if (size == 0) {
            ((PanelContainer)rect).Visible = false;            
        } else {
            ((PanelContainer)rect).CustomMinimumSize = new Vector2(size*ROW_RECT_SIZE, 32f);
            ((PanelContainer)rect).Visible = true;
        }
    }
}

public static class Row {
    public static float ROW_LABEL_SIZE = 192;
    public static float ROW_SIZE = ROW_LABEL_SIZE + Util.ROW_RECT_SIZE;
    public static Node create(string name) {
        var root = new HBoxContainer {
            ZIndex = 100,
            CustomMinimumSize = new(ROW_SIZE, 32f)
        };
        root.AddThemeConstantOverride("separation", 0);

        var nameL = Util.label(name);
        nameL.Size = new Vector2(32f, ROW_LABEL_SIZE);
        var direct = Util.rect(new("607D8BFF"));
        var assist = Util.rect(new("009688FF"));
        var poison = Util.rect(new("4CAF50FF"));
        var doom = Util.rect(new("9C27B0FF"));

        root.AddChild(nameL);
        root.AddChild(direct);
        root.AddChild(assist);
        root.AddChild(poison);
        root.AddChild(doom);

        return root;
    }

    public static void set(Node root, PlayerDamage damage, int total, int turns) {
        var direct = root.GetChild(1);
        var assist = root.GetChild(2);
        var poison = root.GetChild(3);
        var doom =   root.GetChild(4);

        Util.setRectText(direct, damage.direct == 0 ? "" : ((double)damage.direct / turns).ToString("F1"));
        Util.setRectText(assist, damage.assist == 0 ? "" : ((double)damage.assist / turns).ToString("F1"));
        Util.setRectText(poison, damage.poison == 0 ? "" : ((double)damage.poison / turns).ToString("F1"));
        Util.setRectText(doom  , damage.doom   == 0 ? "" : ((double)damage.doom   / turns).ToString("F1"));

        Util.setRectSize(direct, total == 0 ? 0 : (float)damage.direct / total);
        Util.setRectSize(assist, total == 0 ? 0 : (float)damage.assist / total);
        Util.setRectSize(poison, total == 0 ? 0 : (float)damage.poison / total);
        Util.setRectSize(doom  , total == 0 ? 0 : (float)damage.doom   / total);

        ((HBoxContainer)root).QueueSort();
    }
}