using System;

namespace Common.Core;

public partial class DragDropEventHandlerNode : Node
{
	public Action<DraggableNode2D> OnDragEnter { get; set; }
	public Action<DraggableNode2D> OnDragLeave { get; set; }
	public Action<DraggableNode2D> OnDragDrop { get; set; }
}
