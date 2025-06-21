using ReactiveUI;

namespace KyoshinEewViewer.Views.Components;

public class TemplateEditorDialogViewModel : ReactiveObject
{
    private string _title = "";
    private string _templateText = "";
    
    public string Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }
    
    public string TemplateText
    {
        get => _templateText;
        set => this.RaiseAndSetIfChanged(ref _templateText, value);
    }
}