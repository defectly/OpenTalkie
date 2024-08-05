namespace OpenTalkie;

/// <summary>
/// Basic ToggleButton implementation.
/// Toggle behavior is done via <see cref="IsToggled"/>.
/// Style is controlled via <see cref="ToggledStyle"/> and standard Style property (for Neutral state).
/// </summary>
public class ToggleButton : Button
{
    /// <summary>
    /// Backup for style
    /// </summary>
    private Style NeutralStyle = null;

    #region IsToggled
    public static readonly BindableProperty IsToggledProperty = BindableProperty.Create(nameof(IsToggled),
                                                                                        typeof(bool),
                                                                                        typeof(ToggleButton),
                                                                                        false,
                                                                                        BindingMode.TwoWay,
                                                                                        propertyChanged: PropertyChangedDelegate);
    /// <summary>
    /// Control Toggled state
    /// </summary>
    public bool IsToggled
    {
        get { return (bool)GetValue(IsToggledProperty); }
        set { SetValue(IsToggledProperty, value); }
    }
    #endregion

    #region ToggledStyle
    /// <summary>
    /// Identifies the <see cref="ToggledStyle"/> bindable property.
    /// </summary>
    public static readonly BindableProperty ToggledStyleProperty =
        BindableProperty.Create(nameof(ToggledStyle),
                                typeof(Style),
                                typeof(ToggleButton),
                                null,
                                BindingMode.OneWay,
                                propertyChanged: (bindable, oldValue, newValue) =>
                                {
                                    if (bindable is ToggleButton tb
                                    && newValue is Style s
                                    && tb.IsToggled)
                                    {
                                        tb.Style = s;
                                    }
                                });
    /// <summary>
    /// Style to apply when button is toggled
    /// </summary>
    /// <seealso cref="ToggledStyleProperty"/>
    public Style ToggledStyle
    {
        get { return (Style)GetValue(ToggledStyleProperty); }
        set { SetValue(ToggledStyleProperty, value); }
    }
    #endregion

    public ToggleButton()
    {
        Command = new Command(() =>
        {
            IsToggled = !IsToggled;
        });
    }

    /// <summary>
    /// Called everytime IsToggledProperty change
    /// </summary>
    private static void PropertyChangedDelegate(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ToggleButton tb && newValue is bool b)
        {
            //button is about to toggle
            if (b)
            {
                //backup neutral style, if needed
                if (tb.NeutralStyle == null)
                {
                    tb.NeutralStyle = tb.Style;
                }
                //apply toggled style
                tb.Style = tb.ToggledStyle;
            }
            //buton is about to go neutral
            else
            {
                //return to neutral
                tb.Style = tb.NeutralStyle;
            }
        }
    }
}