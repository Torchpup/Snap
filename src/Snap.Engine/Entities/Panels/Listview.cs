namespace Snap.Engine.Entities.Panels;

/// <summary>
/// Represents an individual selectable item in a <see cref="Listview"/>.
/// </summary>
public class ListviewItem : Entity
{
	private ColorRect _bar;
	private bool _selected;

	/// <summary>
	/// Called when this item becomes selected in its parent listview.
	/// Override to define selection behavior.
	/// </summary>
	/// <param name="listview">The parent listview.</param>
	/// <param name="selectedIndex">The global index of the selected item.</param>
	public virtual void OnSelected(Listview listview, int selectedIndex) { }

	/// <summary>
	/// Called when the selected state of this item changes.
	/// Override to respond to selection toggling.
	/// </summary>
	/// <param name="listview">The parent listview.</param>
	/// <param name="selectedIndex">The global index of the selected item.</param>
	public virtual void OnSelectedChanged(Listview listview, int selectedIndex) { }

	/// <summary>
	/// Gets or sets whether this item is currently selected.
	/// Triggers selection callbacks and toggles the visibility of the internal highlight bar.
	/// </summary>
	public bool Selected
	{
		get => _selected;
		set
		{
			bool oldValue = _selected;
			if (_selected == value)
				return;
			_selected = value;

			if (_bar != null)
				_bar.IsVisible = _selected;

			if (_selected)
				OnSelected((Listview)Parent, ChildIndex);

			if (_selected != oldValue)
				OnSelectedChanged(ParentAs<Listview>(), ChildIndex);
		}
	}

	/// <summary>
	/// Gets or sets the highlight color used for selection indication.
	/// </summary>
	public Color SelectionColor { get; set; } = Color.Blue;

	/// <summary>
	/// Called when the item enters the scene tree.
	/// Sets up the visual highlight bar.
	/// </summary>
	protected override void OnEnter()
	{
		if (Size.X <= 0 || Size.Y <= 0)
			throw new Exception();

		AddChild(
			_bar = new ColorRect()
			{
				Size = Size,
				Color = SelectionColor,
				IsVisible = _selected,
			}
		);

		base.OnEnter();
	}
}

/// <summary>
/// A scrollable list container that manages a limited number of visible <see cref="ListviewItem"/>s,
/// allowing selection, navigation, and layout in vertical or horizontal mode.
/// </summary>
public sealed class Listview : RenderTarget
{
	private Vect2 _avgSize;
	private readonly uint _maxItems;
	private int _scrollIndex, _selectedIndex;
	private float _itemTimeout;
	private int MaxScroll => Math.Max(Children.Count - (int)_maxItems, 0);
	private int MaxSelectedIndex => Children.Count <= _maxItems ? Math.Max(Children.Count - 1, 0) : (int)_maxItems - 1;
	private ListviewDirection _direction;
	private float _spacing = 0f;

	/// <summary>
	/// Gets or sets the delay (in seconds) between selection input actions.
	/// Prevents rapid input skipping.
	/// </summary>
	public float PerItemTimeout { get; set; } = 0.255f;

	/// <summary>
	/// Gets the currently selected <see cref="ListviewItem"/> instance, or null if none.
	/// </summary>
	public ListviewItem SelectedItem => ChildCount > 0 ? (ListviewItem)Children[SelectedIndex] : null;

	/// <summary>
	/// Casts the selected item to the specified type.
	/// </summary>
	/// <typeparam name="T">The type to cast the selected item to.</typeparam>
	public T SelectedItemAs<T>() where T : ListviewItem => (T)SelectedItem;

	/// <summary>
	/// Returns true if the listview is scrolled to the top.
	/// </summary>
	public bool AtTop => _scrollIndex == 0;

	/// <summary>
	/// Returns true if the listview is scrolled to the bottom.
	/// </summary>
	public bool AtBottom => _scrollIndex == MaxScroll;

	/// <summary>
	/// Gets the item at the specified index.
	/// </summary>
	/// <param name="index">The global index of the item.</param>
	/// <exception cref="ArgumentOutOfRangeException">If the index is out of bounds.</exception>
	public ListviewItem this[int index]
	{
		get
		{
			return index < 0 || index >= Children.Count
				? throw new ArgumentOutOfRangeException(nameof(index))
				: (ListviewItem)Children[index];
		}
	}

	/// <summary>
	/// Gets or sets the globally selected index in the listview.
	/// Automatically adjusts scroll position to keep selection in view.
	/// </summary>
	public int SelectedIndex
	{
		get => ChildCount > 0 ? _scrollIndex + _selectedIndex : 0;
		set
		{
			if (ChildCount == 0)
				return;

			// clamp into [0 .. last]
			int target = Math.Clamp(value, 0, Children.Count - 1);

			// if before the current window, scroll up
			if (target < _scrollIndex)
			{
				_scrollIndex = target;
				_selectedIndex = 0;
			}
			// if past the visible window, scroll down
			else if (target > _scrollIndex + MaxSelectedIndex)
			{
				_scrollIndex = target - MaxSelectedIndex;
				_selectedIndex = MaxSelectedIndex;
			}
			// otherwise it’s inside the window
			else
			{
				_selectedIndex = target - _scrollIndex;
			}

			SetDirtyState(DirtyState.Update);
		}
	}

	/// <summary>
	/// Gets or sets the spacing (in pixels) between items.
	/// Triggers a size/layout recalculation.
	/// </summary>
	public float Spacing
	{
		get => _spacing;
		set
		{
			if (_spacing == value) return;
			_spacing = value;
			// recalc container size when spacing changes
			RecalculateSize();
			SetDirtyState(DirtyState.Sort | DirtyState.Update);
		}
	}

	/// <summary>
	/// Gets or sets the layout direction (Vertical or Horizontal).
	/// </summary>
	public ListviewDirection Direction
	{
		get => _direction;
		set
		{
			if (_direction == value) return;
			_direction = value;

			RecalculateSize();
			SetDirtyState(DirtyState.Sort | DirtyState.Update);
		}
	}

	/// <summary>
	/// Event triggered when a new item becomes selected.
	/// </summary>
	public Action<Listview> OnItemSelected;

	/// <summary>
	/// Initializes a new <see cref="Listview"/> with a maximum number of visible items and direction.
	/// </summary>
	/// <param name="maxItems">The maximum number of items visible at one time.</param>
	/// <param name="direction">The scroll direction (vertical or horizontal).</param>
	/// <param name="items">The initial listview items to add.</param>
	public Listview(uint maxItems, ListviewDirection direction, params ListviewItem[] items) : base(items)
	{
		ArgumentOutOfRangeException.ThrowIfZero(maxItems);

		_maxItems = maxItems;
		_avgSize = ComputeAverageSize(items);
		if (_avgSize.X <= 0 || _avgSize.Y <= 0)
			_avgSize = Vect2.One;
		_direction = direction;
		_spacing = 0;
		Size = _avgSize;
		RecalculateSize();
	}

	private void RecalculateSize()
	{
		if (Direction == ListviewDirection.Vertical)
		{
			float totalHeight = _avgSize.Y * _maxItems + _spacing * (_maxItems - 1);
			Size = new Vect2(_avgSize.X, totalHeight);
		}
		else
		{
			float totalWidth = _avgSize.X * _maxItems + _spacing * (_maxItems - 1);
			Size = new Vect2(totalWidth, _avgSize.Y);
		}
	}

	/// <summary>
	/// Adds one or more <see cref="ListviewItem"/>s to the listview.
	/// If non-<see cref="ListviewItem"/> entities are passed, they are ignored.
	/// Automatically recalculates layout and adjusts scroll/selection indices.
	/// </summary>
	/// <param name="children">The entities to add. Only <see cref="ListviewItem"/>s are used.</param>
	public new void AddChild(params Entity[] children)
	{
		if (children == null || children.Length == 0)
			return;

		var c = children.OfType<ListviewItem>().ToArray();
		if (c.Length == 0)
			return;

		base.AddChild(children);

		var avgSize = ComputeAverageSize(c);
		if (avgSize != _avgSize)
		{
			_avgSize = avgSize;
			RecalculateSize();
		}

		_scrollIndex = Math.Clamp(_scrollIndex, 0, MaxScroll);
		_selectedIndex = Math.Clamp(_selectedIndex, 0, MaxSelectedIndex);

		foreach (var p in this.GetAncestorsOfType<Panel>())
			p.SetDirtyState(DirtyState.Sort | DirtyState.Update);

		SetDirtyState(DirtyState.Sort | DirtyState.Update);
	}

	/// <summary>
	/// Removes all children from the listview and resets selection and scroll position.
	/// </summary>
	public new void ClearChildren()
	{
		base.ClearChildren();

		_selectedIndex = 0;
		_scrollIndex = 0;

		SetDirtyState(DirtyState.Sort | DirtyState.Update);
	}

	/// <summary>
	/// Removes one or more children from the listview and adjusts internal state accordingly.
	/// </summary>
	/// <param name="children">The entities to remove.</param>
	/// <returns>True if at least one entity was removed; otherwise, false.</returns>
	public new bool RemoveChild(params Entity[] children)
	{
		var result = base.RemoveChild(children);
		if (!result)
			return false;

		_scrollIndex = Math.Min(_scrollIndex, MaxScroll);
		_scrollIndex = Math.Max(_scrollIndex, 0);

		_selectedIndex = Math.Min(_selectedIndex, MaxSelectedIndex);
		_selectedIndex = Math.Max(_scrollIndex, 0);

		SetDirtyState(DirtyState.Sort | DirtyState.Update);

		return result;
	}

	/// <summary>
	/// Updates input and timing for selection throttling.
	/// </summary>
	protected override void OnUpdate()
	{
		if (_itemTimeout >= 0f)
			_itemTimeout -= Clock.DeltaTime;

		base.OnUpdate();
	}

	/// <summary>
	/// Recalculates the visual layout and selection status of all child items.
	/// Called automatically when dirty state changes.
	/// </summary>
	/// <param name="state">The dirty state that triggered the update.</param>
	protected override void OnDirty(DirtyState state)
	{
		float offset = 0f;
		int index = 0;

		// scroll offset along primary axis
		Offset = Direction == ListviewDirection.Vertical
			? new Vect2(0, _avgSize.Y * _scrollIndex + _spacing * _scrollIndex)
			: new Vect2(_avgSize.X * _scrollIndex + _spacing * _scrollIndex, 0);

		for (int i = 0; i < Children.Count; i++)
		{
			var c = (ListviewItem)Children[i];
			if (!c.IsVisible) continue;

			c.Position = Direction == ListviewDirection.Vertical
				? new Vect2(0, offset)
				: new Vect2(offset, 0);

			c.Selected = index == _scrollIndex + _selectedIndex;
			if (c.Selected)
			{
				OnItemSelected?.Invoke(this);
			}

			index++;

			// advance by itemSize + spacing
			offset += Direction == ListviewDirection.Vertical
				? _avgSize.Y + _spacing
				: _avgSize.X + _spacing;
		}

		base.OnDirty(state);
	}

	private static Vect2 ComputeAverageSize(ListviewItem[] items)
	{
		float maxW = 0f, maxH = 0f;
		foreach (var item in items)
		{
			maxW = Math.Max(maxW, item.Size.X);
			maxH = Math.Max(maxH, item.Size.Y);
		}
		return new Vect2(maxW, maxH);
	}

	/// <summary>
	/// Moves selection to the previous item, scrolling if necessary.
	/// </summary>
	public void PreviousItem()
	{
		if (ChildCount == 0 || _itemTimeout >= 0f)
			return;

		// if we can move selection within the visible window…
		if (_selectedIndex > 0)
		{
			_selectedIndex--;
		}
		// otherwise, if there’s more to scroll back through…
		else if (_scrollIndex > 0)
		{
			_scrollIndex--;
		}
		else
		{
			// at absolute start—nothing to do
			return;
		}

		_itemTimeout += PerItemTimeout;
		SetDirtyState(DirtyState.Update);
	}

	/// <summary>
	/// Moves selection to the next item, scrolling if necessary.
	/// </summary>
	public void NextItem()
	{
		if (ChildCount == 0 || _itemTimeout >= 0f)
			return;

		// if we can move selection forward within the visible window…
		if (_selectedIndex < MaxSelectedIndex)
		{
			_selectedIndex++;
		}
		// otherwise, if there’s more content to scroll into view…
		else if (_scrollIndex < MaxScroll)
		{
			_scrollIndex++;
		}
		else
		{
			// at absolute end—nothing to do
			return;
		}

		_itemTimeout += PerItemTimeout;
		SetDirtyState(DirtyState.Update);
	}

	/// <summary>
	/// Casts the selected index to a corresponding enum value.
	/// </summary>
	/// <typeparam name="TEnum">An enum type to map the selected index to.</typeparam>
	/// <returns>The enum value at the selected index.</returns>
	/// <exception cref="InvalidOperationException">Thrown if the selected index is out of range.</exception>
	public TEnum GetSelectedIndexAsEnum<TEnum>() where TEnum : struct, Enum
	{
		int idx = SelectedIndex;
		int length = Enum.GetValues<TEnum>().Length;

		if (idx < 0 || idx >= length)
		{
			throw new InvalidOperationException(
				$"SelectedIndex {idx} is outside the range of enum {typeof(TEnum).Name} (0-{length - 1}).");
		}

		// cast via object to satisfy the compiler
		return (TEnum)(object)idx;
	}
}
