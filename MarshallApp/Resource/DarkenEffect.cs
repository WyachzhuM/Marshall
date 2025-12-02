using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace MarshallApp.Resource;

public class DarkenEffect : ShaderEffect
{
    private static readonly PixelShader Shader = new()
    {
        UriSource = new Uri("pack://application:,,,/MarshallApp;component/Resource/Darken.ps.bytes", UriKind.Absolute)
    };

    public DarkenEffect()
    {
        PixelShader = Shader;
        UpdateShaderValue(InputProperty);
        UpdateShaderValue(TimeProperty);
        UpdateShaderValue(IntensityProperty);
        UpdateShaderValue(GridSizeProperty);
        UpdateShaderValue(ScanlineSpeedProperty);

        CompositionTarget.Rendering += (_, _) =>
        {
            Time = (float)(DateTime.Now - Process.GetCurrentProcess().StartTime).TotalSeconds;
        };
    }

    public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty("Input", typeof(DarkenEffect), 0);
    public Brush Input { get => (Brush)GetValue(InputProperty); set => SetValue(InputProperty, value); }

    public static readonly DependencyProperty TimeProperty = DependencyProperty.Register(nameof(Time), typeof(float), typeof(DarkenEffect),
        new UIPropertyMetadata(0f, PixelShaderConstantCallback(0)));
    public float Time { get => (float)GetValue(TimeProperty); set => SetValue(TimeProperty, value); }

    public static readonly DependencyProperty IntensityProperty = DependencyProperty.Register(nameof(Intensity), typeof(float), typeof(DarkenEffect),
        new UIPropertyMetadata(1f, PixelShaderConstantCallback(1)));
    public float Intensity { get => (float)GetValue(IntensityProperty); set => SetValue(IntensityProperty, value); }

    public static readonly DependencyProperty GridSizeProperty = DependencyProperty.Register(nameof(GridSize), typeof(float), typeof(DarkenEffect),
        new UIPropertyMetadata(45f, PixelShaderConstantCallback(2)));
    public float GridSize { get => (float)GetValue(GridSizeProperty); set => SetValue(GridSizeProperty, value); }

    public static readonly DependencyProperty ScanlineSpeedProperty = DependencyProperty.Register(nameof(ScanlineSpeed), typeof(float), typeof(DarkenEffect),
        new UIPropertyMetadata(1.5f, PixelShaderConstantCallback(3)));
    public float ScanlineSpeed { get => (float)GetValue(ScanlineSpeedProperty); set => SetValue(ScanlineSpeedProperty, value); }
}