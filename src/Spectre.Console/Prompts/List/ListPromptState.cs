namespace Spectre.Console;

internal sealed class ListPromptState<T>
    where T : notnull
{
    private readonly Func<T, string> _converter;

    public int Index { get; set; }
    public int ItemCount => Items.Count;
    public int PageSize { get; }
    public bool WrapAround { get; }
    public SelectionMode Mode { get; }
    public bool SkipUnselectableItems { get; private set; }
    public bool SearchEnabled { get; }
    public IReadOnlyList<ListPromptItem<T>> Items { get; }
    public IReadOnlyList<ListPromptItem<T>> SeachItems => !OnlyShowSearchedText || SearchFunc == null ? Items : Items.Where(x => SearchFunc(x.Data, SearchText)).ToList().AsReadOnly();

    private readonly IReadOnlyList<int>? _leafIndexes;
    private IReadOnlyList<int> _searchLeafIndexes;

    public ListPromptItem<T> Current => Items[Index];
    public string SearchText { get; private set; }
    public bool OnlyShowSearchedText { get; private set; }
    public Func<T, string, bool>? SearchFunc { get; private set; }


    public ListPromptState(
        IReadOnlyList<ListPromptItem<T>> items,
        Func<T, string> converter,
        int pageSize, bool wrapAround,
        SelectionMode mode,
        bool skipUnselectableItems,
        bool searchEnabled,
        bool onlyShowSearchedText,
        Func<T, string, bool>? searchFunc = null)
    {
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        Items = items;
        PageSize = pageSize;
        WrapAround = wrapAround;
        Mode = mode;
        SkipUnselectableItems = skipUnselectableItems;
        SearchEnabled = searchEnabled;
        SearchText = string.Empty;
        OnlyShowSearchedText = onlyShowSearchedText;
        SearchFunc = searchFunc ?? DefaultSearch;
        if (SkipUnselectableItems && mode == SelectionMode.Leaf)
        {
            _leafIndexes =
                Items
                    .Select((item, index) => new { item, index })
                    .Where(x => !x.item.IsGroup)
                    .Select(x => x.index)
                    .ToList()
                    .AsReadOnly();

            Index = _leafIndexes.FirstOrDefault();
        }
        else
        {
            Index = 0;
        }
    }

    public bool Update(ConsoleKeyInfo keyInfo)
    {
        var index = Index;
        if (SkipUnselectableItems && Mode == SelectionMode.Leaf && (String.IsNullOrEmpty(SearchText) || !OnlyShowSearchedText))
        {
            Debug.Assert(_leafIndexes != null, nameof(_leafIndexes) + " != null");
            var leafIndexes = (OnlyShowSearchedText ? _searchLeafIndexes : _leafIndexes) ?? _leafIndexes;

            var currentLeafIndex = OnlyShowSearchedText ? index : leafIndexes.IndexOf(index);
            switch (keyInfo.Key)
            {
                case ConsoleKey.UpArrow:
                    if (currentLeafIndex > 0)
                    {
                        index = leafIndexes[currentLeafIndex - 1];
                    }
                    else if (WrapAround)
                    {
                        index = leafIndexes.LastOrDefault();
                    }

                    break;

                case ConsoleKey.DownArrow:
                    if (currentLeafIndex < leafIndexes.Count - 1)
                    {
                        index = leafIndexes[currentLeafIndex + 1];
                    }
                    else if (WrapAround)
                    {
                        index = leafIndexes.FirstOrDefault();
                    }

                    break;

                case ConsoleKey.Home:
                    index = leafIndexes.FirstOrDefault();
                    break;

                case ConsoleKey.End:
                    index = leafIndexes.LastOrDefault();
                    break;

                case ConsoleKey.PageUp:
                    index = Math.Max(currentLeafIndex - PageSize, 0);
                    if (index < leafIndexes.Count)
                    {
                        index = leafIndexes[index];
                    }

                    break;

                case ConsoleKey.PageDown:
                    index = Math.Min(currentLeafIndex + PageSize, leafIndexes.Count - 1);
                    if (index < leafIndexes.Count)
                    {
                        index = leafIndexes[index];
                    }

                    break;
            }
        }
        else
        {
            index = keyInfo.Key switch
            {
                ConsoleKey.UpArrow => Index - 1,
                ConsoleKey.DownArrow => Index + 1,
                ConsoleKey.Home => 0,
                ConsoleKey.End => ItemCount - 1,
                ConsoleKey.PageUp => Index - PageSize,
                ConsoleKey.PageDown => Index + PageSize,
                _ => Index,
            };

            if (OnlyShowSearchedText)
            {
                if (index < 0) { index = 0; }
                if (index >= _searchLeafIndexes.Count) { index = _searchLeafIndexes.Count - 1; }
            }
        }

        var search = SearchText;

        if (SearchEnabled)
        {
            bool updateIndex = false;

            // If is text input, append to search filter
            if (!char.IsControl(keyInfo.KeyChar))
            {
                search = SearchText + keyInfo.KeyChar;
                updateIndex = true;
            }

            if (keyInfo.Key == ConsoleKey.Backspace && search.Length > 0)
            {
                search = search.Substring(0, search.Length - 1);
                updateIndex = true;
            }

            if (OnlyShowSearchedText && SearchFunc != null)
            {
                _searchLeafIndexes =
                Items
                    .Select((item, index) => new { item, index })
                    .Where(x => !x.item.IsGroup && SearchFunc(x.item.Data, search))
                    .Select(x => x.index)
                    .ToList()
                    .AsReadOnly();
            }

            if (updateIndex && SearchFunc != null)
            {
                var item = Items.FirstOrDefault(x =>
                    SearchFunc(x.Data, search)
                    && (!x.IsGroup || Mode != SelectionMode.Leaf));

                if (item != null)
                {
                    index = Items.IndexOf(item);
                }

                if (OnlyShowSearchedText)
                {
                    index = 0;
                }
            }
        }

        index = WrapAround
            ? (ItemCount + (index % ItemCount)) % ItemCount
            : index.Clamp(0, ItemCount - 1);

        if (index != Index || SearchText != search)
        {
            Index = index;
            SearchText = search;
            return true;
        }

        return false;
    }

    private bool DefaultSearch(T input, string search)
    {
        return _converter.Invoke(input).Contains(search, StringComparison.OrdinalIgnoreCase);
    }
}