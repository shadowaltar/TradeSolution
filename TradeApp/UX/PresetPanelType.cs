using System;
using TradeApp.ViewModels.Widgets;

namespace TradeApp.UX
{
    public enum PresetPanelType
    {
        [ViewResource("Views/Presets/WelcomeLayoutPanel.xaml")]
        Welcome,
        [ViewResource("Views/Widgets/DepthView.xaml", typeof(DepthViewModel))]
        Depth,

        [ViewResource("Views/Widgets/PriceAndIndicatorsView.xaml", typeof(PriceAndIndicatorsViewModel))]
        BackTesting,

        CandlestickPrice,
        TickPrice,
    }


    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
    public sealed class ViewResourceAttribute : Attribute
    {
        public string ResourcePath { get; }
        public Type? ViewModelType { get; }

        // This is a positional argument
        public ViewResourceAttribute(string path, Type? viewModelType = null)
        {
            ResourcePath = path;
            ViewModelType = viewModelType;
        }
    }
}
