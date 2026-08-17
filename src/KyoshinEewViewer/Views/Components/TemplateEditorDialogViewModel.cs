using CommunityToolkit.Mvvm.ComponentModel;

namespace KyoshinEewViewer.Views.Components;

public class TemplateEditorDialogViewModel : ObservableObject
{
    private string _title = "";
    private string _templateText = "";
    
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }
    
    public string TemplateText
    {
        get => _templateText;
        set => SetProperty(ref _templateText, value);
    }
}