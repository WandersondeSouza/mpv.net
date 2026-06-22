
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

using MpvNet.Windows.UI;
using MpvNet.Help;

namespace MpvNet.Windows.WPF;

public partial class InputWindow : Window
{
    readonly ICollectionView _collectionView;
    readonly string _startupContent;
    public List<Binding> Bindings { get; }
    public Theme? Theme => Theme.Current;
    Binding? _focusedBinding;

    public InputWindow()
    {
        InitializeComponent();
        DataContext = this;

        if (App.InputConf.HasMenu)
            Bindings = InputHelp.Parse(App.InputConf.Content);
        else
            Bindings = InputHelp.GetEditorBindings(App.InputConf.Content);

        _startupContent = InputHelp.ConvertToString(Bindings);
        SearchControl.SearchTextBox.TextChanged += SearchTextBox_TextChanged;
        DataGrid.SelectionMode = DataGridSelectionMode.Single;
        CollectionViewSource collectionViewSource = new CollectionViewSource() { Source = Bindings };
        _collectionView = collectionViewSource.View;
        _collectionView.Filter = new Predicate<object>(item => Filter((Binding)item));
        DataGrid.ItemsSource = _collectionView;
    }

    bool Filter(Binding item)
    {
        if (item.Command == "")
            return false;

        string searchText = SearchControl.SearchTextBox.Text.ToLower();

        if (searchText == "" || searchText == "?")
            return true;

        if (searchText.Length == 1)
            return item.Input.ToLower().Replace("ctrl+", "").Replace("shift+", "").Replace("alt+", "") == searchText.ToLower();
        else if (searchText.StartsWith("i ") || searchText.StartsWith("i:") || searchText.Length == 1)
        {
            if (searchText.Length > 1)
                searchText = searchText.Substring(2).Trim();

            if (searchText.Length < 3)
                return item.Input.ToLower().Replace("ctrl+", "").Replace("shift+", "").Replace("alt+", "").Contains(searchText);
            else
                return item.Input.ToLower().Contains(searchText);
        }
        else if (searchText.StartsWith("n ") || searchText.StartsWith("n:"))
            return item.Comment.ToLower().Contains(searchText.Substring(2).Trim());
        else if (searchText.StartsWith("c ") || searchText.StartsWith("c:"))
            return item.Command.ToLower().Contains(searchText.Substring(2).Trim());
        else if (item.Command.ToLower().Contains(searchText) ||
                 item.Comment.ToLower().Contains(searchText) ||
                 item.Input.ToLower().Contains(searchText))
        {
            return true;
        }
        return false;
    }

    void ShowLearnWindow(Binding? binding)
    {
        LearnWindow window = new LearnWindow();
        window.Owner = this;
        window.InputItem = binding;
        window.ShowDialog();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape)
            Close();

        if (e.Key == Key.F3 || e.Key == Key.F6 || (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control))
        {
            Keyboard.Focus(SearchControl.SearchTextBox);
            SearchControl.SearchTextBox.SelectAll();
        }
    }

    void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => _collectionView.Refresh();

    void Window_Loaded(object sender, RoutedEventArgs e) => Keyboard.Focus(SearchControl.SearchTextBox);

    void Window_Closing(object? sender, CancelEventArgs e)
    {
        string newContent =  InputHelp.ConvertToString(Bindings);

        if (_startupContent == newContent)
            return;

        string duplicateInputMessage = GetDuplicateInputMessage();

        if (duplicateInputMessage != "")
        {
            Msg.ShowWarning(duplicateInputMessage);
            e.Cancel = true;
            return;
        }

        if (App.InputConf.HasMenu)
            FileHelp.WriteAllTextAtomic(App.InputConf.Path, App.InputConf.Content = newContent);
        else
        {
            newContent = InputHelp.ConvertToString(InputHelp.GetReducedBindings(Bindings));
            newContent = newContent.Replace(App.MenuSyntax + " ", "# ");
            FileHelp.WriteAllTextAtomic(App.InputConf.Path, App.InputConf.Content = newContent);
        }

        Msg.ShowInfo(_("Changes will be available on next startup."));
    }

    string GetDuplicateInputMessage()
    {
        var duplicateInputs = Bindings
            .Where(it => it.Input != "" && it.Command != "")
            .GroupBy(it => it.Input)
            .Where(group => group.Select(it => it.Command).Distinct().Skip(1).Any())
            .Select(group => group.Key)
            .Take(8)
            .ToList();

        if (duplicateInputs.Count == 0)
            return "";

        return _("The input editor cannot save duplicate shortcuts with different commands:") +
            BR2 + string.Join(BR, duplicateInputs);
    }

    void DataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if (e.Column.DisplayIndex == 1)
            e.Cancel = true;
    }

    void DataGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
    {
        if (e.AddedCells.Count > 0)
            _focusedBinding = e.AddedCells[0].Item as Binding;
    }

    void DataGridCell_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            e.Handled = true;

        switch (e.Key)
        {
            case Key.Left:
            case Key.Up:
            case Key.Right:
            case Key.Down:
            case Key.Tab:
                break;
            default:
                ShowLearnWindow(_focusedBinding);
                break;
        }
    }

    void DataGridCell_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        ShowLearnWindow(_focusedBinding);

    void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            CommandColumn.MaxWidth = 800;
            CommandColumn.Width = 800;
        }
        else
        {
            CommandColumn.MaxWidth = 322;
            CommandColumn.Width = 322;
        }
    }
}
