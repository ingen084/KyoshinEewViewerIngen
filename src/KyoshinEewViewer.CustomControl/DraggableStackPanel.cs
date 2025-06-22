using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Linq;

namespace KyoshinEewViewer.CustomControl;

public class DraggableStackPanel : Panel
{
    public static readonly AttachedProperty<bool> IsDraggableProperty =
        AvaloniaProperty.RegisterAttached<DraggableStackPanel, Control, bool>("IsDraggable");

    public static readonly AttachedProperty<bool> IsDragHandleProperty =
        AvaloniaProperty.RegisterAttached<DraggableStackPanel, Control, bool>("IsDragHandle");

    public static readonly RoutedEvent<ItemMovedEventArgs> ItemMovedEvent =
        RoutedEvent.Register<DraggableStackPanel, ItemMovedEventArgs>(nameof(ItemMoved), RoutingStrategies.Bubble);

    public event EventHandler<ItemMovedEventArgs>? ItemMoved
    {
        add => AddHandler(ItemMovedEvent, value);
        remove => RemoveHandler(ItemMovedEvent, value);
    }

    public static bool GetIsDraggable(Control control) => control.GetValue(IsDraggableProperty);
    public static void SetIsDraggable(Control control, bool value) => control.SetValue(IsDraggableProperty, value);

    public static bool GetIsDragHandle(Control control) => control.GetValue(IsDragHandleProperty);
    public static void SetIsDragHandle(Control control, bool value) => control.SetValue(IsDragHandleProperty, value);

    private Control? _draggedElement;
    private Point _dragStartPoint;
    private int _draggedIndex = -1;
    private int _insertIndex = -1;
    private bool _isDragging;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        
        // すべての子要素にイベントハンドラーを設定
        foreach (var child in Children.OfType<Control>())
        {
            AttachDragHandlers(child);
        }
    }


    private void AttachDragHandlers(Control control)
    {
        control.PointerPressed += OnChildPointerPressed;
        control.PointerMoved += OnChildPointerMoved;
        control.PointerReleased += OnChildPointerReleased;
        
        // 子要素も再帰的に処理
        if (control is Panel panel)
        {
            foreach (var child in panel.Children.OfType<Control>())
            {
                AttachDragHandlers(child);
            }
        }
        else if (control is ContentControl content && content.Content is Control contentChild)
        {
            AttachDragHandlers(contentChild);
        }
    }

    private void OnChildPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control sourceControl) return;
        
        // ドラッグハンドルかチェック
        if (!IsValidDragHandle(sourceControl)) return;
        
        // ドラッグ可能な親要素を探す
        var draggableElement = FindDraggableParent(sourceControl);
        if (draggableElement == null) return;
        
        _draggedElement = draggableElement;
        _dragStartPoint = e.GetPosition(this);
        _draggedIndex = Children.IndexOf(draggableElement);
        _isDragging = false;
        
        e.Pointer.Capture(sourceControl);
        e.Handled = true;
    }

    private void OnChildPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedElement == null) return;
        
        var currentPoint = e.GetPosition(this);
        var distance = Math.Sqrt(Math.Pow(currentPoint.X - _dragStartPoint.X, 2) + Math.Pow(currentPoint.Y - _dragStartPoint.Y, 2));
        
        // ドラッグ開始の閾値
        if (!_isDragging && distance > 5)
        {
            _isDragging = true;
            _draggedElement.ZIndex = 1000;
            _draggedElement.Opacity = 0.7;
        }
        
        if (_isDragging)
        {
            // 挿入位置を計算
            UpdateInsertIndex(currentPoint.Y);
            ShowInsertIndicator();
            InvalidateArrange();
        }
        
        e.Handled = true;
    }

    private void OnChildPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggedElement == null) return;
        
        var sourceControl = sender as Control;
        e.Pointer.Capture(null);
        
        if (_isDragging && _insertIndex != -1 && _insertIndex != _draggedIndex)
        {
            // アイテム移動イベントを発火
            var args = new ItemMovedEventArgs(ItemMovedEvent, _draggedIndex, _insertIndex);
            RaiseEvent(args);
        }
        
        // ドラッグ状態をリセット
        if (_draggedElement != null)
        {
            _draggedElement.ZIndex = 0;
            _draggedElement.Opacity = 1.0;
        }
        
        HideInsertIndicator();
        _draggedElement = null;
        _draggedIndex = -1;
        _insertIndex = -1;
        _isDragging = false;
        
        InvalidateArrange();
        e.Handled = true;
    }

    private bool IsValidDragHandle(Control control)
    {
        return GetIsDragHandle(control) || GetIsDraggable(control);
    }

    private Control? FindDraggableParent(Control control)
    {
        var current = control;
        while (current != null && current != this)
        {
            if (GetIsDraggable(current))
                return current;
            current = current.Parent as Control;
        }
        return null;
    }

    private void UpdateInsertIndex(double y)
    {
        _insertIndex = -1;
        
        for (int i = 0; i < Children.Count; i++)
        {
            if (Children[i] == _draggedElement) continue;
            
            var childBounds = Children[i].Bounds;
            var childCenter = childBounds.Y + childBounds.Height / 2;
            
            if (y < childCenter)
            {
                _insertIndex = i;
                break;
            }
        }
        
        if (_insertIndex == -1)
        {
            _insertIndex = Children.Count - 1;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var totalHeight = 0.0;
        var maxWidth = 0.0;
        
        foreach (var child in Children)
        {
            child.Measure(availableSize);
            totalHeight += child.DesiredSize.Height;
            maxWidth = Math.Max(maxWidth, child.DesiredSize.Width);
        }
        
        return new Size(maxWidth, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var currentY = 0.0;
        
        for (int i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            var childHeight = child.DesiredSize.Height;
            child.Arrange(new Rect(0, currentY, finalSize.Width, childHeight));
            currentY += childHeight;
        }
        
        return finalSize;
    }

    private Border? _insertIndicator;

    private void ShowInsertIndicator()
    {
        if (_insertIndicator == null)
        {
            _insertIndicator = new Border
            {
                Height = 2,
                Background = Brushes.DodgerBlue,
                Margin = new Thickness(0, 1)
            };
        }

        if (!Children.Contains(_insertIndicator))
        {
            Children.Insert(Math.Min(_insertIndex, Children.Count), _insertIndicator);
        }
    }

    private void HideInsertIndicator()
    {
        if (_insertIndicator != null && Children.Contains(_insertIndicator))
        {
            Children.Remove(_insertIndicator);
        }
    }
}

public class ItemMovedEventArgs : RoutedEventArgs
{
    public int OldIndex { get; }
    public int NewIndex { get; }

    public ItemMovedEventArgs(RoutedEvent routedEvent, int oldIndex, int newIndex) : base(routedEvent)
    {
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }
}