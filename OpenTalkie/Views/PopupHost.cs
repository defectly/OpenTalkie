using CommunityToolkit.Maui;
using Microsoft.Maui.Controls.Shapes;

namespace OpenTalkie.Views;

public static class PopupHost
{
    public static async Task ShowAsync(Microsoft.Maui.Controls.View popup)
    {
        if (Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page is Page page)
        {
            var popupOptions = new PopupOptions
            {
                PageOverlayColor = Color.FromArgb("#80000000"),
                CanBeDismissedByTappingOutsideOfPopup = true,
                Shape = new RoundRectangle
                {
                    CornerRadius = new CornerRadius(0),
                    Stroke = Colors.Transparent,
                    StrokeThickness = 0
                },
                Shadow = null
            };

            await page.ShowPopupAsync(popup, popupOptions);
        }
    }
}
