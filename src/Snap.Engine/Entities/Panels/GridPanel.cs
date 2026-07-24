namespace Snap.Engine.Entities.Panels;

/// <summary>
/// A layout panel that arranges its child entities in a uniform grid, with a fixed number of columns and configurable spacing.
/// Supports selection navigation similar to Listview.
/// </summary>
public class GridPanel : Panel
{
	private readonly int _columns;
	private readonly int _spacing;
	private int _selectedIndex;
	private float _itemTimeout;

	/// <summary>
	/// Gets or sets the delay (in seconds) between selection input actions.
	/// Prevents rapid input skipping.
	/// </summary>
	public float PerItemTimeout { get; set; } = 0.255f;

	/// <summary>
	/// Gets or sets whether the grid panel supports keyboard/gamepad navigation.
	/// </summary>
	public bool EnableNavigation { get; set; } = true;

	/// <summary>
	/// Gets the number of rows in the grid based on current child count.
	/// </summary>
	public int Rows => ChildCount > 0 ? (ChildCount + _columns - 1) / _columns : 0;

	/// <summary>
	/// Gets or sets the currently selected index.
	/// </summary>
	public int SelectedIndex
	{
		get => _selectedIndex;
		set
		{
			if (ChildCount == 0 || value == _selectedIndex)
				return;

			int newIndex = Math.Clamp(value, 0, ChildCount - 1);

			if (_selectedIndex >= 0 && _selectedIndex < ChildCount)
			{
				GetListItemAt(_selectedIndex).Selected = false;
			}

			_selectedIndex = newIndex;

			if (_selectedIndex >= 0 && _selectedIndex < ChildCount)
			{
				GetListItemAt(_selectedIndex).Selected = true;
				OnItemSelected?.Invoke(this);
			}

			SetDirtyState(DirtyState.AddOrRemove);
		}
	}

	/// <summary>
	/// Gets the currently selected ListItem, if any.
	/// </summary>
	public ListItem SelectedItem => _selectedIndex >= 0 && _selectedIndex < ChildCount ?
		GetListItemAt(_selectedIndex) : null;

	/// <summary>
	/// Casts the selected item to the specified type.
	/// </summary>
	/// <typeparam name="T">The type to cast the selected item to.</typeparam>
	public T SelectedItemAs<T>() where T : ListItem => (T)SelectedItem;

	/// <summary>
	/// Event triggered when a new item becomes selected.
	/// </summary>
	public Action<GridPanel> OnItemSelected;

	/// <summary>
	/// Initializes a new <see cref="GridPanel"/> with a specific number of columns, spacing between elements, and optional ListItem children.
	/// </summary>
	/// <param name="columns">The number of columns in the grid layout.</param>
	/// <param name="spacing">The horizontal and vertical spacing (in pixels) between grid cells.</param>
	/// <param name="items">The ListItem children to add to the panel.</param>
	public GridPanel(int columns, int spacing, params ListItem[] items) : base(items)
	{
		if (columns <= 0)
			throw new ArgumentException("Columns must be greater than 0", nameof(columns));

		_columns = columns;
		_spacing = spacing;
		_selectedIndex = 0;

		Size = OnResize(items);
	}

	/// <summary>
	/// Initializes a new <see cref="GridPanel"/> with a specific number of columns, using a default spacing of 4 pixels.
	/// </summary>
	/// <param name="columns">The number of columns in the grid layout.</param>
	/// <param name="items">The ListItem children to add to the panel.</param>
	public GridPanel(int columns, params ListItem[] items) : this(columns, spacing: 4, items) { }

	/// <summary>
	/// Called when a child is added to the panel.
	/// Updates the selected index to stay valid.
	/// </summary>
	protected override void OnChildAdded(Entity child)
	{
		base.OnChildAdded(child);

		// Ensure selected index is still valid
		if (_selectedIndex >= ChildCount - 1 && ChildCount > 0)
			_selectedIndex = ChildCount - 1;
	}

	/// <summary>
	/// Called when a child is removed from the panel.
	/// Updates the selected index to stay valid.
	/// </summary>
	protected override void OnChildRemoved(Entity child)
	{
		base.OnChildRemoved(child);

		if (ChildCount == 0)
		{
			_selectedIndex = 0;
		}
		else if (_selectedIndex >= ChildCount)
		{
			_selectedIndex = ChildCount - 1;

			if (ChildCount > 0)
				GetListItemAt(_selectedIndex).Selected = true;
		}
	}

	/// <summary>
	/// Called when the layout needs to be updated due to changes in child visibility, position, or structure.
	/// Recalculates the size and positions of all visible children in the grid.
	/// </summary>
	/// <param name="state">The dirty state triggering the layout update.</param>
	protected override void OnDirty(DirtyState state)
	{
		var visible = Children
			.Where(x => x.Visible && !x.IsExiting)
			.Select(x => x.EntityAs<ListItem>())
			.ToList();

		if (visible.Count == 0)
		{
			Size = Vect2.Zero;
			base.OnDirty(state);
			return;
		}

		Size = OnResize(visible);

		float maxWidth = visible.Max(x => x.Size.X);
		float maxHeight = visible.Max(x => x.Size.Y);

		for (int i = 0; i < visible.Count; i++)
		{
			int row = i / _columns;
			int col = i % _columns;
			var item = visible[i];

			item.Position = new Vect2(
				col * (maxWidth + _spacing),
				row * (maxHeight + _spacing)
			);
		}

		UpdateSelectionChanged(visible);

		if (IsTopmostScreen || Parent == null)
			Screen?.SetDirtyState(DirtyState.Sort | DirtyState.AddOrRemove);

		base.OnDirty(state);
	}

	private void UpdateSelectionChanged(IEnumerable<ListItem> children)
	{
		var index = 0;
		foreach (ListItem child in children)
		{
			child.OnSelectionChanged(child, _selectedIndex);

			if (index == _selectedIndex)
				child.OnSelected(this, _selectedIndex);
			index++;
		}
	}

	/// <summary>
	/// Calculates the total size required to arrange all visible child entities in a grid layout.
	/// </summary>
	/// <param name="children">The collection of child entities to be arranged in the grid.</param>
	/// <returns>
	/// A <see cref="Vect2"/> representing the total calculated size (width and height) needed 
	/// to contain all visible children arranged in the grid with specified columns and spacing.
	/// Returns <see cref="Vect2.Zero"/> if no children are visible.
	/// </returns>
	/// <remarks>
	/// The calculation considers only visible, non-exiting children. The grid arranges children
	/// from left to right, top to bottom, using the specified <see cref="_columns"/> value to
	/// determine the maximum number of children per row. Each cell in the grid is sized to
	/// accommodate the largest child in that dimension, with <see cref="_spacing"/> added between
	/// rows and columns.
	/// </remarks>
	protected override Vect2 OnResize(IEnumerable<Entity> children)
	{
		var visibleChildren = children.Where(x => x.Visible && !x.IsExiting).ToList();

		if (visibleChildren.Count == 0)
			return Vect2.Zero;

		float maxWidth = visibleChildren.Max(x => x.Size.X);
		float maxHeight = visibleChildren.Max(x => x.Size.Y);
		float totalWidth = _columns * maxWidth + (_columns - 1) * _spacing;
		int rows = (visibleChildren.Count + _columns - 1) / _columns;
		float totalHeight = rows * maxHeight + (rows - 1) * _spacing;

		return new Vect2(totalWidth, totalHeight);
	}

	/// <summary>
	/// Updates input handling for navigation.
	/// </summary>
	protected override void OnUpdate()
	{
		if (_itemTimeout >= 0f)
			_itemTimeout -= Clock.DeltaTime;

		base.OnUpdate();
	}

	/// <summary>
	/// Moves selection to the previous item (left).
	/// </summary>
	public void PreviousItem()
	{
		if (!EnableNavigation || ChildCount == 0 || _itemTimeout >= 0f)
			return;

		if (_selectedIndex > 0)
		{
			SelectedIndex--;
			_itemTimeout += PerItemTimeout;
			UpdateSelectionChanged(ChildrenAs<ListItem>());
		}
	}

	/// <summary>
	/// Moves selection to the next item (right).
	/// </summary>
	public void NextItem()
	{
		if (!EnableNavigation || ChildCount == 0 || _itemTimeout >= 0f)
			return;

		if (_selectedIndex < ChildCount - 1)
		{
			SelectedIndex++;
			_itemTimeout += PerItemTimeout;
			UpdateSelectionChanged(ChildrenAs<ListItem>());
		}
	}

	/// <summary>
	/// Moves selection up one row.
	/// </summary>
	public void PreviousRow()
	{
		if (!EnableNavigation || ChildCount == 0 || _itemTimeout >= 0f)
			return;

		int currentRow = _selectedIndex / _columns;
		int currentCol = _selectedIndex % _columns;

		if (currentRow > 0)
		{
			int newIndex = Math.Max((currentRow - 1) * _columns + currentCol, 0);
			newIndex = Math.Min(newIndex, ChildCount - 1);

			SelectedIndex = newIndex;
			_itemTimeout += PerItemTimeout;
			UpdateSelectionChanged(ChildrenAs<ListItem>());
		}
	}

	/// <summary>
	/// Moves selection down one row.
	/// </summary>
	public void NextRow()
	{
		if (!EnableNavigation || ChildCount == 0 || _itemTimeout >= 0f)
			return;

		int currentRow = _selectedIndex / _columns;
		int currentCol = _selectedIndex % _columns;

		if (currentRow < Rows - 1)
		{
			int newIndex = (currentRow + 1) * _columns + currentCol;
			newIndex = Math.Min(newIndex, ChildCount - 1);

			SelectedIndex = newIndex;
			_itemTimeout += PerItemTimeout;
			UpdateSelectionChanged(ChildrenAs<ListItem>());
		}
	}

	/// <summary>
	/// Selects the first item in the grid.
	/// </summary>
	public void SelectFirst()
	{
		if (ChildCount > 0)
		{
			SelectedIndex = 0;
			UpdateSelectionChanged(ChildrenAs<ListItem>());
		}
	}

	/// <summary>
	/// Selects the last item in the grid.
	/// </summary>
	public void SelectLast()
	{
		if (ChildCount > 0)
		{
			SelectedIndex = ChildCount - 1;
			UpdateSelectionChanged(ChildrenAs<ListItem>());
		}
	}

	/// <summary>
	/// Gets the item at the specified grid position (row, column).
	/// Returns null if out of bounds.
	/// </summary>
	public ListItem GetItemAt(int row, int column)
	{
		int index = row * _columns + column;
		return index >= 0 && index < ChildCount ? GetListItemAt(index) : null;
	}

	/// <summary>
	/// Gets the grid position (row, column) of the specified item.
	/// Returns (-1, -1) if not found.
	/// </summary>
	public (int row, int column) GetPositionOf(ListItem item)
	{
		int index = Children.IndexOf(item);
		if (index == -1)
			return (-1, -1);

		return (index / _columns, index % _columns);
	}

	/// <summary>
	/// Gets the ListItem at the specified index.
	/// </summary>
	/// <param name="index">The index of the item.</param>
	/// <exception cref="ArgumentOutOfRangeException">If the index is out of bounds.</exception>
	public ListItem this[int index]
	{
		get
		{
			return index < 0 || index >= ChildCount
				? throw new ArgumentOutOfRangeException(nameof(index))
				: GetListItemAt(index);
		}
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

		return (TEnum)(object)idx;
	}

	private ListItem GetListItemAt(int index)
	{
		return (ListItem)Children[index];
	}
}