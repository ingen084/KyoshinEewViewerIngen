using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaEdit.TextMate;
using KyoshinEewViewer.Services;
using Splat;
using TextMateSharp.Grammars;

namespace KyoshinEewViewer.Views.Components;

public partial class TemplateEditorDialog : Window
{
    private bool _isResultOk = false;
    
    public TemplateEditorDialog()
    {
        InitializeComponent();
        InitializeEditor();
    }
    
    public static async Task<(bool success, string? templateText)> ShowAsync(Window parent, string title, string initialText)
    {
        var viewModel = new TemplateEditorDialogViewModel
        {
            Title = title,
            TemplateText = initialText
        };
        
        var dialog = new TemplateEditorDialog
        {
            DataContext = viewModel
        };
        
        // エディタに初期テキストを設定
        dialog.Editor.Text = initialText;
        
        // テキスト変更を監視してViewModelに反映
        dialog.Editor.TextChanged += (_, _) =>
        {
            viewModel.TemplateText = dialog.Editor.Text;
        };
        
        await dialog.ShowDialog(parent);
        
        if (dialog._isResultOk)
            return (true, viewModel.TemplateText);
        
        return (false, null);
    }
    
    public static async Task<(bool success, string? templateText)> ShowAsync(string title, string initialText)
    {
        var viewModel = new TemplateEditorDialogViewModel
        {
            Title = title,
            TemplateText = initialText
        };
        
        var dialog = new TemplateEditorDialog
        {
            DataContext = viewModel
        };
        
        // エディタに初期テキストを設定
        dialog.Editor.Text = initialText;
        
        // テキスト変更を監視してViewModelに反映
        dialog.Editor.TextChanged += (_, _) =>
        {
            viewModel.TemplateText = dialog.Editor.Text;
        };
        
        // SubWindowServiceから設定ウィンドウを取得
        var subWindowService = Locator.Current.GetService<ISubWindowsService>();
        var settingWindow = subWindowService?.SettingWindow;
        
        if (settingWindow != null)
            await dialog.ShowDialog(settingWindow);
        else
            dialog.Show();
        
        if (dialog._isResultOk)
            return (true, viewModel.TemplateText);
        
        return (false, null);
    }
    
    private void InitializeEditor()
    {
        // エディタの設定
        Editor.TextArea.TextView.Options.ShowSpaces = true;
        Editor.TextArea.TextView.Options.ShowTabs = true;
        
        // TextMateによるシンタックスハイライト
        // Scribanテンプレート向けにRazorシンタックスを適用
        var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
        Editor.InstallTextMate(registryOptions)
            .SetGrammar(registryOptions.GetScopeByLanguageId(registryOptions.GetLanguageByExtension(".razor").Id));
    }
    
    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        _isResultOk = true;
        Close();
    }
    
    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        _isResultOk = false;
        Close();
    }
}