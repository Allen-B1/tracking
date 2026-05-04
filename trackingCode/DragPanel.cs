using Godot;

[GlobalClass]
public partial class DragPanel : PanelContainer {
    private bool _isDragging = false;
    private Vector2 _dragOffset;

    public override void _Input(InputEvent @event) {
        if (@event is InputEventMouseButton mouseEvent) {
            if (mouseEvent.ButtonIndex == MouseButton.Left) {
                if (mouseEvent.Pressed) {
                    if (!GetGlobalRect().HasPoint(mouseEvent.Position)) {
                        return;
                    }
                    
                    _isDragging = true;
                    _dragOffset = GetGlobalMousePosition() - GlobalPosition;
                } else {
                    _isDragging = false;
                }
            }
        }
    }

    public override void _Process(double delta) {
        if (_isDragging) {
            GlobalPosition = GetGlobalMousePosition() - _dragOffset;
        }
    }
}