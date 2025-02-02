namespace OpenTalkie.Views;

public partial class FieldView : ContentView
{
    public static readonly BindableProperty IconPathProperty =
        BindableProperty.Create(nameof(IconPath), typeof(string), typeof(FieldView), default(string));

    public static readonly BindableProperty NameProperty =
        BindableProperty.Create(nameof(Name), typeof(string), typeof(FieldView), default(string));

    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(nameof(Value), typeof(string), typeof(FieldView), default(string));

    public string IconPath
    {
        get => (string)GetValue(IconPathProperty);
        set => SetValue(IconPathProperty, value);
    }

    public string Name
    {
        get => (string)GetValue(NameProperty);
        set => SetValue(NameProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public FieldView()
    {
        InitializeComponent();

        Title.BindingContext = this;
        Title.SetBinding(Label.TextProperty, new Binding(nameof(Name), source: this));

        Parameter.BindingContext = this;
        Parameter.SetBinding(Label.TextProperty, new Binding(nameof(Value), source: this));
    }
}