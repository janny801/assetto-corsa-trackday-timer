using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AcManager.Controls;
using AcManager.Tools;
using AcManager.Tools.Helpers.AcSettings;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.Drive
{
    public static class TrackdayDurationHelper
    {
        public static void EnsureDurationSlider(UserControl trackdayControl)
        {
            try
            {
                if (trackdayControl == null) return;
                Grid rootGrid = trackdayControl.Content as Grid;
                if (rootGrid == null) return;

                Grid mainGrid = null;
                foreach (object child in rootGrid.Children)
                {
                    Grid g = child as Grid;
                    if (g != null && Grid.GetColumn(g) == 0)
                    {
                        mainGrid = g;
                        break;
                    }
                }
                if (mainGrid == null) return;

                foreach (UIElement child in mainGrid.Children)
                {
                    FrameworkElement fe = child as FrameworkElement;
                    if (fe != null && (string)fe.Tag == "TrackdayDurationPanel")
                    {
                        return;
                    }
                }

                while (mainGrid.RowDefinitions.Count < 4)
                {
                    mainGrid.RowDefinitions.Add(new RowDefinition());
                }

                foreach (UIElement child in mainGrid.Children)
                {
                    if (Grid.GetRow(child) == 2 && Grid.GetColumn(child) == 1)
                    {
                        Grid.SetRow(child, 3);
                    }
                }

                StackPanel panel = new StackPanel();
                panel.Tag = "TrackdayDurationPanel";
                object marginRes = trackdayControl.TryFindResource("ElementMargin");
                if (marginRes is Thickness)
                {
                    panel.Margin = (Thickness)marginRes;
                }
                Grid.SetRow(panel, 2);
                Grid.SetColumn(panel, 1);

                ValueLabel valueLabel = new ValueLabel();
                valueLabel.Content = ToolsStrings.Session_TrackDay;
                BetterTextBox.SetMode(valueLabel, SpecialMode.IntegerOrZeroLabel);

                Binding valBinding = new Binding("TrackdayDuration");
                valBinding.Converter = AcSettingsHolder.ZeroToOffConverter;
                valBinding.ConverterParameter = "unlimited";
                valBinding.Mode = BindingMode.TwoWay;
                valueLabel.SetBinding(ValueLabel.ValueProperty, valBinding);

                Binding postfixBinding = new Binding("TrackdayDuration");
                object plurConverter = trackdayControl.TryFindResource("PluralizingConverter");
                if (plurConverter is IValueConverter)
                {
                    postfixBinding.Converter = (IValueConverter)plurConverter;
                    postfixBinding.ConverterParameter = ControlsStrings.Common_MinutePostfix;
                    valueLabel.SetBinding(ValueLabel.PostfixProperty, postfixBinding);
                }

                Binding showPostfixBinding = new Binding("TrackdayDuration");
                object enumBoolConverter = trackdayControl.TryFindResource("EnumToBooleanConverter");
                if (enumBoolConverter is IValueConverter)
                {
                    showPostfixBinding.Converter = (IValueConverter)enumBoolConverter;
                    showPostfixBinding.ConverterParameter = "\u22600";
                    valueLabel.SetBinding(ValueLabel.ShowPostfixProperty, showPostfixBinding);
                }

                Slider slider = new Slider();
                slider.Minimum = 0;
                slider.Maximum = 120;
                slider.SmallChange = 1;
                slider.LargeChange = 5;
                slider.SetBinding(Slider.ValueProperty, new Binding("TrackdayDuration") { Mode = BindingMode.TwoWay });

                panel.Children.Add(valueLabel);
                panel.Children.Add(slider);

                mainGrid.Children.Add(panel);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("EnsureDurationSlider error: " + ex);
            }
        }
    }
}
